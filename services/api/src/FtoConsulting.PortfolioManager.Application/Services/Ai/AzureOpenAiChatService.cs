using Azure.AI.OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.Logging;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>
/// Azure OpenAI implementation of AI chat service
/// </summary>
public class AzureOpenAiChatService : IAiChatService
{
    private readonly AzureOpenAIClient _azureOpenAIClient;
    private readonly ILogger<AzureOpenAiChatService> _logger;

    public AzureOpenAiChatService(
        AzureOpenAIClient azureOpenAIClient,
        ILogger<AzureOpenAiChatService> logger)
    {
        _azureOpenAIClient = azureOpenAIClient;
        _logger = logger;
    }

    public async Task<string> CompleteChatAsync(ChatMessage[] messages, CancellationToken cancellationToken = default)
    {
        try
        {
            var chatClient = _azureOpenAIClient.GetChatClient("gpt-4o-mini");
            var response = await chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
            return response.Value.Content[0].Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing chat with Azure OpenAI");
            throw;
        }
    }
}