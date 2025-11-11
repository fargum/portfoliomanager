using FtoConsulting.PortfolioManager.Application.Services.Ai;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FtoConsulting.PortfolioManager.Application.Tests.Services.Ai;

public class AgentPromptServiceTests
{
    private readonly Mock<ILogger<AgentPromptService>> _loggerMock;
    private readonly AgentPromptService _service;

    public AgentPromptServiceTests()
    {
        _loggerMock = new Mock<ILogger<AgentPromptService>>();
        _service = new AgentPromptService(_loggerMock.Object);
    }

    [Fact]
    public void GetPortfolioAdvisorPrompt_WithValidAccountId_ReturnsPromptWithAccountId()
    {
        // Arrange
        const int accountId = 123;

        // Act
        var result = _service.GetPortfolioAdvisorPrompt(accountId);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains("Account ID 123", result);
        Assert.Contains("financial advisor", result);
    }

    [Fact]
    public void GetPortfolioAdvisorPrompt_ContainsExpectedSections()
    {
        // Arrange
        const int accountId = 456;

        // Act
        var result = _service.GetPortfolioAdvisorPrompt(accountId);

        // Assert
        Assert.Contains("WHEN TO USE YOUR TOOLS", result);
        Assert.Contains("COMMUNICATION STYLE", result);
        Assert.Contains("FORMATTING THAT FEELS NATURAL", result);
        Assert.Contains("REMEMBER:", result);
        Assert.Contains("£", result); // Should mention UK currency formatting
    }

    [Fact] 
    public void GetPrompt_WithPortfolioAdvisorAndAccountId_ReturnsSameAsSpecificMethod()
    {
        // Arrange
        const int accountId = 789;
        var parameters = new Dictionary<string, object> { ["accountId"] = accountId };

        // Act
        var genericResult = _service.GetPrompt("PortfolioAdvisor", parameters);
        var specificResult = _service.GetPortfolioAdvisorPrompt(accountId);

        // Assert
        Assert.Equal(specificResult, genericResult);
    }

    [Fact]
    public void GetPrompt_WithUnknownPromptName_ReturnsFallback()
    {
        // Act
        var result = _service.GetPrompt("UnknownPrompt");

        // Assert
        Assert.Equal("You are a helpful AI assistant.", result);
    }
}