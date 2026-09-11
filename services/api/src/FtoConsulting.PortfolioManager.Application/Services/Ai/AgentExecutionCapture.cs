using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using Microsoft.Extensions.AI;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>One recorder per invocation. It is explicitly passed through the pipeline, never resolved from tool DI scopes.</summary>
public sealed class AgentExecutionCapture(AgentExecutionOptions options)
{
    private readonly object _gate = new();
    public AgentExecutionOptions Options { get; } = options;
    public AgentExecution Execution { get; } = new()
    {
        SuiteRunId = options.SuiteRunId,
        ScenarioId = options.ScenarioId,
        Attempt = options.Attempt,
        FixtureVersion = options.FixtureVersion,
        CodeRevision = options.CodeRevision,
        ContextMode = options.ContextMode,
        CaptureEnabled = options.CaptureContent,
        EffectiveTime = options.EffectiveTime ?? DateTimeOffset.UtcNow,
        ConversationId = options.ConversationId
    };

    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public void RecordCaptureError(Exception error)
    {
        lock (_gate) Execution.CaptureErrors.Add(error.GetType().Name);
    }

    public JsonElement? Snapshot(object? value)
    {
        try { return JsonSerializer.SerializeToElement(value); }
        catch (Exception ex)
        {
            RecordCaptureError(ex);
            return null;
        }
    }

    public List<MessageSnapshot> Messages(IEnumerable<ChatMessage> messages) => messages.Select(m =>
        new MessageSnapshot(m.Role.Value, m.MessageId, m.Contents.Select(c => c switch
        {
            TextContent t => new ContentSnapshot("text", Text: t.Text),
            FunctionCallContent f => new ContentSnapshot("tool_call", ToolCallId: f.CallId, Name: f.Name, Arguments: Snapshot(f.Arguments)),
            FunctionResultContent f => new ContentSnapshot("tool_result", ToolCallId: f.CallId, Result: Snapshot(f.Result), ErrorType: f.Exception?.GetType().FullName),
            _ => new ContentSnapshot(c.GetType().Name)
        }).ToList())).ToList();

    public ModelRoundSnapshot BeginRound(IEnumerable<ChatMessage> messages, ChatOptions? chatOptions)
    {
        lock (_gate)
        {
            var round = new ModelRoundSnapshot
            {
                Round = Execution.ModelRounds.Count + 1,
                Input = Messages(messages),
                Instructions = chatOptions?.Instructions
            };
            Execution.ModelRounds.Add(round);
            if (round.Round == 1) EnrichAgentInput(round);
            return round;
        }
    }

    private void EnrichAgentInput(ModelRoundSnapshot round)
    {
        var agent = Activity.Current;
        while (agent != null && agent.GetTagItem("gen_ai.operation.name") as string != "invoke_agent") agent = agent.Parent;
        if (agent == null) return;
        // The agent wrapper sees caller messages before memory/compaction and excludes ChatOptions.Instructions.
        // Export the first actual model input, including instructions, for Foundry's agent-level query extraction.
        try
        {
            var messages = new List<object>();
            if (round.Instructions != null) messages.Add(new { role = "system", parts = new[] { new { type = "text", content = round.Instructions } } });
            messages.AddRange(round.Input.Select(m => new
            {
                role = m.Role,
                parts = m.Content.Select(c => c.Type switch
            {
                "text" => (object)new { type = "text", content = c.Text },
                "tool_call" => new { type = "tool_call", id = c.ToolCallId, name = c.Name, arguments = c.Arguments },
                "tool_result" => new { type = "tool_call_response", id = c.ToolCallId, response = c.Result },
                _ => throw new InvalidDataException($"Unsupported trace input content: {c.Type}")
            }).ToArray()
            }));
            agent.SetTag("gen_ai.input.messages", JsonSerializer.Serialize(messages));
        }
        catch (Exception ex) { RecordCaptureError(ex); }
    }

    public async ValueTask<object?> InvokeFunctionAsync(FunctionInvocationContext context, CancellationToken cancellationToken)
    {
        var call = new ToolExecutionSnapshot
        {
            ToolCallId = context.CallContent.CallId,
            Name = context.Function.Name,
            Iteration = context.Iteration,
            CallIndex = context.FunctionCallIndex,
            RequestedArguments = Snapshot(context.CallContent.Arguments),
            TraceId = Activity.Current?.TraceId.ToHexString(),
            SpanId = Activity.Current?.SpanId.ToHexString()
        };
        lock (_gate) Execution.ToolExecutions.Add(call);
        try
        {
            var result = await context.Function.InvokeAsync(context.Arguments, cancellationToken).ConfigureAwait(false);
            call.Result = Snapshot(result);
            call.InvocationStatus = "completed";
            call.ApplicationStatus = HasError(call.Result) ? "error" : "success";
            if (call.ApplicationStatus == "error")
            {
                Activity.Current?.SetTag("portfolio.tool.application_status", "error");
                Activity.Current?.SetStatus(ActivityStatusCode.Error, "Tool returned an application error");
            }
            return result;
        }
        catch (Exception ex)
        {
            call.InvocationStatus = ex is OperationCanceledException ? "cancelled" : "failed";
            call.ErrorType = ex.GetType().FullName;
            throw;
        }
        finally { call.CompletedAt = DateTimeOffset.UtcNow; }
    }

    public void RecordExecutionArguments(string callId, object arguments)
    {
        lock (_gate)
        {
            var call = Execution.ToolExecutions.LastOrDefault(c => c.ToolCallId == callId);
            if (call != null) call.ExecutionArguments = Snapshot(arguments);
        }
    }

    private static bool HasError(JsonElement? element) => element is { ValueKind: JsonValueKind.Object } obj &&
        obj.EnumerateObject().Any(p => p.Name.Equals("error", StringComparison.OrdinalIgnoreCase) &&
            p.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.False) && p.Value.ToString().Length > 0);
}
