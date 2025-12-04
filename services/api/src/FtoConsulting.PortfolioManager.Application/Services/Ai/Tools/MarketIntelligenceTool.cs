using System.ComponentModel;
using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Application.Utilities;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;


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
        [Description("Date for market analysis in various formats (YYYY-MM-DD, DD/MM/YYYY, DD MMMM YYYY, etc.)")] string date,
        CancellationToken cancellationToken = default)
    {
        if (_marketIntelligenceServiceFactory == null)
        {
            return new { Error = "Market intelligence service not available" };
        }

        var marketIntelligenceService = _marketIntelligenceServiceFactory();
        var parsedDate = DateUtilities.ParseDateTime(date);
        var context = await marketIntelligenceService.GetMarketContextAsync(tickers, parsedDate, cancellationToken);
        
        return new
        {
            Tickers = tickers,
            Date = date,
            Context = context
        };
    }

    [Description("Get overall market sentiment and indicators for a specific date")]
    public async Task<object> GetMarketSentiment(
        [Description("Date for sentiment analysis in various formats (YYYY-MM-DD, DD/MM/YYYY, DD MMMM YYYY, etc.)")] string date,
        CancellationToken cancellationToken = default)
    {
        if (_marketIntelligenceServiceFactory == null)
        {
            return new { Error = "Market intelligence service not available" };
        }

        var marketIntelligenceService = _marketIntelligenceServiceFactory();
        var parsedDate = DateUtilities.ParseDateTime(date);
        var sentiment = await marketIntelligenceService.GetMarketSentimentAsync(parsedDate, cancellationToken);
        
        return new
        {
            Date = date,
            Sentiment = sentiment
        };
    }
}
