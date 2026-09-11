using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;
using OpenAI.Evals;

namespace FtoConsulting.PortfolioManager.Evaluation;

public sealed record FoundryRun(string EvaluationId, string RunId, string Mode, string JudgeModel,
    Dictionary<string, string?> ExecutionTraces, string Evaluator, DateTimeOffset SubmittedAt);
public sealed record FoundryResults(JsonElement Run, List<JsonElement> OutputItems);

/// <summary>All preview protocol details stay here; production projects do not depend on the Foundry evaluation SDK.</summary>
public sealed class FoundryEvaluationClient
{
    private readonly EvaluationClient _client;
    public FoundryEvaluationClient(string endpoint)
    {
        var project = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential());
        _client = project.ProjectOpenAIClient.GetEvaluationClient();
    }
    public FoundryEvaluationClient(EvaluationClient client) => _client = client;

    public static object Definition(string judgeModel, string evaluator, bool traces)
    {
        if (evaluator is not ("relevance" or "tool_call_accuracy" or "tool_output_utilization" or "groundedness" or "task_adherence"))
            throw new ArgumentException("Unsupported evaluator; add and test its input contract explicitly.", nameof(evaluator));
        var mapping = new Dictionary<string, string>
        {
            ["query"] = "{{item.query}}",
            ["response"] = "{{item.response}}"
        };
        if (evaluator.StartsWith("tool_")) mapping["tool_definitions"] = "{{item.tool_definitions}}";
        if (evaluator == "tool_call_accuracy") mapping["tool_calls"] = "{{item.tool_calls}}";
        return new
        {
            name = $"portfolio-{evaluator}-{(traces ? "traces" : "captured")}",
            data_source_config = traces ? (object)new { type = "azure_ai_source", scenario = "traces" } : new
            {
                type = "custom",
                include_sample_schema = false,
                item_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        execution_id = new { type = "string" },
                        scenario_id = new { type = "string" },
                        trace_id = new { type = "string" },
                        query = new { type = "array" },
                        response = new { type = "array" },
                        messages = new { type = "array" },
                        tool_definitions = new { type = "array" },
                        tool_calls = new { type = "array" }
                    },
                    required = new[] { "execution_id", "query", "response", "tool_definitions" }
                }
            },
            testing_criteria = new[] { new
            {
                type = "azure_ai_evaluator", name = evaluator, evaluator_name = $"builtin.{evaluator}",
                initialization_parameters = new { model = judgeModel }, data_mapping = mapping
            } }
        };
    }

    public async Task<FoundryRun> SubmitAsync(IReadOnlyList<CollectedCase> cases, string judgeModel, string evaluator,
        bool traces, CancellationToken ct)
    {
        if (cases.Count == 0) throw new InvalidDataException("No applicable executions to evaluate");
        if (cases.Any(c => c.Execution.Status != Application.DTOs.Ai.AgentExecutionStatus.Completed || ExecutionAssertions.Evaluate(c.Scenario, c.Execution).Any(x => x.Category == "capture" && !x.Passed)))
            throw new InvalidDataException("Only completed executions with valid capture may be submitted");
        // Validate and materialize evidence before creating any remote resource.
        object source = traces ? new
        {
            type = "azure_ai_traces",
            trace_ids = cases.Select(c => c.Execution.TraceId).Distinct().ToArray(),
            lookback_hours = 168
        } : new
        {
            type = "jsonl",
            source = new { type = "file_content", content = cases.Select(c => new { item = ExecutionDataset.CreateItem(c.Execution) }).ToArray() }
        };
        using var definition = BinaryContent.Create(BinaryData.FromObjectAsJson(Definition(judgeModel, evaluator, traces)));
        var eval = Parse(await _client.CreateEvaluationAsync(definition, new RequestOptions { CancellationToken = ct }));
        var evalId = eval.GetProperty("id").GetString()!;
        using var body = BinaryContent.Create(BinaryData.FromObjectAsJson(new { name = $"portfolio-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}", data_source = source }));
        var run = Parse(await _client.CreateEvaluationRunAsync(evalId, body, new RequestOptions { CancellationToken = ct }));
        return new(evalId, run.GetProperty("id").GetString()!, traces ? "traces" : "captured", judgeModel,
            cases.ToDictionary(c => c.Execution.ExecutionId, c => c.Execution.TraceId), evaluator, DateTimeOffset.UtcNow);
    }

    public async Task<FoundryResults> ReadResultsAsync(FoundryRun reference, TimeSpan timeout, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        var options = new RequestOptions { CancellationToken = deadline.Token };
        JsonElement run;
        while (true)
        {
            run = Parse(await _client.GetEvaluationRunAsync(reference.EvaluationId, reference.RunId, options));
            var status = run.GetProperty("status").GetString();
            if (status is "completed" or "failed" or "canceled" or "cancelled") break;
            await Task.Delay(TimeSpan.FromSeconds(5), deadline.Token);
        }
        var items = new List<JsonElement>();
        if (run.GetProperty("status").GetString() != "completed") return new(run, items);
        string? after = null;
        while (true)
        {
            var page = Parse(await _client.GetEvaluationRunOutputItemsAsync(evaluationId: reference.EvaluationId, evaluationRunId: reference.RunId,
                limit: 100, order: "asc", after: after, outputItemStatus: null, options: options));
            items.AddRange(page.GetProperty("data").EnumerateArray().Select(x => x.Clone()));
            if (!page.TryGetProperty("has_more", out var more) || !more.GetBoolean()) break;
            var next = page.GetProperty("last_id").GetString();
            if (string.IsNullOrEmpty(next) || next == after) throw new InvalidDataException("Foundry returned a non-advancing page cursor");
            after = next;
        }
        return new(run, items);
    }

    public Task CancelAsync(FoundryRun run, CancellationToken ct) => _client.CancelEvaluationRunAsync(
        run.EvaluationId, run.RunId, new RequestOptions { CancellationToken = ct });

    public static List<Check> ValidateResults(FoundryRun receipt, FoundryResults results)
    {
        var checks = new List<Check>
        {
            new("cloud", "completed", results.Run.GetProperty("status").GetString() == "completed", results.Run.GetProperty("status").ToString()),
            new("cloud", "coverage", results.OutputItems.Count == receipt.ExecutionTraces.Count && results.OutputItems.Count > 0,
                $"{results.OutputItems.Count} results / {receipt.ExecutionTraces.Count} executions")
        };
        foreach (var item in results.OutputItems)
        {
            var id = item.TryGetProperty("id", out var identifier) ? identifier.ToString() : "unknown";
            var scored = item.TryGetProperty("results", out var values) && values.ValueKind == JsonValueKind.Array && values.GetArrayLength() > 0
                && values.EnumerateArray().All(v => v.TryGetProperty("passed", out var passed) && passed.ValueKind is JsonValueKind.True or JsonValueKind.False
                    && (!v.TryGetProperty("error", out var error) || error.ValueKind == JsonValueKind.Null));
            checks.Add(new("cloud", $"scored:{id}", scored, "Every submitted item needs a non-error judge result; missing scores are not passes"));
        }
        return checks;
    }
    private static JsonElement Parse(ClientResult result)
    {
        using var document = JsonDocument.Parse(result.GetRawResponse().Content.ToMemory());
        return document.RootElement.Clone();
    }
}
