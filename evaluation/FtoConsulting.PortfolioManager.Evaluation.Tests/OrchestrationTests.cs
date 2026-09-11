using System.Diagnostics;
using System.Text.Json;
using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace FtoConsulting.PortfolioManager.Evaluation.Tests;

public sealed class OrchestrationTests
{
    [Theory]
    [InlineData(AgentContextMode.Isolated)]
    [InlineData(AgentContextMode.Seeded)]
    public async Task RealAgentAndTool_UseFixtureWithoutPersistence_AndArtifactsRoundTrip(AgentContextMode mode)
    {
        var config = new ConfigurationBuilder().Build();
        var fixture = new PortfolioFixture("test-v1", new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero), 42,
            [new(new(2026, 9, 11), "TEST.L", "Test Company", 10, 100, 150)]);
        var exported = new SnapshotProcessor();
        using var telemetry = Sdk.CreateTracerProviderBuilder().AddSource("PortfolioManager.*").AddProcessor(exported).Build();
        var captured = new List<Activity>();
        using var listener = CaptureTests.Listen("PortfolioManager.AI", captured);
        await using var host = EvaluationHost.Build(config, fixture, _ => new ScriptedChatClient((round, messages) => round == 1
            ? CaptureTests.Calls(new FunctionCallContent("holdings-1", "GetPortfolioHoldings", new Dictionary<string, object?> { ["date"] = "today" }))
            : CaptureTests.Answer("Your portfolio value is £150.")));
        await using var scope = host.CreateAsyncScope();
        var execution = await scope.ServiceProvider.GetRequiredService<IAiOrchestrationService>().ExecutePortfolioQueryAsync(
            "What are my portfolio holdings today?", 42, new()
            {
                CaptureContent = true,
                ContextMode = mode,
                EffectiveTime = fixture.EffectiveTime,
                SeedMessages = [new(ChatRole.User, "My earlier question"), new(ChatRole.Assistant, "Earlier reply")]
            });
        Assert.True(execution.Status == AgentExecutionStatus.Completed, $"{execution.Status}: {execution.ErrorType}");
        Assert.Null(execution.ThreadId);
        var call = Assert.Single(execution.ToolExecutions);
        Assert.Equal("success", call.ApplicationStatus);
        Assert.Equal(42, call.ExecutionArguments!.Value.GetProperty("accountId").GetInt32());
        Assert.False(call.RequestedArguments!.Value.TryGetProperty("accountId", out _));
        Assert.Contains("150", call.Result!.Value.ToString());
        Assert.NotNull(execution.AgentSpanId);
        var agent = Assert.Single(captured, a => a.SpanId.ToHexString() == execution.AgentSpanId);
        Assert.Equal(execution.AgentId, agent.GetTagItem("gen_ai.agent.id"));
        Assert.Equal(execution.ExecutionId, agent.GetTagItem("portfolio.execution.id"));
        Assert.All(TraceVerifier.Validate(execution, exported.Spans), c => Assert.True(c.Passed, c.Name + ": " + c.Detail));
        var agentExport = Assert.Single(exported.Spans, s => s.SpanId == execution.AgentSpanId);
        Assert.Contains("CRITICAL SAFETY", agentExport.Properties.GetProperty("gen_ai.input.messages").GetString());
        Assert.Contains("holdings-1", agentExport.Properties.GetProperty("gen_ai.output.messages").GetString());
        Assert.Contains("tool_call", agentExport.Properties.GetProperty("gen_ai.output.messages").GetString());
        Assert.Contains("tool_call_response", agentExport.Properties.GetProperty("gen_ai.output.messages").GetString());
        Assert.Contains(TraceVerifier.Validate(execution, exported.Spans.Where(s => s.SpanId != call.SpanId).ToList()), c => c.Name == "tool_count" && !c.Passed);
        var truncatedProperties = JsonSerializer.Deserialize<Dictionary<string, object>>(agentExport.Properties.GetRawText())!;
        truncatedProperties["gen_ai.output.messages"] = "[{\"role\":\"assistant\"";
        var truncated = exported.Spans.Select(s => s.SpanId == agentExport.SpanId ? s with { Properties = JsonSerializer.SerializeToElement(truncatedProperties) } : s).ToList();
        Assert.Contains(TraceVerifier.Validate(execution, truncated), c => c.Name == "structured_messages" && !c.Passed);
        Assert.Equal(mode == AgentContextMode.Seeded, execution.ModelRounds[0].Input.Any(m => m.Content.Any(c => c.Text == "Earlier reply")));
        var scenario = new Scenario { Id = "holdings", Query = "holdings", RequiredTools = [new("GetPortfolioHoldings")], ResponseContains = ["150"] };
        var item = new CollectedCase(scenario, execution, ExecutionAssertions.Evaluate(scenario, execution));
        Assert.All(item.Checks, c => Assert.True(c.Passed, c.Name + ": " + c.Detail));
        var saved = JsonSerializer.Serialize(item, ArtifactJson.Options);
        var restored = JsonSerializer.Deserialize<CollectedCase>(saved, ArtifactJson.Options)!;
        Assert.Equal(execution.ModelRounds.Count, restored.Execution.ModelRounds.Count);
        Assert.Single(restored.Execution.ToolExecutions);
        Assert.Equal(JsonSerializer.Serialize(ExecutionDataset.CreateItem(execution)), JsonSerializer.Serialize(ExecutionDataset.CreateItem(restored.Execution)));
    }

    [Fact]
    public async Task ConcurrentExecutionsInSameTrace_KeepSeparateAgentAndToolSpans()
    {
        var config = new ConfigurationBuilder().Build();
        var fixture = new PortfolioFixture("v1", new(2026, 9, 11, 0, 0, 0, TimeSpan.Zero), 42, [new(new(2026, 9, 11), "TEST", "Test", 1, 100, 150)]);
        var exported = new SnapshotProcessor();
        using var telemetry = Sdk.CreateTracerProviderBuilder().AddSource("PortfolioManager.*").AddProcessor(exported).Build();
        using var source = new ActivitySource("PortfolioManager.Tests.Parent");
        using var parent = source.StartActivity("shared-parent");
        await using var host = EvaluationHost.Build(config, fixture, _ => new ScriptedChatClient((round, _) => round == 1
            ? CaptureTests.Calls(new FunctionCallContent("same-model-call-id", "GetPortfolioHoldings", new Dictionary<string, object?> { ["date"] = "today" })) : CaptureTests.Answer("150")));
        async Task<AgentExecution> Run()
        {
            await using var scope = host.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IAiOrchestrationService>().ExecutePortfolioQueryAsync("Show my holdings today", 42,
                new() { CaptureContent = true, ContextMode = AgentContextMode.Isolated, EffectiveTime = fixture.EffectiveTime });
        }
        var executions = await Task.WhenAll(Run(), Run());
        Assert.Equal(executions[0].TraceId, executions[1].TraceId);
        Assert.NotEqual(executions[0].AgentSpanId, executions[1].AgentSpanId);
        foreach (var execution in executions)
            Assert.All(TraceVerifier.Validate(execution, exported.Spans), c => Assert.True(c.Passed, c.Name));
    }

    private sealed class SnapshotProcessor : BaseProcessor<Activity>
    {
        public List<TraceSpan> Spans { get; } = [];
        public override void OnEnd(Activity activity)
        {
            lock (Spans) Spans.Add(new(activity.TraceId.ToHexString(), activity.SpanId.ToHexString(), activity.DisplayName,
                JsonSerializer.SerializeToElement(activity.TagObjects.ToDictionary(t => t.Key, t => t.Value)), activity.Status != ActivityStatusCode.Error));
        }
    }
}
