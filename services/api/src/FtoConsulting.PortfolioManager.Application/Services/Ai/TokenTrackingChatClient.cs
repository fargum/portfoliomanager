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
            var completionTokens = usage?.OutputTokenCount ?? 0;
            var promptTokens = usage?.InputTokenCount ?? estimatedInputTokens;
            var totalTokens = usage?.TotalTokenCount ?? (promptTokens + completionTokens);

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

        await foreach (var update in _innerClient.GetStreamingResponseAsync(chatMessages, options, cancellationToken))
        {
            chunkCount++;
            totalStreamedText += update.Text?.Length ?? 0;
            
            // Capture finish reason when available
            if (update.FinishReason != null)
            {
                finishReason = update.FinishReason.ToString();
            }

            yield return update;
        }

        // Log completion (this runs after the enumeration is complete)
        stopwatch.Stop();

        // Log completion details
        var completionTokens = EstimateTokens(totalStreamedText);
        var promptTokens = estimatedInputTokens;
        var totalTokens = promptTokens + completionTokens;

        activity?.SetTag("output.completion_tokens", completionTokens.ToString());
        activity?.SetTag("output.prompt_tokens", promptTokens.ToString());
        activity?.SetTag("output.total_tokens", totalTokens.ToString());
        activity?.SetTag("response.text_length", totalStreamedText.ToString());
        activity?.SetTag("response.chunk_count", chunkCount.ToString());
        activity?.SetTag("response.finish_reason", finishReason ?? "unknown");
        activity?.SetTag("duration.ms", stopwatch.ElapsedMilliseconds.ToString());

        _logger.LogInformation(
            "[LLM Streaming Complete] Client={ClientId} Account={AccountId} CallId={CallId} " +
            "PromptTokens={PromptTokens} CompletionTokens={CompletionTokens} TotalTokens={TotalTokens} " +
            "StreamedLength={StreamedLength} Chunks={ChunkCount} Duration={DurationMs}ms FinishReason={FinishReason}",
            _clientId, _accountId, callId, promptTokens, completionTokens, totalTokens,
            totalStreamedText, chunkCount, stopwatch.ElapsedMilliseconds, finishReason);
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