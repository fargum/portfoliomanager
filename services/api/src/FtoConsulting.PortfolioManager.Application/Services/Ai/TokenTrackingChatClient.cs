using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>
/// Wrapper for IChatClient that provides comprehensive token usage tracking and logging
/// </summary>
public class TokenTrackingChatClient : IChatClient
{
    private static readonly ActivitySource s_activitySource = new("PortfolioManager.AI.TokenTracking");

    private readonly IChatClient _innerClient;
    private readonly ILogger<TokenTrackingChatClient> _logger;
    private readonly int _accountId;
    private readonly string _clientId;

    public TokenTrackingChatClient(IChatClient innerClient, ILogger<TokenTrackingChatClient> logger, int accountId, string? clientId = null)
    {
        _innerClient = innerClient;
        _logger = logger;
        _accountId = accountId;
        _clientId = clientId ?? Guid.NewGuid().ToString("N")[..8];
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return _innerClient.GetService(serviceType, serviceKey);
    }

    public void Dispose()
    {
        _innerClient.Dispose();
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatMessages);

        using var activity = s_activitySource.StartActivity("ChatCompletion");
        var stopwatch = Stopwatch.StartNew();
        var callId = Guid.NewGuid().ToString("N")[..12];

        // Log request details
        var messageCount = chatMessages.Count();
        var estimatedInputTokens = EstimateTokens(chatMessages);
        var toolCount = options?.Tools?.Count ?? 0;

        activity?.SetTag("client.id", _clientId);
        activity?.SetTag("account.id", _accountId.ToString());
        activity?.SetTag("call.id", callId);
        activity?.SetTag("input.message_count", messageCount.ToString());
        activity?.SetTag("input.estimated_tokens", estimatedInputTokens.ToString());
        activity?.SetTag("input.tool_count", toolCount.ToString());

        _logger.LogInformation(
            "[LLM Call Start] Client={ClientId} Account={AccountId} CallId={CallId} Messages={MessageCount} EstInputTokens={EstInputTokens} Tools={ToolCount}",
            _clientId, _accountId, callId, messageCount, estimatedInputTokens, toolCount);

        try
        {
            var response = await _innerClient.GetResponseAsync(chatMessages, options, cancellationToken);
            stopwatch.Stop();

            // Extract token usage from response
            var usage = response.Usage;
            var completionTokens = usage?.OutputTokenCount;
            var promptTokens = usage?.InputTokenCount;
            var totalTokens = usage?.TotalTokenCount;
            activity?.SetTag("usage.source", usage == null ? "unavailable" : "provider");

            // Log response details
            activity?.SetTag("output.completion_tokens", completionTokens.ToString());
            activity?.SetTag("output.prompt_tokens", promptTokens.ToString());
            activity?.SetTag("output.total_tokens", totalTokens.ToString());
            activity?.SetTag("response.text_length", response.Text?.Length.ToString() ?? "0");
            activity?.SetTag("response.finish_reason", response.FinishReason?.ToString() ?? "unknown");
            activity?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds.ToString());

            _logger.LogInformation(
                "[LLM Call Complete] Client={ClientId} Account={AccountId} CallId={CallId} " +
                "PromptTokens={PromptTokens} CompletionTokens={CompletionTokens} TotalTokens={TotalTokens} " +
                "ResponseLength={ResponseLength} Duration={DurationMs}ms FinishReason={FinishReason}",
                _clientId, _accountId, callId, promptTokens, completionTokens, totalTokens,
                response.Text?.Length ?? 0, stopwatch.ElapsedMilliseconds, response.FinishReason);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            _logger.LogError(ex,
                "[LLM Call Error] Client={ClientId} Account={AccountId} CallId={CallId} Duration={DurationMs}ms Error={ErrorMessage}",
                _clientId, _accountId, callId, stopwatch.ElapsedMilliseconds, ex.Message);

            throw;
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatMessages);

        using var activity = s_activitySource.StartActivity("StreamingChatCompletion");
        var stopwatch = Stopwatch.StartNew();
        var callId = Guid.NewGuid().ToString("N")[..12];

        // Log request details
        var messageCount = chatMessages.Count();
        var estimatedInputTokens = EstimateTokens(chatMessages);
        var toolCount = options?.Tools?.Count ?? 0;

        activity?.SetTag("client.id", _clientId);
        activity?.SetTag("account.id", _accountId.ToString());
        activity?.SetTag("call.id", callId);
        activity?.SetTag("input.message_count", messageCount.ToString());
        activity?.SetTag("input.estimated_tokens", estimatedInputTokens.ToString());
        activity?.SetTag("input.tool_count", toolCount.ToString());
        activity?.SetTag("streaming", "true");

        _logger.LogInformation(
            "[LLM Streaming Start] Client={ClientId} Account={AccountId} CallId={CallId} Messages={MessageCount} EstInputTokens={EstInputTokens} Tools={ToolCount}",
            _clientId, _accountId, callId, messageCount, estimatedInputTokens, toolCount);

        var totalStreamedText = 0;
        var chunkCount = 0;
        string? finishReason = null;

        UsageDetails? usage = null;
        var completed = false;
        await using var iterator = _innerClient.GetStreamingResponseAsync(chatMessages, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                bool next;
                try { next = await iterator.MoveNextAsync().ConfigureAwait(false); }
                catch (Exception ex)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
                    activity?.SetTag("error.type", ex.GetType().FullName);
                    throw;
                }
                if (!next) { completed = true; break; }
                var update = iterator.Current;
                chunkCount++;
                totalStreamedText += update.Text?.Length ?? 0;
                if (update.FinishReason != null) finishReason = update.FinishReason.ToString();
                foreach (var content in update.Contents.OfType<UsageContent>())
                {
                    usage ??= new UsageDetails();
                    usage.Add(content.Details);
                }
                yield return update;
            }
        }
        finally
        {
            stopwatch.Stop();
            activity?.SetTag("usage.source", usage == null ? "unavailable" : "provider");
            activity?.SetTag("output.completion_tokens", usage?.OutputTokenCount);
            activity?.SetTag("output.prompt_tokens", usage?.InputTokenCount);
            activity?.SetTag("output.total_tokens", usage?.TotalTokenCount);
            activity?.SetTag("output.estimated_tokens", EstimateTokens(totalStreamedText));
            activity?.SetTag("response.text_length", totalStreamedText);
            activity?.SetTag("response.chunk_count", chunkCount);
            activity?.SetTag("response.finish_reason", finishReason ?? "unknown");
            activity?.SetTag("response.completed", completed);
            activity?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds);
            if (!completed) activity?.SetStatus(ActivityStatusCode.Error, "Stream did not complete");
            _logger.LogInformation("[LLM Streaming End] Client={ClientId} Account={AccountId} CallId={CallId} Completed={Completed} PromptTokens={PromptTokens} CompletionTokens={CompletionTokens} Duration={DurationMs}ms",
                _clientId, _accountId, callId, completed, usage?.InputTokenCount, usage?.OutputTokenCount, stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Estimate token count based on text length (rough approximation: ~4 characters per token)
    /// </summary>
    private static int EstimateTokens(IEnumerable<ChatMessage> messages)
    {
        var totalLength = messages
            .Where(m => m.Text != null)
            .Sum(m => m.Text!.Length);

        return Math.Max(1, totalLength / 4);
    }

    /// <summary>
    /// Estimate token count for text
    /// </summary>
    private static int EstimateTokens(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return Math.Max(1, text.Length / 4);
    }

    /// <summary>
    /// Estimate token count for numeric text length
    /// </summary>
    private static int EstimateTokens(int textLength)
    {
        return Math.Max(1, textLength / 4);
    }
}
