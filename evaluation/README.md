# C# portfolio agent evaluation

This runner calls the same `IAiOrchestrationService` as the API. It records each real model round and function invocation, makes deterministic assertions locally, and can submit the captured interaction to Foundry's hosted evaluators. Cloud evaluation remains preview and requires a live acceptance run in your Foundry project before enforcing CI gates.

## Run locally

From the repository root, with .NET 10 installed:

```powershell
dotnet restore evaluation/FtoConsulting.PortfolioManager.Evaluation --locked-mode
dotnet test evaluation/FtoConsulting.PortfolioManager.Evaluation.Tests
$runner = 'evaluation/FtoConsulting.PortfolioManager.Evaluation'
dotnet run --project $runner -- --help
```

Copy `evaluation/appsettings.example.json` to the ignored `evaluation/appsettings.Local.json` and set the inference endpoint and deployment. Supply `AzureFoundry__ApiKey` through environment configuration. The model inference URL ends in `/openai/v1/`; the evaluation project URL ends in `/api/projects/PROJECT`. They are different endpoints.

```powershell
dotnet run --project $runner -- collect --config evaluation/appsettings.Local.json --suite evaluation/suites/regression.jsonl --fixture evaluation/fixtures/portfolio-v1.json --output evaluation/artifacts/run-001
dotnet run --project $runner -- assert --input evaluation/artifacts/run-001
```

Collection still calls your deployed model and consumes inference tokens. Fixture mode replaces holdings data, uses a fixed effective date, and rejects external market tools. The real agent, guardrails, function loop, MCP dispatcher and portfolio tools run. It does not start the API or background schedulers, load account memory, or write conversation history. Automated tests use scripted model responses and need no credentials, network or database.

The example regression suite includes explicit tool-routing checks, isolated greeting, seeded follow-up and input blocking. These are starter cases, not a comprehensive quality benchmark. Extend it with natural-language routing, numerical answer checks and domain-specific expectations. Unit tests separately cover parallel calls, errors and cancellation.

For live integrations, explicitly use `--live --account-id TEST_ACCOUNT_ID` instead of `--fixture`, configure `ConnectionStrings__EvaluationDatabase` for a test database and supply the applicable EOD/Tavily configuration. `live-integration.jsonl` demonstrates this mode. The default context is still isolated. `Production` context explicitly uses account memory/history and can write to the configured database. `Seeded` context accepts only the supplied messages. Inference configuration is loaded only from the requested JSON file and environment, never automatically from production appsettings or `.env`.

## Evidence and assertions

Every run requires a new output directory. `manifest.json` records the suite hash, fixture and scenarios. Each `case-EXECUTION_ID.json` contains schema-versioned evidence, timestamps, effective date, model deployment, prompt/schema hashes, model-round messages, requested tool arguments, server-injected execution arguments, results, outcomes and trace/span IDs. Set `CODE_REVISION` (or `GITHUB_SHA`) for revision provenance. Attempts default to one; collection never silently retries an entire scenario.

Tool calls are observed inside `FunctionInvokingChatClient.FunctionInvoker`, immediately around the actual function delegate. Calls are not inferred from prose, status messages or tool descriptions. Original call IDs distinguish repeated and parallel uses of the same tool. The existing `execute_tool` activity supplies the span ID; no duplicate tool span is created. A tool returning an `Error` object is recorded separately from a thrown exception. The next model input establishes what the model actually received.

Assertions check terminal outcome, capture completeness, unique call IDs, actual execution, results returned to later model rounds, tool outcomes, required arguments, allowed/forbidden tools, call counts, dependency ordering and response substrings. Empty capture never counts as a successful no-tool answer. Local reports separate capture failures from agent/tool failures.

Exit codes: `0` checks passed or cloud submission succeeded; `1` deterministic or opted-in judge failure; `2` configuration/capture/trace/cloud failure; `130` cancellation or operation timeout. Submission success is not a passing evaluation. `--timeout-seconds` defaults to 180 per scenario or polling operation. Ctrl+C cancels collection; finished/in-flight execution evidence is retained when orchestration returns a cancellation outcome.

Artifacts contain prompts, seeded history, tool arguments/results and portfolio data. Keep them in the ignored artifacts directory and apply your normal access and retention controls. Evaluation capture enables sensitive GenAI telemetry for those runs. Production payload export defaults off and can be enabled explicitly through `AzureFoundry__CaptureSensitiveTelemetry=true`. Existing application logs may still contain query/tool data; this switch controls GenAI telemetry, not all application logging.

## Foundry evaluation

Set `AZURE_AI_PROJECT_ENDPOINT` and authenticate using `DefaultAzureCredential` (for example, an existing Azure CLI login or a workload identity). The caller needs evaluation permissions on that project and access to the judge deployment. The preview SDK is isolated in this console project and pinned to `Azure.AI.Projects 3.0.0-beta.2`; a package lock records transitive versions.

```powershell
dotnet run --project $runner -- evaluate --config evaluation/appsettings.Local.json --input evaluation/artifacts/run-001 --judge YOUR-JUDGE-DEPLOYMENT --evaluator tool_call_accuracy
dotnet run --project $runner -- report --config evaluation/appsettings.Local.json --input evaluation/artifacts/run-001 --run evaluation/artifacts/run-001/foundry-RECEIPT.json --fail-on-judge
dotnet run --project $runner -- cancel --config evaluation/appsettings.Local.json --run evaluation/artifacts/run-001/foundry-RECEIPT.json
```

Supported evaluators: `relevance`, `groundedness`, `task_adherence`, `tool_call_accuracy`, `tool_output_utilization`. Submit each independently so applicability is explicit. Blocked, cancelled, failed and invalid captures are excluded. Tool evaluators also exclude no-tool interactions. The console reports submitted/total counts; zero applicable cases is an error. Local deterministic failures remain visible and are not replaced by judge scores.

The adapter uses `AIProjectClient.ProjectOpenAIClient.GetEvaluationClient()` and protocol `BinaryContent` for custom JSONL inline evidence or exact trace IDs. A receipt records evaluation/run IDs, evaluator, judge and execution-to-trace mapping. Reporting polls to a terminal state, retrieves every output page and retains raw scores/reasons/report URL. A local timeout does not cancel an already submitted cloud run; use the saved receipt with `cancel`. Do not blindly resubmit an ambiguous create-run failure: inspect the Foundry portal first, because a service-side run may already exist.

Microsoft's current cloud C# examples use `initialization_parameters.model`; some evaluator overview examples still use `deployment_name`. This adapter follows the cloud C# contract and isolates that choice in one file. Validate against the target project before enforcing judge gates.

Live validation on 2026-09-12 showed that supplying both unified `messages` and separate `query`/`response` fields causes conflicting mapping requirements for task adherence and groundedness. The adapter sends only the separate conversation arrays, retaining system instructions, real tool calls and tool results. Task adherence also maps the supplied tool definitions.

The Foundry package resolves OpenAI 2.12.0 in the evaluation process; the existing application currently resolves 2.10.0. Application source and Agent Framework middleware are shared, but these runtime dependency sets differ. The manifest records loaded versions. Production package versions have not been upgraded as part of this change; include a production-runtime smoke comparison in acceptance, or separate collection and cloud submission into processes if strict binary parity is required.

## OpenTelemetry correlation

The application starts `portfolio.query`, then the Agent Framework `invoke_agent` span wraps token tracking, function orchestration, model spans and existing `execute_tool` spans. Execution, scenario and suite IDs supplement W3C trace IDs. The agent has stable ID `portfolio-manager`. Child scopes retain the original function call ID and active trace/span context.

Foundry reads conversation attributes on `invoke_agent`, not arbitrary child spans. Agent Framework emits the actual streamed tool calls/results there; this implementation also puts the real function schemas and first actual model input (including instructions and memory/seed context) on that span before export. Capture and export tests verify the agent identity, tool call IDs, arguments, results, instructions and schemas. The API's existing newline-JSON completion envelope now includes `ExecutionId`, `TraceId` and `Outcome`; it also exposes `X-Trace-Id` on the response. No capture endpoint was added.

Configure `APPLICATIONINSIGHTS_CONNECTION_STRING` for the evaluation process, and connect that Application Insights resource to the Foundry project. `LOG_ANALYTICS_WORKSPACE_ID` is the workspace GUID, not the Application Insights application ID. The CLI identity needs permission to query that workspace. Microsoft's trace evaluation guide also requires the project managed identity to have Log Analytics Reader on the linked resources; protected tables require the additional documented reader role.

```powershell
dotnet run --project $runner -- verify-traces --config evaluation/appsettings.Local.json --input evaluation/artifacts/run-001 --timeout-seconds 300
dotnet run --project $runner -- evaluate-traces --config evaluation/appsettings.Local.json --input evaluation/artifacts/run-001 --judge YOUR-JUDGE-DEPLOYMENT --evaluator tool_call_accuracy --timeout-seconds 300
```

The runner uses full sampling and flushes before exit. Verification queries the workspace by exact W3C trace ID, checks the captured agent/tool span IDs and call IDs, compares exported tool arguments/results, and requires agent-level messages and schemas. It retries for ingestion delay. Missing or truncated telemetry fails verification; `evaluate-traces` verifies again before submission. `OTEL_EXPORTER_OTLP_ENDPOINT` optionally exports to an OTLP collector, but a collector alone does not make traces available to Foundry. Payload limits and ingestion behavior must be verified in Azure; large tool results may exceed Application Insights attribute limits even when local capture is complete.

## Azure acceptance checks

1. Run the fixture suite using the intended model deployment. Review deterministic failures independently of judge scores.
2. Submit one captured interaction to each intended evaluator, then retrieve its scores and reasons with the receipt.
3. Confirm the same execution's complete agent/tool evidence in Application Insights, and evaluate its exact trace ID.
4. Run the dedicated live test account suite, including cancellation and dependency failures, then add CI thresholds based on an accepted baseline.

No Azure resource setup, deployment or continuous-evaluation policy is performed by this runner. Existing output-validation code is not wired into the application's streaming path; this work does not claim that responses undergo output safety validation.

References reviewed on 2026-09-11:

- [Microsoft cloud evaluation client setup](https://learn.microsoft.com/en-us/azure/foundry/observability/how-to/cloud-evaluation)
- [Evaluate deployed interactions and trace data requirements](https://learn.microsoft.com/en-us/azure/foundry/observability/how-to/cloud-evaluation-deployed-interactions)
- [Agent evaluator input contracts](https://learn.microsoft.com/en-us/azure/foundry/concepts/evaluation-evaluators/agent-evaluators)
- [Get results, paginate and cancel](https://learn.microsoft.com/en-us/azure/foundry/observability/how-to/cloud-evaluation-results)
