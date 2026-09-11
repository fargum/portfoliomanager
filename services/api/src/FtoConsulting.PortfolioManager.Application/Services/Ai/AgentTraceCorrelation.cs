using System.Diagnostics;
using FtoConsulting.PortfolioManager.Application.DTOs.Ai;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>Enriches already sampled activities; it neither enables sampling nor creates duplicate spans.</summary>
public sealed class AgentTraceCorrelation : IDisposable
{
    private readonly ActivityListener _listener;
    public AgentTraceCorrelation(AgentExecution execution)
    {
        void Enrich(Activity activity)
        {
            if (activity.TraceId.ToHexString() != execution.TraceId) return;
            // Several executions can share an incoming trace. Use the closest execution ancestor.
            var owner = activity;
            while (owner != null && owner.GetTagItem("portfolio.execution.id") == null) owner = owner.Parent;
            if (owner?.GetTagItem("portfolio.execution.id") as string != execution.ExecutionId) return;
            activity.SetTag("gen_ai.agent.id", execution.AgentId);
            activity.SetTag("gen_ai.conversation.id", execution.ConversationId);
            activity.SetTag("portfolio.execution.id", execution.ExecutionId);
            activity.SetTag("evaluation.run.id", execution.SuiteRunId);
            activity.SetTag("evaluation.scenario.id", execution.ScenarioId);
            for (var current = activity; current != null; current = current.Parent)
            {
                if (current.GetTagItem("portfolio.execution.id") as string != execution.ExecutionId) break;
                if (current.GetTagItem("gen_ai.operation.name") as string != "invoke_agent") continue;
                execution.AgentSpanId = current.SpanId.ToHexString();
                // Agent Framework emits schemas on chat spans, while Foundry reads invoke_agent.
                // Enrich the live ancestor before exporters receive its OnEnd notification.
                if (execution.CaptureEnabled && current.GetTagItem("gen_ai.tool.definitions") == null)
                    current.SetTag("gen_ai.tool.definitions", System.Text.Json.JsonSerializer.Serialize(execution.ToolDefinitions.Select(t =>
                        new { type = "function", name = t.Name, description = t.Description, parameters = t.Parameters })));
                break;
            }
        }
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith("PortfolioManager.", StringComparison.Ordinal),
            ActivityStarted = Enrich,
            ActivityStopped = Enrich
        };
        ActivitySource.AddActivityListener(_listener);
    }
    public void Dispose() => _listener.Dispose();
}
