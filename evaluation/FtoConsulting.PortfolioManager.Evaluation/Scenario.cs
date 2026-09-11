using System.Text.Json;
using System.Text.Json.Serialization;
using FtoConsulting.PortfolioManager.Application.DTOs.Ai;

namespace FtoConsulting.PortfolioManager.Evaluation;

public sealed record Scenario
{
    public required string Id { get; init; }
    public required string Query { get; init; }
    public AgentContextMode ContextMode { get; init; } = AgentContextMode.Isolated;
    public List<SeedMessage> SeedMessages { get; init; } = [];
    public string? ConversationId { get; init; }
    public int? ThreadId { get; init; }
    public AgentExecutionStatus ExpectedStatus { get; init; } = AgentExecutionStatus.Completed;
    public List<ExpectedTool> RequiredTools { get; init; } = [];
    public List<string> AllowedTools { get; init; } = [];
    public List<string> ForbiddenTools { get; init; } = [];
    public int? MaximumToolCalls { get; init; }
    public bool ExpectNoTools { get; init; }
    public List<string> ResponseContains { get; init; } = [];
    public List<ToolOrder> RequiredOrder { get; init; } = [];
}
public sealed record SeedMessage(string Role, string Text);
public sealed record ExpectedTool(string Name, Dictionary<string, JsonElement>? Arguments = null, int MinimumCalls = 1);
public sealed record ToolOrder(string Before, string After);
public sealed record Check(string Category, string Name, bool Passed, string Detail);
public sealed record CollectedCase(Scenario Scenario, AgentExecution Execution, List<Check> Checks);

public static class ArtifactJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        Converters = { new JsonStringEnumConverter() }
    };
    public static async Task WriteNewAsync<T>(string path, T value, CancellationToken ct = default)
    {
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(file, value, Options, ct);
    }
    public static async Task<T> ReadAsync<T>(string path, CancellationToken ct = default)
    {
        await using var file = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(file, Options, ct) ?? throw new InvalidDataException($"Empty artifact: {path}");
    }
}
