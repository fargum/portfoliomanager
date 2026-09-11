using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Utilities;
using Microsoft.Extensions.AI;

namespace FtoConsulting.PortfolioManager.Evaluation.Tests;

public sealed class CaptureTests
{
    [Fact]
    public async Task Streaming_ParallelRepeatedTools_PreservesIdsResultsAndSpans()
    {
        var source = $"PortfolioManager.Tests.{Guid.NewGuid():N}";
        var spans = new List<Activity>();
        using var listener = Listen(source, spans);
        var capture = new AgentExecutionCapture(new() { CaptureContent = true });
        var entered = 0;
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<string> Echo(string value, CancellationToken ct)
        {
            if (Interlocked.Increment(ref entered) == 2) bothStarted.SetResult();
            await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            return value;
        }
        var model = new ScriptedChatClient((round, messages) => round == 1
            ? Calls(new FunctionCallContent("call-a", "echo", new Dictionary<string, object?>() { ["value"] = "alpha" }), new FunctionCallContent("call-b", "echo", new Dictionary<string, object?>() { ["value"] = "beta" }))
            : Answer(string.Join(",", messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Select(c => c.Result))));
        using var client = Pipeline(model, capture, source);
        await foreach (var _ in client.GetStreamingResponseAsync([new(ChatRole.User, "echo both")], new() { Tools = [AIFunctionFactory.Create(Echo, "echo")] })) { }

        Assert.Equal(2, entered);
        Assert.Equal(2, capture.Execution.ModelRounds.Count);
        Assert.Equal(["call-a", "call-b"], capture.Execution.ToolExecutions.Select(c => c.ToolCallId).Order());
        foreach (var call in capture.Execution.ToolExecutions)
        {
            Assert.Equal("completed", call.InvocationStatus);
            var span = Assert.Single(spans, s => s.GetTagItem("gen_ai.tool.call.id") as string == call.ToolCallId);
            Assert.Equal(span.SpanId.ToHexString(), call.SpanId);
            Assert.Equal(span.TraceId.ToHexString(), call.TraceId);
            Assert.Equal(call.RequestedArguments!.Value.GetProperty("value").GetString(), call.Result!.Value.GetString());
        }
        Assert.Equal(2, capture.Execution.ModelRounds[1].Input.SelectMany(m => m.Content).Count(c => c.Type == "tool_result"));
    }

    [Fact]
    public async Task UnknownTool_IsCapturedAsRequestWithoutFabricatedExecution()
    {
        var capture = new AgentExecutionCapture(new() { CaptureContent = true });
        using var client = Pipeline(new ScriptedChatClient((round, _) => round == 1 ? Calls(new FunctionCallContent("missing", "unknown", new Dictionary<string, object?>())) : Answer("unavailable")), capture, "PortfolioManager.Unknown");
        await client.GetResponseAsync([new(ChatRole.User, "try unknown")], new() { Tools = [AIFunctionFactory.Create(() => "ok", "known")] });
        Assert.Empty(capture.Execution.ToolExecutions);
        Assert.Contains(capture.Execution.ModelRounds[0].Output.SelectMany(m => m.Content), c => c.Name == "unknown");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ErrorAndException_AreDistinguished(bool throws)
    {
        var capture = new AgentExecutionCapture(new() { CaptureContent = true });
        object Fail() => throws ? throw new InvalidOperationException("test failure") : new { Error = "dependency unavailable" };
        using var client = Pipeline(new ScriptedChatClient((round, _) => round == 1 ? Calls(new FunctionCallContent("failed", "fail", new Dictionary<string, object?>())) : Answer("cannot complete")), capture, "PortfolioManager.Failure");
        await client.GetResponseAsync([new(ChatRole.User, "fail")], new() { Tools = [AIFunctionFactory.Create(Fail, "fail")] });
        var call = Assert.Single(capture.Execution.ToolExecutions);
        Assert.Equal(throws ? "failed" : "completed", call.InvocationStatus);
        Assert.Equal(throws ? "unknown" : "error", call.ApplicationStatus);
    }

    [Fact]
    public async Task Cancellation_ReachesToolAndPreservesIncompleteCapture()
    {
        var capture = new AgentExecutionCapture(new() { CaptureContent = true });
        using var cts = new CancellationTokenSource();
        async Task<string> Wait(CancellationToken ct) { cts.Cancel(); await Task.Delay(1000, ct); return "unexpected"; }
        using var client = Pipeline(new ScriptedChatClient((_, _) => Calls(new FunctionCallContent("wait", "wait", new Dictionary<string, object?>()))), capture, "PortfolioManager.Cancel");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetResponseAsync([new(ChatRole.User, "wait")],
            new() { Tools = [AIFunctionFactory.Create(Wait, "wait")] }, cts.Token));
        Assert.Equal("cancelled", Assert.Single(capture.Execution.ToolExecutions).InvocationStatus);
    }

    [Fact]
    public void Snapshot_DoesNotChangeWhenArgumentsAreMutated()
    {
        var capture = new AgentExecutionCapture(new() { CaptureContent = true });
        var args = new Dictionary<string, object?> { ["date"] = "today" };
        var messages = capture.Messages([new(ChatRole.Assistant, [new FunctionCallContent("one", "tool", args)])]);
        args["date"] = "tomorrow";
        Assert.Equal("today", messages[0].Content[0].Arguments!.Value.GetProperty("date").GetString());
    }

    [Fact]
    public async Task Clock_ParallelExecutions_AreIsolatedAndRestored()
    {
        async Task<DateOnly> Parse(int year)
        {
            using var clock = PortfolioClock.Use(new DateTimeOffset(year, 1, 2, 0, 0, 0, TimeSpan.Zero));
            await Task.Yield();
            return DateUtilities.ParseDate("yesterday");
        }
        Assert.Equal([new DateOnly(2020, 1, 1), new DateOnly(2021, 1, 1)], await Task.WhenAll(Parse(2020), Parse(2021)));
        Assert.Equal(DateTime.UtcNow.Year, PortfolioClock.UtcNow.Year);
    }

    internal static IChatClient Pipeline(IChatClient model, AgentExecutionCapture capture, string source) => model.AsBuilder()
        .UseFunctionInvocation(configure: c => { c.AllowConcurrentInvocation = true; c.FunctionInvoker = capture.InvokeFunctionAsync; })
        .Use(inner => new ExecutionRecordingChatClient(inner, capture))
        .UseOpenTelemetry(sourceName: source, configure: c => c.EnableSensitiveData = true).Build();
    internal static ChatResponse Calls(params FunctionCallContent[] calls) => new(new ChatMessage(ChatRole.Assistant, calls.Cast<AIContent>().ToList())) { FinishReason = ChatFinishReason.ToolCalls };
    internal static ChatResponse Answer(string text) => new(new ChatMessage(ChatRole.Assistant, text)) { FinishReason = ChatFinishReason.Stop };
    internal static ActivityListener Listen(string source, List<Activity> spans)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => { lock (spans) spans.Add(a); }
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}

internal sealed class ScriptedChatClient(Func<int, List<ChatMessage>, ChatResponse> respond) : IChatClient
{
    private int _round;
    public void Dispose() { }
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType == typeof(ChatClientMetadata) ? new ChatClientMetadata("test", null, "scripted") : null;
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => Task.FromResult(respond(Interlocked.Increment(ref _round), messages.ToList()));
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in response.ToChatResponseUpdates()) { await Task.Yield(); yield return update; }
    }
}
