using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Application.Services.Ai.Tools;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;


namespace FtoConsulting.PortfolioManager.Application.Services.Ai;

/// <summary>
/// Implementation of market intelligence service for external market data
/// This is a basic implementation that will be enhanced with real APIs later
/// </summary>
public class MarketIntelligenceService : IMarketIntelligenceService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MarketIntelligenceService> _logger;
    private readonly IAiChatService? _aiChatService;
    private readonly IMcpServerService? _mcpServerService;
    private readonly Func<EodMarketDataTool>? _eodMarketDataToolFactory;

    public MarketIntelligenceService(
        HttpClient httpClient,
        ILogger<MarketIntelligenceService> logger,
        IAiChatService? aiChatService = null,
        IMcpServerService? mcpServerService = null,
        Func<EodMarketDataTool>? eodMarketDataToolFactory = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _aiChatService = aiChatService;
        _mcpServerService = mcpServerService;
        _eodMarketDataToolFactory = eodMarketDataToolFactory;
    }

    public async Task<MarketContextDto> GetMarketContextAsync(IEnumerable<string> tickers, DateTime date, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting market context for tickers: {Tickers} on {Date}", 
                string.Join(", ", tickers), date);

            // Try to get real data from EOD
            var news = await GetNewsAsync(tickers, date);
            var sentiment = await GetSentimentAsync(date, tickers);

            // Generate AI-powered market summary
            var marketSummary = await GenerateMarketSummaryAsync(tickers, news, sentiment, Array.Empty<MarketIndexDto>(), cancellationToken);

            return new MarketContextDto(
                Tickers: tickers,
                Date: date,
                MarketSummary: marketSummary,
                RelevantNews: news,
                Sentiment: sentiment,
                Indices: Array.Empty<MarketIndexDto>()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting market context for tickers: {Tickers}", string.Join(", ", tickers));
            throw;
        }
    }

    public async Task<MarketSentimentDto> GetMarketSentimentAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting market sentiment for {Date}", date);

            // Use default market tickers for UK/European and US sentiment analysis
            var defaultTickers = new[] { 
                // US Market Indices
                "SPY.US", "QQQ.US", "IWM.US", "VIX.US",
                // UK Market Indices  
                "UKX.INDX", "FTSE.INDX", "ISF.LSE", // FTSE 100, FTSE All-Share, iShares Core FTSE 100
                // European Market Indices
                "SX5E.INDX", "DAX.INDX", "CAC.INDX", "AEX.INDX", // STOXX 50, DAX, CAC 40, AEX
                // Major UK/European Stocks
                "HSBA.LSE", "BP.LSE", "SHEL.LSE", "AZN.LSE", // HSBC, BP, Shell, AstraZeneca
                "ASML.AS", "NESN.SW", "SAP.DE" // ASML (Netherlands), Nestle (Switzerland), SAP (Germany)
            };
            var sentiment = await GetSentimentAsync(date, defaultTickers);
            return sentiment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting market sentiment for {Date}", date);
            throw;
        }
    }

    /// <summary>
    /// Generate an intelligent market summary using Azure OpenAI based on market context
    /// </summary>
    /// <param name="tickers">Portfolio tickers to analyze</param>
    /// <param name="news">Relevant news items</param>
    /// <param name="sentiment">Market sentiment data</param>
    /// <param name="indices">Market indices performance</param>
    /// <param name="wordWeights">News word weights and trending topics (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI-generated market summary</returns>
    public async Task<string> GenerateMarketSummaryAsync(
        IEnumerable<string> tickers,
        IEnumerable<NewsItemDto> news,
        MarketSentimentDto sentiment,
        IEnumerable<MarketIndexDto> indices,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating AI market summary for tickers: {Tickers}", string.Join(", ", tickers));

            
            // Check if AI chat service is available
            if (_aiChatService == null)
            {
                _logger.LogError("AI chat service not configured");
                throw new InvalidOperationException("Market summary generation unavailable: AI chat service not configured");
            }

            // Construct the prompt with market context
            var prompt = BuildMarketSummaryPrompt(tickers, news, sentiment, indices);

            // Call AI service
            var summary = await _aiChatService.CompleteChatAsync(
                new ChatMessage[]
                {
                    new SystemChatMessage("You are a financial analyst AI assistant that provides accurate, useful market insights for portfolio management."),
                    new UserChatMessage(prompt)
                },
                cancellationToken);
            
            _logger.LogInformation("Successfully generated AI market summary with {Characters} characters", summary.Length);
            
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI market summary for tickers: {Tickers}", string.Join(", ", tickers));
            throw new InvalidOperationException($"Failed to generate market summary: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Build a structured prompt for market summary generation
    /// </summary>
    private string BuildMarketSummaryPrompt(
        IEnumerable<string> tickers,
        IEnumerable<NewsItemDto> news,
        MarketSentimentDto sentiment,
        IEnumerable<MarketIndexDto> indices)
    {
        var tickerList = string.Join(", ", tickers);
        var newsItems = news.Take(5); // Limit to top 5 news items to avoid token limits
        var topIndices = indices.Take(3); // Top 3 indices

        var prompt = $@"Analyze the current market conditions and provide a summary for a portfolio containing these holdings: {tickerList}

MARKET SENTIMENT DATA:
- Overall Sentiment: {sentiment.SentimentLabel} (Score: {sentiment.OverallSentimentScore:F2})
- Fear & Greed Index: {sentiment.FearGreedIndex}
- Key Indicators: {string.Join(", ", sentiment.Indicators.Select(i => $"{i.Name}: {i.Value} ({i.Trend})"))}

SECTOR SENTIMENT:
{string.Join("\n", sentiment.SectorSentiments.Select(s => $"- {s.SectorName}: {s.SentimentScore:F2} ({s.Trend}) - Key factors: {string.Join(", ", s.KeyFactors)}"))}

RECENT NEWS:
{string.Join("\n", newsItems.Select(n => $"- [{n.Title}]({n.Url}) (Sentiment: {n.SentimentScore:F2}) - {n.Summary}"))}

CRITICAL FORMATTING REQUIREMENT:
When you mention ANY news story in your summary, you MUST include it as a clickable markdown link using this exact format: [Story Title](URL)
For example: Recent reports indicate [HSBC faces regulatory scrutiny in Switzerland](https://example.com/article)

Please provide a useful market summary that:
1. Explains the current market environment in context of the portfolio holdings
2. Highlights key opportunities or risks for these specific tickers
3. Gives a forward-looking perspective based on the sentiment and news analysis
4. MUST include clickable markdown links for every news story you reference
5. If you were not able to find useful information, state that clearly

Keep the response professional, accurate, and useful for portfolio management decisions.";

        return prompt;
    }

    /// <summary>
    /// Get news data from EOD - returns empty collection if unavailable
    /// </summary>
    private async Task<IEnumerable<NewsItemDto>> GetNewsAsync(IEnumerable<string> tickers, DateTime date)
    {
        if (_eodMarketDataToolFactory == null)
        {
            _logger.LogWarning("EOD market data tool not configured, returning empty news");
            return Array.Empty<NewsItemDto>();
        }

        try
        {
            var eodMarketDataTool = _eodMarketDataToolFactory();
            var realNews = await eodMarketDataTool.GetFinancialNewsAsync(_mcpServerService, tickers, date.AddDays(-7), date);
            if (realNews.Any())
            {
                _logger.LogInformation("Retrieved {Count} news items from EOD", realNews.Count());
                return realNews;
            }
            else
            {
                _logger.LogWarning("No news data returned from EOD for tickers: {Tickers}", string.Join(", ", tickers));
                return Array.Empty<NewsItemDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve news data from EOD (possibly rate limited), returning empty news");
            return Array.Empty<NewsItemDto>();
        }
    }

    /// <summary>
    /// Get sentiment data from EOD - throws exception if unavailable
    /// </summary>
    private async Task<MarketSentimentDto> GetSentimentAsync(DateTime date, IEnumerable<string> tickers)
    {
        if (_eodMarketDataToolFactory == null)
        {
            _logger.LogWarning("EOD market data tool not configured, using neutral sentiment");
            
return CreateFallbackSentiment();
        }

        try
        {
            var eodMarketDataTool = _eodMarketDataToolFactory();
            var realSentiment = await eodMarketDataTool.GetMarketSentimentAsync(tickers, date);
            if (realSentiment != null)
            {
                _logger.LogInformation("Retrieved sentiment data from EOD");
                return realSentiment;
            }
            else
            {
                _logger.LogWarning("No sentiment data returned from EOD for date: {Date}, using fallback", date);
                
return CreateFallbackSentiment();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve sentiment data from EOD (possibly rate limited), using fallback");
            
return CreateFallbackSentiment();
        }
    }

    /// <summary>
    /// Create a fallback sentiment when EOD data is unavailable
    /// </summary>
    private MarketSentimentDto CreateFallbackSentiment()
    {
        return new MarketSentimentDto(
            Date: DateTime.Now,
            OverallSentimentScore: 0.5m,
            SentimentLabel: "Neutral - Market data source not configured",
            FearGreedIndex: 50,
            SectorSentiments: Array.Empty<SectorSentimentDto>(),
            Indicators: new MarketIndicatorDto[]
            {
                new("Configuration", 0.5m, "Required", "Please configure EOD Historical Data API for real market sentiment", DateTime.Now)
            }
        );
    }
}
