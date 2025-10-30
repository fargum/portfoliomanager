using System.ComponentModel;
using FtoConsulting.PortfolioManager.Application.Services.Ai;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai.Tools;

/// <summary>
/// MCP tool for market intelligence
/// </summary>
public class MarketIntelligenceTool
{
    private readonly Func<IMarketIntelligenceService>? _marketIntelligenceServiceFactory;

    public MarketIntelligenceTool(Func<IMarketIntelligenceService>? marketIntelligenceServiceFactory = null)
    {
        _marketIntelligenceServiceFactory = marketIntelligenceServiceFactory;
    }

    [Description("Get market context and news for specific stock tickers")]
    public async Task<object> GetMarketContext(
        [Description("List of stock tickers")] string[] tickers,
        [Description("Date for market analysis in YYYY-MM-DD format")] string date,
        CancellationToken cancellationToken = default)
    {
        if (_marketIntelligenceServiceFactory == null)
        {
            return new { Error = "Market intelligence service not available" };
        }

        var marketIntelligenceService = _marketIntelligenceServiceFactory();
        var parsedDate = DateTime.Parse(date);
        var context = await marketIntelligenceService.GetMarketContextAsync(tickers, parsedDate, cancellationToken);
        
        return new
        {
            Tickers = tickers,
            Date = date,
            Context = context
        };
    }

    [Description("Search for financial news related to specific tickers within a date range")]
    public async Task<object> SearchFinancialNews(
        [Description("List of stock tickers")] string[] tickers,
        [Description("Start date in YYYY-MM-DD format")] string fromDate,
        [Description("End date in YYYY-MM-DD format")] string toDate,
        CancellationToken cancellationToken = default)
    {
        if (_marketIntelligenceServiceFactory == null)
        {
            return new { Error = "Market intelligence service not available" };
        }

        var marketIntelligenceService = _marketIntelligenceServiceFactory();
        var from = DateTime.Parse(fromDate);
        var to = DateTime.Parse(toDate);
        var news = await marketIntelligenceService.SearchFinancialNewsAsync(tickers, from, to, cancellationToken);
        
        return new
        {
            Tickers = tickers,
            FromDate = fromDate,
            ToDate = toDate,
            News = news
        };
    }

    [Description("Get overall market sentiment and indicators for a specific date")]
    public async Task<object> GetMarketSentiment(
        [Description("Date for sentiment analysis in YYYY-MM-DD format")] string date,
        CancellationToken cancellationToken = default)
    {
        if (_marketIntelligenceServiceFactory == null)
        {
            return new { Error = "Market intelligence service not available" };
        }

        var marketIntelligenceService = _marketIntelligenceServiceFactory();
        var parsedDate = DateTime.Parse(date);
        var sentiment = await marketIntelligenceService.GetMarketSentimentAsync(parsedDate, cancellationToken);
        
        return new
        {
            Date = date,
            Sentiment = sentiment
        };
    }
}