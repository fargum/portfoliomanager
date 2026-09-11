using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text.Json;
using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using OpenAI;
using OpenAI.Evals;
#pragma warning disable OPENAI001 // Tests deliberately exercise the pinned preview evaluation protocol.

namespace FtoConsulting.PortfolioManager.Evaluation.Tests;

public sealed class FoundryClientTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Submit_UsesPublishedProtocol_AndPreservesExecutionCorrelation(bool traces)
    {
        using var handler = new ResponsesHandler("""{"id":"eval-1"}""", """{"id":"run-1"}""");
        using var http = new HttpClient(handler);
        var client = Client(http);
        var execution = new AgentExecution { CaptureEnabled = true, Status = AgentExecutionStatus.Completed, TraceId = new('a', 32) };
        execution.ModelRounds.Add(new() { Round = 1, CompletedAt = DateTimeOffset.UtcNow, Input = [new("user", null, [new("text", Text: "Hello")])], Output = [new("assistant", null, [new("text", Text: "Hello back")])] });
        var run = await client.SubmitAsync([new(new() { Id = "hello", Query = "Hello" }, execution, [])], "judge-deployment", "relevance", traces, default);
        Assert.Equal(execution.TraceId, run.ExecutionTraces[execution.ExecutionId]);
        var definition = handler.Bodies[0];
        Assert.Equal("judge-deployment", definition.GetProperty("testing_criteria")[0].GetProperty("initialization_parameters").GetProperty("model").GetString());
        var source = handler.Bodies[1].GetProperty("data_source");
        Assert.Equal(traces ? "azure_ai_traces" : "jsonl", source.GetProperty("type").GetString());
        if (traces) Assert.Equal(execution.TraceId, source.GetProperty("trace_ids")[0].GetString());
        else Assert.Equal(execution.ExecutionId, source.GetProperty("source").GetProperty("content")[0].GetProperty("item").GetProperty("execution_id").GetString());
    }

    [Fact]
    public async Task Results_ReadAllPages_WithAfterCursor()
    {
        using var handler = new ResponsesHandler("""{"status":"completed"}""",
            """{"data":[{"id":"one"}],"has_more":true,"last_id":"one"}""",
            """{"data":[{"id":"two"}],"has_more":false}""");
        using var http = new HttpClient(handler);
        var results = await Client(http).ReadResultsAsync(Receipt(), TimeSpan.FromSeconds(5), default);
        Assert.Equal(2, results.OutputItems.Count);
        Assert.Contains("after=one", handler.Urls[2]);
        Assert.Contains("order=asc", handler.Urls[2]);
    }

    [Fact]
    public async Task FailedRun_ReturnsErrorWithoutRequestingScores()
    {
        using var handler = new ResponsesHandler("""{"status":"failed","error":{"code":"invalid_data"}}""");
        using var http = new HttpClient(handler);
        var results = await Client(http).ReadResultsAsync(Receipt(), TimeSpan.FromSeconds(5), default);
        Assert.Empty(results.OutputItems);
        Assert.Single(handler.Urls);
        Assert.Equal("invalid_data", results.Run.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task RepeatedPageCursor_FailsInsteadOfLooping()
    {
        using var handler = new ResponsesHandler("""{"status":"completed"}""",
            """{"data":[{"id":"one"}],"has_more":true,"last_id":"one"}""",
            """{"data":[{"id":"one"}],"has_more":true,"last_id":"one"}""");
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => Client(http).ReadResultsAsync(Receipt(), TimeSpan.FromSeconds(5), default));
    }

    private static FoundryRun Receipt() => new("eval-1", "run-1", "captured", "judge", [], "relevance", DateTimeOffset.UtcNow);
    private static FoundryEvaluationClient Client(HttpClient http) => new(new EvaluationClient(new ApiKeyCredential("test-only"), new OpenAIClientOptions
    { Endpoint = new Uri("https://test.invalid/openai/v1/"), Transport = new HttpClientPipelineTransport(http), RetryPolicy = new ClientRetryPolicy(0) }));

    private sealed class ResponsesHandler(params string[] responses) : HttpMessageHandler
    {
        private int _index;
        public List<string> Urls { get; } = [];
        public List<JsonElement> Bodies { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.ToString());
            if (request.Content != null)
            {
                using var body = JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
                Bodies.Add(body.RootElement.Clone());
            }
            return new(request.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK) { Content = new StringContent(responses[_index++], System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
