using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using FtoConsulting.PortfolioManager.Application.Services.Ai;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using OpenAI.Chat;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;

namespace FtoConsulting.PortfolioManager.Application.Tests.Services;

public class MarketIntelligenceServiceTests
{
    private readonly Mock<HttpClient> _mockHttpClient;
    private readonly Mock<ILogger<MarketIntelligenceService>> _mockLogger;
    private readonly Mock<IAiChatService> _mockAiChatService;
    private readonly Mock<IMcpServerService> _mockMcpServerService;
    private readonly MarketIntelligenceService _service;

    public MarketIntelligenceServiceTests()
    {
        _mockHttpClient = new Mock<HttpClient>();
        _mockLogger = new Mock<ILogger<MarketIntelligenceService>>();
        _mockAiChatService = new Mock<IAiChatService>();
        _mockMcpServerService = new Mock<IMcpServerService>();

        _service = new MarketIntelligenceService(
            _mockHttpClient.Object,
            _mockLogger.Object,
            _mockAiChatService.Object,
            _mockMcpServerService.Object,
            null); // No EOD factory for these tests
    }

    [Fact]
    public async Task GenerateMarketSummaryAsync_WithValidInput_ReturnsAiGeneratedSummary()
    {
        // Arrange
        var tickers = new[] { "AAPL", "MSFT" };
        var news = new[]
        {
            new NewsItemDto(
                Title: "Apple Reports Strong Earnings",
                Summary: "Apple shows strong quarterly performance",
                Source: "Reuters",
                PublishedDate: DateTime.Now,
                Url: "https://example.com/apple-earnings",
                RelatedTickers: new[] { "AAPL" },
                SentimentScore: 0.8m,
                Category: "Earnings"
            )
        };
        var sentiment = new MarketSentimentDto(
            Date: DateTime.Now,
            OverallSentimentScore: 0.7m,
            SentimentLabel: "Positive",
            FearGreedIndex: 75m,
            SectorSentiments: new[]
            {
                new SectorSentimentDto("Technology", 0.8m, "Bullish", new[] { "Strong earnings" })
            },
            Indicators: new[]
            {
                new MarketIndicatorDto("VIX", 15m, "Low", "Low volatility", DateTime.Now)
            }
        );
        var indices = new[]
        {
            new MarketIndexDto("S&P 500", "SPX", 4500m, 25m, 0.56m, DateTime.Now)
        };

        var expectedResponse = "The market shows positive sentiment with technology stocks leading gains. Your portfolio holdings are well-positioned for continued growth.";
        
        _mockAiChatService.Setup(x => x.CompleteChatAsync(
            It.IsAny<ChatMessage[]>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.GenerateMarketSummaryAsync(tickers, news, sentiment, indices);

        // Assert
        Assert.Equal(expectedResponse, result);
        _mockAiChatService.Verify(x => x.CompleteChatAsync(
            It.IsAny<ChatMessage[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateMarketSummaryAsync_WithNullAiService_ThrowsInvalidOperationException()
    {
        // Arrange
        var serviceWithNullAi = new MarketIntelligenceService(
            _mockHttpClient.Object,
            _mockLogger.Object,
            null); // Null AI service

        var tickers = new[] { "AAPL", "MSFT" };
        var news = Array.Empty<NewsItemDto>();
        var sentiment = new MarketSentimentDto(
            Date: DateTime.Now,
            OverallSentimentScore: 0.6m,
            SentimentLabel: "Moderately Positive",
            FearGreedIndex: 65m,
            SectorSentiments: new[]
            {
                new SectorSentimentDto("Technology", 0.7m, "Positive", new[] { "Growth" })
            },
            Indicators: Array.Empty<MarketIndicatorDto>()
        );
        var indices = Array.Empty<MarketIndexDto>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => serviceWithNullAi.GenerateMarketSummaryAsync(tickers, news, sentiment, indices));
        
        Assert.Contains("Market summary generation unavailable: AI chat service not configured", exception.Message);
    }

    [Fact]
    public async Task GenerateMarketSummaryAsync_WhenAiServiceThrows_ThrowsInvalidOperationException()
    {
        // Arrange
        var tickers = new[] { "AAPL" };
        var news = Array.Empty<NewsItemDto>();
        var sentiment = new MarketSentimentDto(
            Date: DateTime.Now,
            OverallSentimentScore: 0.5m,
            SentimentLabel: "Neutral",
            FearGreedIndex: 50m,
            SectorSentiments: new[]
            {
                new SectorSentimentDto("Technology", 0.5m, "Neutral", new[] { "Mixed signals" })
            },
            Indicators: Array.Empty<MarketIndicatorDto>()
        );
        var indices = Array.Empty<MarketIndexDto>();

        _mockAiChatService.Setup(x => x.CompleteChatAsync(
            It.IsAny<ChatMessage[]>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("AI service unavailable"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GenerateMarketSummaryAsync(tickers, news, sentiment, indices));
        
        Assert.Contains("Failed to generate market summary: AI service unavailable", exception.Message);
    }

    [Fact]
    public async Task GetMarketContextAsync_WithoutEodService_ThrowsInvalidOperationException()
    {
        // Arrange
        var tickers = new[] { "AAPL", "MSFT" };
        var date = DateTime.Now;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GetMarketContextAsync(tickers, date));
        
        Assert.Contains("Failed to generate market summary", exception.Message);
    }

    [Fact]
    public async Task GetMarketSentimentAsync_WithoutEodService_ReturnsFallbackSentiment()
    {
        // Arrange
        var date = DateTime.Now;

        // Act
        var result = await _service.GetMarketSentimentAsync(date);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("Neutral (EOD unavailable)", result.SentimentLabel);
        Assert.Equal(0.5m, result.OverallSentimentScore);
        Assert.Equal(50, result.FearGreedIndex);
    }
}
