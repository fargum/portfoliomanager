using System.Text.Json;
using FtoConsulting.PortfolioManager.Application.DTOs.Ai;

namespace FtoConsulting.PortfolioManager.Evaluation;

public static class ExecutionAssertions
{
    public static List<Check> Evaluate(Scenario scenario, AgentExecution execution)
    {
        var checks = new List<Check>();
        void Add(string category, string name, bool passed, string detail) => checks.Add(new(category, name, passed, detail));
        Add("agent", "terminal_status", execution.Status == scenario.ExpectedStatus, execution.Status.ToString());
        Add("capture", "enabled", execution.CaptureEnabled, "Structured capture must be enabled");
        Add("capture", "serialization", execution.CaptureErrors.Count == 0, string.Join(", ", execution.CaptureErrors));
        if (execution.Status == AgentExecutionStatus.Completed)
        {
            Add("capture", "model_evidence", execution.ModelRounds.Count > 0 && execution.ModelRounds.All(r => r.ErrorType == null && r.CompletedAt != null), "Completed executions need complete model rounds");
            Add("agent", "response_present", !string.IsNullOrWhiteSpace(execution.Response), "Completed executions must produce an answer");
        }
        var requests = execution.ModelRounds.SelectMany(r => r.Output).SelectMany(m => m.Content)
            .Where(c => c.Type == "tool_call").ToList();
        Add("capture", "unique_call_ids", requests.All(c => !string.IsNullOrWhiteSpace(c.ToolCallId)) &&
            requests.Select(c => c.ToolCallId).Distinct().Count() == requests.Count, "Model requests must have unique nonempty call IDs");
        foreach (var call in requests)
        {
            var executed = execution.ToolExecutions.Where(t => t.ToolCallId == call.ToolCallId).ToList();
            Add("tool", $"executed:{call.ToolCallId}", executed.Count == 1, $"{call.Name}: {executed.Count} invocations");
            var requestedRound = execution.ModelRounds.First(r => r.Output.Any(m => m.Content.Contains(call))).Round;
            var returned = execution.ModelRounds.Where(r => r.Round > requestedRound).SelectMany(r => r.Input).SelectMany(m => m.Content)
                .Any(c => c.Type == "tool_result" && c.ToolCallId == call.ToolCallId);
            if (execution.Status == AgentExecutionStatus.Completed)
                Add("capture", $"returned_to_model:{call.ToolCallId}", returned, "Tool result must appear in a later model input");
        }
        foreach (var tool in execution.ToolExecutions)
        {
            Add("capture", $"request_for_execution:{tool.ToolCallId}", requests.Any(r => r.ToolCallId == tool.ToolCallId && r.Name == tool.Name), tool.Name);
            Add("tool", $"outcome:{tool.ToolCallId}", tool.InvocationStatus == "completed" && tool.ApplicationStatus == "success",
                $"{tool.InvocationStatus}/{tool.ApplicationStatus}");
        }
        if (scenario.ExpectNoTools) Add("agent", "no_tools", requests.Count == 0 && execution.ToolExecutions.Count == 0, $"{requests.Count} requested");
        if (scenario.MaximumToolCalls is int max) Add("agent", "tool_count", requests.Count <= max, $"{requests.Count} <= {max}");
        foreach (var expected in scenario.RequiredTools)
        {
            var matches = requests.Count(r => r.Name == expected.Name && Matches(r.Arguments, expected.Arguments));
            Add("agent", $"required:{expected.Name}", matches >= expected.MinimumCalls, $"{matches} calls with expected arguments");
        }
        foreach (var request in requests)
        {
            Add("agent", $"permitted:{request.ToolCallId}", !scenario.ForbiddenTools.Contains(request.Name!) &&
                (scenario.AllowedTools.Count == 0 || scenario.AllowedTools.Contains(request.Name!)), request.Name!);
        }
        foreach (var order in scenario.RequiredOrder)
        {
            var before = execution.ToolExecutions.Where(c => c.Name == order.Before).ToList();
            var after = execution.ToolExecutions.Where(c => c.Name == order.After).ToList();
            Add("agent", $"order:{order.Before}:{order.After}", before.Count > 0 && after.Count > 0 &&
                before.Max(c => c.CompletedAt) <= after.Min(c => c.StartedAt), "Dependency must complete before dependent tool starts");
        }
        foreach (var expected in scenario.ResponseContains)
            Add("agent", "response_contains", execution.Response?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true, expected);
        return checks;
    }

    private static bool Matches(JsonElement? actual, Dictionary<string, JsonElement>? expected) => expected == null ||
        actual is { ValueKind: JsonValueKind.Object } obj && expected.All(p => obj.TryGetProperty(p.Key, out var value) && JsonElement.DeepEquals(value, p.Value));
}
