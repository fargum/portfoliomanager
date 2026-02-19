using OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.AI;
using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;


namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>
/// Azure AI Foundry implementation of AI chat service (used for memory extraction and market intelligence)
/// </summary>
public class AzureOpenAiChatService(
    OpenAIClient openAiClient,
    IOptions<AzureFoundryOptions> azureFoundryOptions,
    ILogger<AzureOpenAiChatService> logger) : IAiChatService
{

    public async Task<string> CompleteChatAsync(OpenAI.Chat.ChatMessage[] messages, CancellationToken cancellationToken = default)
    {
        try
        {
            // Convert OpenAI.Chat.ChatMessage[] to Microsoft.Extensions.AI.ChatMessage[]
            var aiMessages = messages.Select(m => {
                var role = m switch
                {
                    SystemChatMessage _ => Microsoft.Extensions.AI.ChatRole.System,
                    UserChatMessage _ => Microsoft.Extensions.AI.ChatRole.User,
                    AssistantChatMessage _ => Microsoft.Extensions.AI.ChatRole.Assistant,
                    _ => Microsoft.Extensions.AI.ChatRole.User
                };

                // Extract text content from the message
                var content = m switch
                {
                    SystemChatMessage sys => sys.Content?.FirstOrDefault()?.Text ?? "",
                    UserChatMessage user => user.Content?.FirstOrDefault()?.Text ?? "",
                    AssistantChatMessage assistant => assistant.Content?.FirstOrDefault()?.Text ?? "",
                    _ => ""
                };

                return new Microsoft.Extensions.AI.ChatMessage(role, content);
            });

            // Use instrumented chat client for telemetry (uses default model for memory/intelligence tasks)
            var instrumentedChatClient = openAiClient
                .GetChatClient(azureFoundryOptions.Value.ModelName)
                .AsIChatClient()
                .AsBuilder()
                .UseOpenTelemetry(sourceName: "PortfolioManager.AI.DirectChat", 
                                 configure: cfg => cfg.EnableSensitiveData = true)
                .Build();

            var response = await instrumentedChatClient.GetResponseAsync(aiMessages, cancellationToken: cancellationToken);
            return response.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error completing chat with Azure OpenAI");
            throw;
        }
    }
}
