using System.Text.Json;
using Microsoft.Extensions.AI;

namespace FtoConsulting.PortfolioManager.Application.DTOs.Ai;

public enum AgentContextMode { Production, Isolated, Seeded }
public enum AgentExecutionStatus { Running, Completed, Blocked, Failed, Cancelled, Truncated, Filtered }

public sealed record AgentExecutionOptions
{
    public bool CaptureContent { get; init; }
    public AgentContextMode ContextMode { get; init; } = AgentContextMode.Production;
    public string? SuiteRunId { get; init; }
    public string? ScenarioId { get; init; }
    public int Attempt { get; init; } = 1;
    public string? ConversationId { get; init; }
    public string? FixtureVersion { get; init; }
    public string? CodeRevision { get; init; }
    public DateTimeOffset? EffectiveTime { get; init; }
    public IReadOnlyList<ChatMessage> SeedMessages { get; init; } = [];
}

public sealed class AgentExecution
{
    public int SchemaVersion { get; init; } = 1;
    public string ExecutionId { get; init; } = Guid.NewGuid().ToString("N");
    public string AgentId { get; set; } = "portfolio-manager";
    public string? SuiteRunId { get; init; }
    public string? ScenarioId { get; init; }
    public int Attempt { get; init; }
    public string? FixtureVersion { get; init; }
    public string? CodeRevision { get; init; }
    public AgentContextMode ContextMode { get; init; }
    public DateTimeOffset EffectiveTime { get; init; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? FirstContentAt { get; set; }
    public string? TraceId { get; set; }
    public string? AgentSpanId { get; set; }
    public string? ConversationId { get; set; }
    public int? ThreadId { get; set; }
    public string? Model { get; set; }
    public string? PromptHash { get; set; }
    public string? ToolSchemaHash { get; set; }
    public string? Instructions { get; set; }
    public string? Query { get; set; }
    public string? Response { get; set; }
    public AgentExecutionStatus Status { get; set; }
    public string? ErrorType { get; set; }
    public bool CaptureEnabled { get; init; }
    public List<string> CaptureErrors { get; } = [];
    public List<ToolDefinitionSnapshot> ToolDefinitions { get; } = [];
    public List<ModelRoundSnapshot> ModelRounds { get; } = [];
    public List<ToolExecutionSnapshot> ToolExecutions { get; } = [];
}

public sealed record ToolDefinitionSnapshot(string Name, string? Description, JsonElement Parameters);
public sealed record MessageSnapshot(string Role, string? MessageId, List<ContentSnapshot> Content);
public sealed record ContentSnapshot(string Type, string? Text = null, string? ToolCallId = null,
    string? Name = null, JsonElement? Arguments = null, JsonElement? Result = null, string? ErrorType = null);

public sealed class ModelRoundSnapshot
{
    public int Round { get; init; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ResponseId { get; set; }
    public string? FinishReason { get; set; }
    public string? ErrorType { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public string? Instructions { get; init; }
    public List<MessageSnapshot> Input { get; init; } = [];
    public List<MessageSnapshot> Output { get; set; } = [];
}

public sealed class ToolExecutionSnapshot
{
    public required string ToolCallId { get; init; }
    public required string Name { get; init; }
    public int Iteration { get; init; }
    public int CallIndex { get; init; }
    public JsonElement? RequestedArguments { get; init; }
    public JsonElement? ExecutionArguments { get; set; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public string InvocationStatus { get; set; } = "running";
    public string ApplicationStatus { get; set; } = "unknown";
    public JsonElement? Result { get; set; }
    public string? ErrorType { get; set; }
}
