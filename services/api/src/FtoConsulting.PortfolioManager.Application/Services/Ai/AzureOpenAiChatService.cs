using Azure.AI.OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;


namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>
/// Azure OpenAI implementation of AI chat service
/// </summary>
public class AzureOpenAiChatService : IAiChatService
{
    private readonly AzureOpenAIClient _azureOpenAIClient;
    private readonly AzureFoundryOptions _azureFoundryOptions;
    private readonly ILogger<AzureOpenAiChatService> _logger;

    public AzureOpenAiChatService(
        AzureOpenAIClient azureOpenAIClient,
        IOptions<AzureFoundryOptions> azureFoundryOptions,
        ILogger<AzureOpenAiChatService> logger)
    {
        _azureOpenAIClient = azureOpenAIClient;
        _azureFoundryOptions = azureFoundryOptions.Value;
        _logger = logger;
    }

    public async Task<string> CompleteChatAsync(ChatMessage[] messages, CancellationToken cancellationToken = default)
    {
        try
        {
            var chatClient = _azureOpenAIClient.GetChatClient(_azureFoundryOptions.ModelName);
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
