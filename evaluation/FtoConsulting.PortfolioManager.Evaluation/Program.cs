using System.Text.Json;
using Azure.Identity;
using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using FtoConsulting.PortfolioManager.Evaluation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;

return await EvaluationCli.RunAsync(args);

public static class EvaluationCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "--help")
        {
            Console.WriteLine("""
                collect --suite scenarios.jsonl --output NEW_DIRECTORY (--fixture fixture.json | --live --account-id ID) [--model DEPLOYMENT]
                assert --input DIRECTORY
                evaluate --input DIRECTORY --judge DEPLOYMENT [--evaluator relevance]
                verify-traces --input DIRECTORY
                evaluate-traces --input DIRECTORY --judge DEPLOYMENT [--evaluator relevance]
                report --input DIRECTORY [--run RECEIPT.json] [--fail-on-judge]
                cancel --run RECEIPT.json
                Shared: --config FILE, --timeout-seconds N (default 180 per operation/scenario).
                Collection uses the real agent/model; fixture mode replaces portfolio data only.
                """);
            return 0;
        }
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += handler;
        try
        {
            var options = Parse(args.Skip(1).ToArray());
            string Required(string key) => options.GetValueOrDefault(key) ?? throw new ArgumentException($"Missing --{key}");
            var configBuilder = new ConfigurationBuilder();
            if (options.TryGetValue("config", out var configFile)) configBuilder.AddJsonFile(Path.GetFullPath(configFile!), optional: false);
            var config = configBuilder.AddEnvironmentVariables().Build();
            var timeout = TimeSpan.FromSeconds(int.Parse(options.GetValueOrDefault("timeout-seconds") ?? "180"));
            if (timeout <= TimeSpan.Zero) throw new ArgumentException("Timeout must be positive");
            var ct = cancellation.Token;
            FoundryEvaluationClient Foundry() => new(config["AZURE_AI_PROJECT_ENDPOINT"] ?? throw new ArgumentException("Set AZURE_AI_PROJECT_ENDPOINT"));
            if (args[0] == "collect") return await CollectAsync(options, config, timeout, ct);
            if (args[0] == "cancel")
            {
                await Foundry().CancelAsync(await ArtifactJson.ReadAsync<FoundryRun>(Required("run"), ct), ct);
                return 0;
            }
            if (args[0] is not ("assert" or "evaluate" or "verify-traces" or "evaluate-traces" or "report")) throw new ArgumentException($"Unknown command: {args[0]}");
            var directory = Path.GetFullPath(Required("input"));
            var cases = new List<CollectedCase>();
            foreach (var file in Directory.GetFiles(directory, "case-*.json").Order())
            {
                var item = await ArtifactJson.ReadAsync<CollectedCase>(file, ct);
                cases.Add(item with { Checks = ExecutionAssertions.Evaluate(item.Scenario, item.Execution) });
            }
            if (cases.Count == 0) throw new InvalidDataException("No collected cases found");
            var evaluator = options.GetValueOrDefault("evaluator") ?? "relevance";
            var applicable = cases.Where(c => c.Execution.Status == AgentExecutionStatus.Completed && !c.Checks.Any(x => x.Category == "capture" && !x.Passed)
                && (!evaluator.StartsWith("tool_", StringComparison.Ordinal) || c.Execution.ToolExecutions.Count > 0)).ToList();
            if (args[0] is "verify-traces" or "evaluate-traces")
            {
                if (applicable.Count == 0) throw new InvalidDataException("No applicable executions to verify");
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var verifier = new TraceVerifier(http, new DefaultAzureCredential(), config["LOG_ANALYTICS_WORKSPACE_ID"] ?? throw new ArgumentException("Set LOG_ANALYTICS_WORKSPACE_ID"));
                var verified = new List<TraceVerification>();
                foreach (var item in applicable) verified.Add(await verifier.VerifyAsync(item.Execution, timeout, ct));
                Console.WriteLine($"Verified {applicable.Count}/{cases.Count} applicable executions.");
                await ArtifactJson.WriteNewAsync(Path.Combine(directory, $"traces-{Guid.NewGuid():N}.json"), verified, ct);
                if (verified.Any(v => v.Checks.Any(c => !c.Passed))) { Console.Error.WriteLine("Trace verification failed; see trace artifact."); return 2; }
                if (args[0] == "verify-traces") return 0;
            }
            if (args[0] is "evaluate" or "evaluate-traces")
            {
                var run = await Foundry().SubmitAsync(applicable, Required("judge"), evaluator, args[0] == "evaluate-traces", ct);
                var receipt = Path.Combine(directory, $"foundry-{Guid.NewGuid():N}.json");
                await ArtifactJson.WriteNewAsync(receipt, run, CancellationToken.None);
                Console.WriteLine($"Submitted {applicable.Count}/{cases.Count} cases. Receipt: {receipt}");
                return 0;
            }
            var judgeFailed = false;
            if (args[0] == "report" && options.TryGetValue("run", out var runFile))
            {
                var receipt = await ArtifactJson.ReadAsync<FoundryRun>(runFile!, ct);
                if (receipt.ExecutionTraces.Keys.Any(id => !cases.Any(c => c.Execution.ExecutionId == id))) throw new InvalidDataException("Receipt contains executions outside this input directory");
                var scores = await Foundry().ReadResultsAsync(receipt, timeout, ct);
                await ArtifactJson.WriteNewAsync(Path.Combine(directory, $"scores-{Guid.NewGuid():N}.json"), scores, ct);
                if (scores.Run.TryGetProperty("report_url", out var reportUrl)) Console.WriteLine($"Foundry report: {reportUrl}");
                var scoreChecks = FoundryEvaluationClient.ValidateResults(receipt, scores);
                await ArtifactJson.WriteNewAsync(Path.Combine(directory, $"score-checks-{Guid.NewGuid():N}.json"), scoreChecks, ct);
                if (scoreChecks.Any(c => !c.Passed)) { Console.Error.WriteLine("Cloud scoring was incomplete; see score-checks artifact."); return 2; }
                judgeFailed = scores.OutputItems.Any(i => i.TryGetProperty("results", out var results) && results.EnumerateArray().Any(r => r.TryGetProperty("passed", out var passed) && passed.ValueKind == JsonValueKind.False));
            }
            var local = await ReportAsync(directory, cases, ct);
            return local != 0 ? local : judgeFailed && options.ContainsKey("fail-on-judge") ? 1 : 0;
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("Cancelled or timed out."); return 130; }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 2; }
        finally { Console.CancelKeyPress -= handler; }
    }

    private static async Task<int> CollectAsync(Dictionary<string, string?> options, IConfiguration config, TimeSpan timeout, CancellationToken ct)
    {
        string Required(string key) => options.GetValueOrDefault(key) ?? throw new ArgumentException($"Missing --{key}");
        var fixture = options.TryGetValue("fixture", out var fixtureFile) ? await ArtifactJson.ReadAsync<PortfolioFixture>(fixtureFile!, ct) : null;
        if ((fixture != null) == options.ContainsKey("live")) throw new ArgumentException("Choose exactly one of --fixture or --live");
        var accountId = fixture?.AccountId ?? int.Parse(Required("account-id"));
        var suiteText = await File.ReadAllTextAsync(Required("suite"), ct);
        var scenarios = suiteText.Split('\n').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => JsonSerializer.Deserialize<Scenario>(s, ArtifactJson.Options)!).ToList();
        if (scenarios.Count == 0 || scenarios.Any(s => string.IsNullOrWhiteSpace(s.Id) || string.IsNullOrWhiteSpace(s.Query)) || scenarios.Select(s => s.Id).Distinct().Count() != scenarios.Count)
            throw new InvalidDataException("Suite requires nonempty unique scenario IDs and queries");
        if (fixture != null && scenarios.Any(s => s.ContextMode == AgentContextMode.Production)) throw new InvalidDataException("Fixture runs cannot use production memory");
        var directory = Path.GetFullPath(Required("output"));
        if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Output path already exists; use a new directory");
        Directory.CreateDirectory(directory);
        var suiteRunId = Guid.NewGuid().ToString("N");
        var runtimeVersions = new Dictionary<string, string?>
        {
            ["dotnet"] = Environment.Version.ToString(),
            ["OpenAI"] = typeof(OpenAI.OpenAIClient).Assembly.GetName().Version?.ToString(),
            ["AgentFramework"] = typeof(Microsoft.Agents.AI.AIAgent).Assembly.GetName().Version?.ToString(),
            ["Microsoft.Extensions.AI"] = typeof(IChatClient).Assembly.GetName().Version?.ToString()
        };
        await ArtifactJson.WriteNewAsync(Path.Combine(directory, "manifest.json"), new { suiteRunId, suiteHash = AgentExecutionCapture.Hash(suiteText), fixture, scenarios, runtimeVersions }, ct);
        using var telemetry = EvaluationHost.CreateTelemetry(config);
        await using var services = EvaluationHost.Build(config, fixture);
        var cases = new List<CollectedCase>();
        foreach (var scenario in scenarios)
        {
            ct.ThrowIfCancellationRequested();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(timeout);
            await using var scope = services.CreateAsyncScope();
            var execution = await scope.ServiceProvider.GetRequiredService<IAiOrchestrationService>().ExecutePortfolioQueryAsync(scenario.Query, accountId,
                new AgentExecutionOptions
                {
                    CaptureContent = true,
                    ContextMode = scenario.ContextMode,
                    SuiteRunId = suiteRunId,
                    ScenarioId = scenario.Id,
                    ConversationId = scenario.ConversationId,
                    FixtureVersion = fixture?.Version,
                    EffectiveTime = fixture?.EffectiveTime,
                    CodeRevision = config["CODE_REVISION"] ?? config["GITHUB_SHA"] ?? "unknown",
                    SeedMessages = scenario.SeedMessages.Select(m => new ChatMessage(new ChatRole(m.Role), m.Text)).ToList()
                },
                threadId: scenario.ThreadId, modelId: options.GetValueOrDefault("model"), cancellationToken: deadline.Token);
            var item = new CollectedCase(scenario, execution, ExecutionAssertions.Evaluate(scenario, execution));
            cases.Add(item);
            await ArtifactJson.WriteNewAsync(Path.Combine(directory, $"case-{execution.ExecutionId}.json"), item, CancellationToken.None);
            Console.WriteLine($"{scenario.Id}: {execution.Status}, {execution.ToolExecutions.Count} tools, trace {execution.TraceId}");
        }
        if (!telemetry.ForceFlush()) Console.Error.WriteLine("Telemetry flush timed out; verify exported traces before trace evaluation.");
        return await ReportAsync(directory, cases, ct);
    }

    private static async Task<int> ReportAsync(string directory, List<CollectedCase> cases, CancellationToken ct)
    {
        var lines = new List<string> { "# Portfolio evaluation", "", "| Scenario | Status | Failed checks |", "|---|---|---:|" };
        foreach (var item in cases) lines.Add($"| {item.Scenario.Id.Replace('|', '/')} | {item.Execution.Status} | {item.Checks.Count(c => !c.Passed)} |");
        foreach (var item in cases)
            foreach (var check in item.Checks.Where(c => !c.Passed)) lines.Add($"\n- {item.Scenario.Id}: [{check.Category}] {check.Name}: {check.Detail}");
        var path = Path.Combine(directory, $"report-{Guid.NewGuid():N}.md");
        await File.WriteAllLinesAsync(path, lines, ct);
        Console.WriteLine(path);
        if (cases.Any(c => c.Checks.Any(x => x.Category == "capture" && !x.Passed) ||
            c.Execution.Status is AgentExecutionStatus.Failed or AgentExecutionStatus.Cancelled && c.Execution.Status != c.Scenario.ExpectedStatus)) return 2;
        return cases.Any(c => c.Checks.Any(x => !x.Passed)) ? 1 : 0;
    }

    private static Dictionary<string, string?> Parse(string[] args)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        var flags = new[] { "live", "fail-on-judge" };
        var values = new[] { "suite", "output", "fixture", "account-id", "model", "input", "judge", "evaluator", "run", "config", "timeout-seconds" };
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i].StartsWith("--", StringComparison.Ordinal) ? args[i][2..] : throw new ArgumentException($"Unexpected argument: {args[i]}");
            string? value = null;
            if (!flags.Contains(key))
            {
                if (!values.Contains(key) || ++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Invalid or missing value for --{key}");
                value = args[i];
            }
            if (!result.TryAdd(key, value)) throw new ArgumentException($"Duplicate --{key}");
        }
        return result;
    }
}
