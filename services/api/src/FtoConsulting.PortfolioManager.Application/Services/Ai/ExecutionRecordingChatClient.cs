using System.Runtime.CompilerServices;
using System.Text.Json;
using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using Microsoft.Extensions.AI;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>Inside function invocation, so every model round includes its actual input and output.</summary>
public sealed class ExecutionRecordingChatClient(IChatClient innerClient, AgentExecutionCapture capture) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var input = messages.ToList();
        var round = capture.BeginRound(input, options);
        try
        {
            var response = await base.GetResponseAsync(input, options, cancellationToken).ConfigureAwait(false);
            Complete(round, response);
            return response;
        }
        catch (Exception ex) { round.ErrorType = ex.GetType().FullName; throw; }
        finally { round.CompletedAt = DateTimeOffset.UtcNow; }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var input = messages.ToList();
        var round = capture.BeginRound(input, options);
        var updates = new List<ChatResponseUpdate>();
        await using var iterator = base.GetStreamingResponseAsync(input, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                bool next;
                try { next = await iterator.MoveNextAsync().ConfigureAwait(false); }
                catch (Exception ex) { round.ErrorType = ex.GetType().FullName; throw; }
                if (!next) break;
                // Retain values, not provider-owned mutable objects. Capture failures never change model execution.
                try
                {
                    updates.Add(JsonSerializer.Deserialize<ChatResponseUpdate>(JsonSerializer.SerializeToUtf8Bytes(iterator.Current))!);
                }
                catch (Exception ex) { capture.RecordCaptureError(ex); }
                yield return iterator.Current;
            }
        }
        finally
        {
            try { Complete(round, updates.ToChatResponse()); }
            catch (Exception ex) { capture.RecordCaptureError(ex); }
            round.CompletedAt = DateTimeOffset.UtcNow;
        }
    }

    private void Complete(ModelRoundSnapshot round, ChatResponse response)
    {
        round.ResponseId = response.ResponseId;
        round.FinishReason = response.FinishReason?.Value;
        round.InputTokens = response.Usage?.InputTokenCount;
        round.OutputTokens = response.Usage?.OutputTokenCount;
        round.Output = capture.Messages(response.Messages);
    }
}
