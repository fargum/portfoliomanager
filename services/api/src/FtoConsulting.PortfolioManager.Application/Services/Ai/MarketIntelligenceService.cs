using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Services.Ai.Tools;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

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
            var indices = await GetIndicesAsync(date);

            // Generate AI-powered market summary
            var marketSummary = await GenerateMarketSummaryAsync(tickers, news, sentiment, indices, cancellationToken);

            return new MarketContextDto(
                Tickers: tickers,
                Date: date,
                MarketSummary: marketSummary,
                RelevantNews: news,
                Sentiment: sentiment,
                Indices: indices
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting market context for tickers: {Tickers}", string.Join(", ", tickers));
            throw;
        }
    }

    public async Task<IEnumerable<NewsItemDto>> SearchFinancialNewsAsync(IEnumerable<string> tickers, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Searching financial news for {Tickers} from {FromDate} to {ToDate}", 
                string.Join(", ", tickers), fromDate, toDate);

            // Call the internal method that uses EOD service
            return await GetNewsAsync(tickers, toDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching financial news for tickers: {Tickers}", string.Join(", ", tickers));
            throw;
        }
    }

    public async Task<MarketSentimentDto> GetMarketSentimentAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting market sentiment for {Date}", date);

            // Use the internal method that calls EOD service
            var sentiment = await GetSentimentAsync(date, Array.Empty<string>());
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
                    new SystemChatMessage("You are a financial analyst AI assistant that provides concise, actionable market insights for portfolio management."),
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

        var prompt = $@"Analyze the current market conditions and provide a concise summary for a portfolio containing these holdings: {tickerList}

MARKET SENTIMENT DATA:
- Overall Sentiment: {sentiment.SentimentLabel} (Score: {sentiment.OverallSentimentScore:F2})
- Fear & Greed Index: {sentiment.FearGreedIndex}
- Key Indicators: {string.Join(", ", sentiment.Indicators.Select(i => $"{i.Name}: {i.Value} ({i.Trend})"))}

SECTOR SENTIMENT:
{string.Join("\n", sentiment.SectorSentiments.Select(s => $"- {s.SectorName}: {s.SentimentScore:F2} ({s.Trend}) - Key factors: {string.Join(", ", s.KeyFactors)}"))}

MARKET INDICES:
{string.Join("\n", topIndices.Select(idx => $"- {idx.Name}: {idx.CurrentValue:F2} ({idx.DayChangePercentage:+0.00%;-0.00%}%)"))}

RECENT NEWS:
{string.Join("\n", newsItems.Select(n => $"- {n.Title} (Sentiment: {n.SentimentScore:F2}) - {n.Summary}"))}

Please provide a 2-3 sentence market summary that:
1. Explains the current market environment in context of the portfolio holdings
2. Highlights key opportunities or risks for these specific tickers
3. Gives a forward-looking perspective based on the sentiment and news analysis

Keep the response professional, concise, and actionable for portfolio management decisions.";

        return prompt;
    }

    /// <summary>
    /// Get news data from EOD - throws exception if unavailable
    /// </summary>
    private async Task<IEnumerable<NewsItemDto>> GetNewsAsync(IEnumerable<string> tickers, DateTime date)
    {
        if (_eodMarketDataToolFactory == null)
        {
            _logger.LogError("EOD market data tool not configured");
            throw new InvalidOperationException("News data unavailable: EOD market data service not configured");
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
                throw new InvalidOperationException($"No news data available for tickers: {string.Join(", ", tickers)}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve news data from EOD");
            throw new InvalidOperationException($"News data retrieval failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get sentiment data from EOD - throws exception if unavailable
    /// </summary>
    private async Task<MarketSentimentDto> GetSentimentAsync(DateTime date, IEnumerable<string> tickers)
    {
        if (_eodMarketDataToolFactory == null)
        {
            _logger.LogError("EOD market data tool not configured");
            throw new InvalidOperationException("Sentiment data unavailable: EOD market data service not configured");
        }

        try
        {
            var eodMarketDataTool = _eodMarketDataToolFactory();
            var realSentiment = await eodMarketDataTool.GetMarketSentimentAsync(_mcpServerService, tickers, date);
            if (realSentiment != null)
            {
                _logger.LogInformation("Retrieved sentiment data from EOD");
                return realSentiment;
            }
            else
            {
                _logger.LogWarning("No sentiment data returned from EOD for date: {Date}", date);
                throw new InvalidOperationException($"No sentiment data available for date: {date:yyyy-MM-dd}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve sentiment data from EOD");
            throw new InvalidOperationException($"Sentiment data retrieval failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get indices data from EOD - throws exception if unavailable
    /// </summary>
    private async Task<IEnumerable<MarketIndexDto>> GetIndicesAsync(DateTime date)
    {
        if (_eodMarketDataToolFactory == null)
        {
            _logger.LogError("EOD market data tool not configured");
            throw new InvalidOperationException("Market indices data unavailable: EOD market data service not configured");
        }

        try
        {
            var eodMarketDataTool = _eodMarketDataToolFactory();
            var indexSymbols = new[] { "SPY", "QQQ", "DIA" }; // Major ETFs representing indices
            var realIndices = await eodMarketDataTool.GetMarketIndicesAsync(_mcpServerService, indexSymbols, date);
            if (realIndices.Any())
            {
                _logger.LogInformation("Retrieved {Count} indices from EOD", realIndices.Count());
                return realIndices;
            }
            else
            {
                _logger.LogWarning("No indices data returned from EOD for date: {Date}", date);
                throw new InvalidOperationException($"No market indices data available for date: {date:yyyy-MM-dd}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve indices data from EOD");
            throw new InvalidOperationException($"Market indices data retrieval failed: {ex.Message}", ex);
        }
    }
}