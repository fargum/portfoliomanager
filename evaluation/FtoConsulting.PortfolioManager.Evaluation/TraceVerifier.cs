using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;
using FtoConsulting.PortfolioManager.Application.DTOs.Ai;

namespace FtoConsulting.PortfolioManager.Evaluation;

public sealed record TraceSpan(string TraceId, string SpanId, string Name, JsonElement Properties, bool? Success);
public sealed record TraceVerification(string ExecutionId, List<Check> Checks, List<TraceSpan> Spans);

public sealed class TraceVerifier(HttpClient httpClient, TokenCredential credential, string workspaceId)
{
    public static List<Check> Validate(AgentExecution execution, IReadOnlyList<TraceSpan> spans)
    {
        var result = new List<Check>();
        void Add(string name, bool ok, string detail) => result.Add(new("trace", name, ok, detail));
        var own = spans.Where(s => s.TraceId == execution.TraceId).ToList();
        var agent = own.FirstOrDefault(s => Normalize(s.SpanId) == execution.AgentSpanId);
        Add("agent_span", agent != null, execution.AgentSpanId ?? "Missing agent span ID");
        if (agent != null)
        {
            Add("agent_identity", Get(agent, "gen_ai.agent.id") == execution.AgentId, execution.AgentId);
            Add("execution_identity", Get(agent, "portfolio.execution.id") == execution.ExecutionId, execution.ExecutionId);
            Add("agent_operation", Get(agent, "gen_ai.operation.name") == "invoke_agent", "Foundry requires invoke_agent");
        }
        var toolSpans = own.Where(s => Get(s, "gen_ai.operation.name") == "execute_tool" && Get(s, "portfolio.execution.id") == execution.ExecutionId).ToList();
        Add("tool_count", toolSpans.Count == execution.ToolExecutions.Count, $"{toolSpans.Count} exported / {execution.ToolExecutions.Count} captured");
        foreach (var call in execution.ToolExecutions)
        {
            var matches = toolSpans.Where(s => Get(s, "gen_ai.tool.call.id") == call.ToolCallId).ToList();
            var span = matches.Count == 1 ? matches[0] : null;
            Add($"tool:{call.ToolCallId}", span != null && Normalize(span.SpanId) == call.SpanId && Get(span, "gen_ai.tool.name") == call.Name,
                $"{matches.Count} matching spans for {call.Name}");
            if (span == null) continue;
            Add($"arguments:{call.ToolCallId}", JsonMatches(Get(span, "gen_ai.tool.call.arguments"), call.RequestedArguments), "Exported arguments must match capture");
            if (call.InvocationStatus == "completed")
                Add($"result:{call.ToolCallId}", JsonMatches(Get(span, "gen_ai.tool.call.result"), call.Result), "Exported result must match capture without truncation");
            if (call.InvocationStatus == "failed" || call.ApplicationStatus == "error")
                Add($"error:{call.ToolCallId}", span.Success == false, "Failed tool span must retain error status");
        }
        Add("input_messages", agent != null && !string.IsNullOrEmpty(Get(agent, "gen_ai.input.messages")), "Foundry reads input messages on invoke_agent");
        Add("output_messages", agent != null && !string.IsNullOrEmpty(Get(agent, "gen_ai.output.messages")), "Foundry reads output messages on invoke_agent");
        if (execution.ToolDefinitions.Count > 0)
            Add("tool_definitions", agent != null && !string.IsNullOrEmpty(Get(agent, "gen_ai.tool.definitions")), "Foundry needs schemas on invoke_agent");
        if (agent != null)
        {
            try
            {
                using var input = JsonDocument.Parse(Get(agent, "gen_ai.input.messages") ?? "null");
                using var output = JsonDocument.Parse(Get(agent, "gen_ai.output.messages") ?? "null");
                var inputs = Parts(input.RootElement).ToList();
                var outputs = Parts(output.RootElement).ToList();
                var instructions = execution.ModelRounds.FirstOrDefault()?.Instructions;
                if (instructions != null) Add("instructions", inputs.Any(p => p.TryGetProperty("content", out var text) && text.ValueKind == JsonValueKind.String && text.GetString() == instructions), "Agent input must retain exact model instructions");
                foreach (var call in execution.ToolExecutions)
                {
                    var requests = outputs.Where(p => PartType(p) == "tool_call" && PartId(p) == call.ToolCallId).ToList();
                    Add($"agent_request:{call.ToolCallId}", requests.Count == 1 && requests[0].TryGetProperty("arguments", out var args) && PayloadMatches(args, call.RequestedArguments), "Agent messages must retain actual tool arguments");
                    var replies = outputs.Where(p => PartType(p) == "tool_call_response" && PartId(p) == call.ToolCallId).ToList();
                    if (call.InvocationStatus == "completed")
                        Add($"agent_result:{call.ToolCallId}", replies.Count == 1 && replies[0].TryGetProperty("response", out var reply) && PayloadMatches(reply, call.Result), "Agent messages must retain complete tool results");
                }
                var finalText = string.Concat(execution.ModelRounds.LastOrDefault()?.Output.SelectMany(m => m.Content).Where(c => c.Type == "text").Select(c => c.Text) ?? []);
                var exportedText = string.Concat(outputs.Where(p => PartType(p) == "text").Select(p => p.GetProperty("content").GetString()));
                Add("final_response", exportedText.Contains(finalText, StringComparison.Ordinal), "Exported response must retain the final model text");
                if (execution.ToolDefinitions.Count > 0)
                {
                    using var definitions = JsonDocument.Parse(Get(agent, "gen_ai.tool.definitions") ?? "null");
                    Add("schemas_complete", definitions.RootElement.ValueKind == JsonValueKind.Array && execution.ToolDefinitions.All(t => definitions.RootElement.EnumerateArray().Any(d =>
                        d.TryGetProperty("name", out var name) && name.GetString() == t.Name && d.TryGetProperty("parameters", out var schema) && JsonElement.DeepEquals(schema, t.Parameters))), "Exported schemas must match actual function schemas");
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
            {
                Add("structured_messages", false, "Missing, malformed or truncated agent payloads");
            }
        }
        return result;
    }

    public async Task<TraceVerification> VerifyAsync(AgentExecution execution, TimeSpan timeout, CancellationToken ct)
    {
        if (!Regex.IsMatch(execution.TraceId ?? "", "^[a-f0-9]{32}$") || !Guid.TryParse(workspaceId, out _))
            throw new InvalidDataException("A W3C trace ID and Log Analytics workspace GUID are required");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        ct = timeoutCts.Token;
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            var token = await credential.GetTokenAsync(new TokenRequestContext(["https://api.loganalytics.io/.default"]), ct);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.loganalytics.azure.com/v1/workspaces/{workspaceId}/query");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            request.Content = JsonContent.Create(new
            {
                query = $"AppDependencies | where OperationId == '{execution.TraceId}' | project OperationId, Id, Name, Properties, Success",
                timespan = "P7D"
            });
            using var response = await httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (json.RootElement.TryGetProperty("error", out _)) throw new InvalidDataException("Log Analytics returned a partial query error");
            var spans = json.RootElement.GetProperty("tables")[0].GetProperty("rows").EnumerateArray().Select(r =>
                new TraceSpan(r[0].GetString()!, r[1].GetString()!, r[2].GetString()!,
                    r[3].ValueKind == JsonValueKind.String ? ParseProperties(r[3].GetString()!) : r[3].Clone(),
                    r[4].ValueKind is JsonValueKind.True or JsonValueKind.False ? r[4].GetBoolean() : null)).ToList();
            var checks = Validate(execution, spans);
            if (checks.All(c => c.Passed) || DateTimeOffset.UtcNow >= deadline) return new(execution.ExecutionId, checks, spans);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
    private static string? Get(TraceSpan span, string name) => span.Properties.TryGetProperty(name, out var v) ? v.ToString() : null;
    private static IEnumerable<JsonElement> Parts(JsonElement messages) => messages.EnumerateArray().SelectMany(m => m.GetProperty("parts").EnumerateArray());
    private static string? PartType(JsonElement part) => part.TryGetProperty("type", out var type) ? type.GetString() : null;
    private static string? PartId(JsonElement part) => part.TryGetProperty("id", out var id) ? id.GetString() : null;
    private static bool PayloadMatches(JsonElement actual, JsonElement? expected) => expected != null &&
        (JsonElement.DeepEquals(actual, expected.Value) || actual.ValueKind == JsonValueKind.String && JsonMatches(actual.GetString(), expected));
    private static JsonElement ParseProperties(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
    private static string Normalize(string id) => id.Trim('|').Split('.').Last(x => x.Length > 0);
    private static bool JsonMatches(string? json, JsonElement? expected)
    {
        if (json == null || expected == null) return false;
        try { using var parsed = JsonDocument.Parse(json); return JsonElement.DeepEquals(parsed.RootElement, expected.Value); }
        catch (JsonException) { return false; }
    }
}
