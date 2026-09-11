using FtoConsulting.PortfolioManager.Application.DTOs.Ai;

namespace FtoConsulting.PortfolioManager.Evaluation;

/// <summary>Foundry conversation format. The final model input contains the real function-result messages and prior rounds.</summary>
public static class ExecutionDataset
{
    public static object CreateItem(AgentExecution execution)
    {
        if (!execution.CaptureEnabled || execution.CaptureErrors.Count != 0 || execution.ModelRounds.Count == 0)
            throw new InvalidDataException($"Execution {execution.ExecutionId} has no complete model evidence");
        var first = execution.ModelRounds[0];
        var query = new List<object>();
        if (!string.IsNullOrEmpty(first.Instructions)) query.Add(new { role = "system", content = first.Instructions });
        query.AddRange(first.Input.Select(ToMessage));
        // Each round's new model output, then tool results from the next input. Do not duplicate repeated history.
        var response = new List<object>();
        var seenResults = new HashSet<string>();
        for (var i = 0; i < execution.ModelRounds.Count; i++)
        {
            var round = execution.ModelRounds[i];
            if (i > 0)
            {
                var previousCalls = execution.ModelRounds.Take(i).SelectMany(r => r.Output).SelectMany(m => m.Content)
                    .Where(c => c.Type == "tool_call").Select(c => c.ToolCallId).ToHashSet();
                foreach (var message in round.Input.Where(m => m.Content.Any(c => c.Type == "tool_result" && previousCalls.Contains(c.ToolCallId))))
                {
                    var parts = message.Content.Where(c => c.Type == "tool_result" && c.ToolCallId != null && seenResults.Add(c.ToolCallId)).ToList();
                    foreach (var part in parts) response.Add(ToMessage(message with { Content = [part] }));
                }
            }
            response.AddRange(round.Output.Select(ToMessage));
        }
        return new
        {
            execution_id = execution.ExecutionId,
            scenario_id = execution.ScenarioId,
            trace_id = execution.TraceId,
            query,
            response,
            messages = query.Concat(response).ToArray(),
            response_text = execution.Response,
            query_text = execution.Query,
            tool_definitions = execution.ToolDefinitions.Select(t => new { name = t.Name, description = t.Description, parameters = t.Parameters }),
            tool_calls = execution.ModelRounds.SelectMany(r => r.Output).SelectMany(m => m.Content).Where(c => c.Type == "tool_call")
                .Select(c => new { type = "tool_call", tool_call_id = c.ToolCallId, name = c.Name, arguments = c.Arguments })
        };
    }

    private static object ToMessage(MessageSnapshot message) => new
    {
        role = message.Role,
        content = message.Content.Select(c => c.Type switch
        {
            "text" => (object)new { type = "text", text = c.Text },
            "tool_call" => new { type = "tool_call", tool_call_id = c.ToolCallId, name = c.Name, arguments = c.Arguments },
            "tool_result" => new { type = "tool_result", tool_call_id = c.ToolCallId, tool_result = c.Result },
            _ => throw new InvalidDataException($"Unsupported captured content: {c.Type}")
        }).ToArray(),
        tool_call_id = message.Role == "tool" ? message.Content.FirstOrDefault()?.ToolCallId : null
    };
}
