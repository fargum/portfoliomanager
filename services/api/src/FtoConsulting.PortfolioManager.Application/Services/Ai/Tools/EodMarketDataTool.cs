using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai.Tools;

/// <summary>
/// MCP tool for fetching real market data from EOD Historical Data MCP server
/// Uses the same EOD API configuration as the pricing service for consistency
/// </summary>
public class EodMarketDataTool
{
    private readonly ILogger<EodMarketDataTool> _logger;
    private readonly EodApiOptions _eodApiOptions;

    public EodMarketDataTool(
        ILogger<EodMarketDataTool> logger,
        IOptions<EodApiOptions> eodApiOptions)
    {
        _logger = logger;
        _eodApiOptions = eodApiOptions.Value;
    }

    /// <summary>
    /// Get real financial news from EOD Historical Data
    /// </summary>
    /// <param name="mcpServerService">MCP server service for making external calls</param>
    /// <param name="tickers">Stock tickers to get news for</param>
    /// <param name="fromDate">Start date for news search</param>
    /// <param name="toDate">End date for news search</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Financial news items</returns>
    public async Task<IEnumerable<NewsItemDto>> GetFinancialNewsAsync(
        IMcpServerService? mcpServerService,
        IEnumerable<string> tickers,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching financial news from EOD for tickers: {Tickers}", string.Join(", ", tickers));

            if (mcpServerService == null)
            {
                _logger.LogWarning("MCP server service not available, returning empty news");
                return Array.Empty<NewsItemDto>();
            }

            var parameters = new Dictionary<string, object>
            {
                ["ticker"] = string.Join(",", tickers), // Use ticker parameter name from schema
                ["start_date"] = fromDate.ToString("yyyy-MM-dd"), // Use start_date parameter name
                ["end_date"] = toDate.ToString("yyyy-MM-dd"), // Use end_date parameter name  
                ["limit"] = 50
            };

            var result = await mcpServerService.CallMcpToolAsync(
                _eodApiOptions.McpServerUrl, 
                "get_company_news", 
                parameters, 
                cancellationToken);

            return ParseNewsResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching financial news from EOD for tickers: {Tickers}", string.Join(", ", tickers));
            return Array.Empty<NewsItemDto>();
        }
    }

    /// <summary>
    /// Get real market indices data from EOD Historical Data
    /// NOTE: DISABLED - Requires upgraded EOD plan for live price data
    /// </summary>
    /// <param name="mcpServerService">MCP server service for making external calls</param>
    /// <param name="indices">Index symbols (e.g., SPY, QQQ, DIA)</param>
    /// <param name="date">Date for market data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Market indices data</returns>
    public async Task<IEnumerable<MarketIndexDto>> GetMarketIndicesAsync(
        IMcpServerService? mcpServerService,
        IEnumerable<string> indices,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        // DISABLED: get_live_price_data requires upgraded EOD plan
        _logger.LogInformation("Market indices data disabled - requires upgraded EOD plan for live price data. Indices requested: {Indices}", string.Join(", ", indices));
        
        // Return empty collection instead of making EOD call
        return await Task.FromResult(Array.Empty<MarketIndexDto>());
    }

    /// <summary>
    /// Get market sentiment data from EOD Historical Data
    /// </summary>
    /// <param name="mcpServerService">MCP server service for making external calls</param>
    /// <param name="tickers">Stock tickers for sentiment analysis</param>
    /// <param name="date">Date for sentiment data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Market sentiment data</returns>
    public async Task<MarketSentimentDto?> GetMarketSentimentAsync(
        IMcpServerService? mcpServerService,
        IEnumerable<string> tickers,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching market sentiment from EOD for tickers: {Tickers}", string.Join(", ", tickers));

            if (mcpServerService == null)
            {
                _logger.LogWarning("MCP server service not available, returning null sentiment");
                return null;
            }

            var parameters = new Dictionary<string, object>
            {
                ["symbols"] = string.Join(",", tickers),
                ["start_date"] = date.AddDays(-7).ToString("yyyy-MM-dd"), // Get sentiment for past week
                ["end_date"] = date.ToString("yyyy-MM-dd")
            };

            var result = await mcpServerService.CallMcpToolAsync(
                _eodApiOptions.McpServerUrl,
                "get_sentiment_data",
                parameters,
                cancellationToken);

            return ParseSentimentResponse(result, date);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching market sentiment from EOD for tickers: {Tickers}", string.Join(", ", tickers));
            return null;
        }
    }

    /// <summary>
    /// Get VIX and other market indicators from EOD Historical Data
    /// </summary>
    /// <param name="mcpServerService">MCP server service for making external calls</param>
    /// <param name="date">Date for indicators</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Market indicators</returns>
    public async Task<IEnumerable<MarketIndicatorDto>> GetMarketIndicatorsAsync(
        IMcpServerService? mcpServerService,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching market indicators from EOD for date: {Date}", date);

            if (mcpServerService == null)
            {
                _logger.LogWarning("MCP server service not available, returning empty indicators");
                return Array.Empty<MarketIndicatorDto>();
            }

            var parameters = new Dictionary<string, object>
            {
                ["symbols"] = "VIX,SPY,QQQ", // VIX and major ETFs for indicators
                ["date"] = date.ToString("yyyy-MM-dd")
            };

            var result = await mcpServerService.CallMcpToolAsync(
                _eodApiOptions.McpServerUrl,
                "get_historical_stock_prices",
                parameters,
                cancellationToken);

            return ParseIndicatorsResponse(result, date);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching market indicators from EOD for date: {Date}", date);
            return Array.Empty<MarketIndicatorDto>();
        }
    }

    private IEnumerable<NewsItemDto> ParseNewsResponse(object result)
    {
        try
        {
            JsonElement jsonElement;
            
            if (result is JsonElement resultElement)
            {
                // Handle MCP JSON-RPC response structure - EOD specific format
                if (resultElement.TryGetProperty("result", out var resultProp) && 
                    resultProp.TryGetProperty("content", out var contentArray) && 
                    contentArray.ValueKind == JsonValueKind.Array &&
                    contentArray.GetArrayLength() > 0)
                {
                    var firstContent = contentArray.EnumerateArray().First();
                    if (firstContent.TryGetProperty("text", out var textProp))
                    {
                        var newsJsonString = textProp.GetString();
                        if (!string.IsNullOrEmpty(newsJsonString))
                        {
                            _logger.LogInformation("Parsing EOD news from content.text: {Length} characters", newsJsonString.Length);
                            jsonElement = JsonSerializer.Deserialize<JsonElement>(newsJsonString);
                            _logger.LogInformation("Successfully parsed JSON from content.text, ValueKind: {ValueKind}", jsonElement.ValueKind);
                        }
                        else
                        {
                            _logger.LogWarning("EOD news response text is empty");
                            return Array.Empty<NewsItemDto>();
                        }
                    }
                    else
                    {
                        _logger.LogWarning("EOD news response missing text property");
                        return Array.Empty<NewsItemDto>();
                    }
                }
                // Alternative: Try structuredContent.result
                else if (resultElement.TryGetProperty("result", out var resultProp2) &&
                         resultProp2.TryGetProperty("structuredContent", out var structuredProp) &&
                         structuredProp.TryGetProperty("result", out var structuredResultProp))
                {
                    var newsJsonString = structuredResultProp.GetString();
                    if (!string.IsNullOrEmpty(newsJsonString))
                    {
                        _logger.LogInformation("Parsing EOD news from structuredContent.result: {Length} characters", newsJsonString.Length);
                        jsonElement = JsonSerializer.Deserialize<JsonElement>(newsJsonString);
                    }
                    else
                    {
                        _logger.LogWarning("EOD structuredContent result is empty");
                        return Array.Empty<NewsItemDto>();
                    }
                }
                // Handle direct result property with array
                else if (resultElement.TryGetProperty("result", out var directResultProp) && 
                         directResultProp.ValueKind == JsonValueKind.Array)
                {
                    _logger.LogInformation("Found direct result array in EOD response");
                    jsonElement = directResultProp;
                }
                // Handle direct array response
                else if (resultElement.ValueKind == JsonValueKind.Array)
                {
                    _logger.LogInformation("EOD response is direct array");
                    jsonElement = resultElement;
                }
                else
                {
                    _logger.LogWarning("EOD news response structure not recognized. Properties: {Properties}", 
                        string.Join(", ", resultElement.EnumerateObject().Select(p => p.Name)));
                    return Array.Empty<NewsItemDto>();
                }
            }
            else
            {
                _logger.LogWarning("EOD news response not a JsonElement: {ResultType}", result?.GetType().Name ?? "null");
                return Array.Empty<NewsItemDto>();
            }

            if (jsonElement.ValueKind == JsonValueKind.Array)
            {
                var newsList = new List<NewsItemDto>();
                
                foreach (var item in jsonElement.EnumerateArray())
                {
                    if (item.TryGetProperty("title", out var titleProp) &&
                        item.TryGetProperty("content", out var contentProp) &&
                        item.TryGetProperty("date", out var dateProp))
                    {
                        // Get sentiment score from nested sentiment object
                        var sentimentScore = 0.5m; // Default neutral
                        if (item.TryGetProperty("sentiment", out var sentimentProp) && 
                            sentimentProp.ValueKind == JsonValueKind.Object &&
                            sentimentProp.TryGetProperty("polarity", out var polarityProp))
                        {
                            sentimentScore = polarityProp.GetDecimal();
                        }

                        // Handle symbols array
                        var symbols = Array.Empty<string>();
                        if (item.TryGetProperty("symbols", out var symbolsProp) && 
                            symbolsProp.ValueKind == JsonValueKind.Array)
                        {
                            symbols = symbolsProp.EnumerateArray()
                                .Select(s => s.GetString() ?? "")
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToArray();
                        }

                        // Extract source from link or use default
                        var source = "EOD Historical Data";
                        var url = "";
                        if (item.TryGetProperty("link", out var linkProp))
                        {
                            url = linkProp.GetString() ?? "";
                            if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
                            {
                                source = uri.Host;
                            }
                        }

                        newsList.Add(new NewsItemDto(
                            Title: titleProp.GetString() ?? "",
                            Summary: contentProp.GetString() ?? "",
                            Source: source,
                            PublishedDate: DateTime.TryParse(dateProp.GetString(), out var publishedDate) ? publishedDate : DateTime.Now,
                            Url: url,
                            RelatedTickers: symbols,
                            SentimentScore: sentimentScore,
                            Category: item.TryGetProperty("tags", out var tagsProp) && 
                                     tagsProp.ValueKind == JsonValueKind.Array &&
                                     tagsProp.GetArrayLength() > 0 
                                     ? tagsProp.EnumerateArray().First().GetString() ?? "General" 
                                     : "General"
                        ));
                    }
                }

                _logger.LogInformation("Successfully parsed {NewsCount} news items from EOD", newsList.Count);
                return newsList;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing news response from EOD");
        }

        return Array.Empty<NewsItemDto>();
    }

    private IEnumerable<MarketIndexDto> ParseIndicesResponse(object result)
    {
        try
        {
            if (result is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                var indicesList = new List<MarketIndexDto>();
                
                foreach (var item in jsonElement.EnumerateArray())
                {
                    if (item.TryGetProperty("symbol", out var symbolProp) &&
                        item.TryGetProperty("price", out var priceProp))
                    {
                        var change = item.TryGetProperty("change", out var changeProp) ? changeProp.GetDecimal() : 0m;
                        var changePercent = item.TryGetProperty("change_p", out var changePercentProp) ? changePercentProp.GetDecimal() : 0m;

                        indicesList.Add(new MarketIndexDto(
                            Name: GetIndexName(symbolProp.GetString() ?? ""),
                            Symbol: symbolProp.GetString() ?? "",
                            CurrentValue: priceProp.GetDecimal(),
                            DayChange: change,
                            DayChangePercentage: changePercent / 100m, // Convert percentage
                            LastUpdated: DateTime.Now
                        ));
                    }
                }

                return indicesList;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing indices response from EOD");
        }

        return Array.Empty<MarketIndexDto>();
    }

    private MarketSentimentDto? ParseSentimentResponse(object result, DateTime date)
    {
        try
        {
            if (result is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
            {
                var overallSentiment = jsonElement.TryGetProperty("overall_sentiment", out var overallProp) ? 
                    overallProp.GetDecimal() : 0.5m;

                var fearGreedIndex = jsonElement.TryGetProperty("fear_greed_index", out var fearGreedProp) ? 
                    fearGreedProp.GetDecimal() : 50m;

                return new MarketSentimentDto(
                    Date: date,
                    OverallSentimentScore: overallSentiment,
                    SentimentLabel: GetSentimentLabel(overallSentiment),
                    FearGreedIndex: fearGreedIndex,
                    SectorSentiments: ParseSectorSentiments(jsonElement),
                    Indicators: Array.Empty<MarketIndicatorDto>()
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing sentiment response from EOD");
        }

        return null;
    }

    private IEnumerable<MarketIndicatorDto> ParseIndicatorsResponse(object result, DateTime date)
    {
        try
        {
            if (result is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                var indicators = new List<MarketIndicatorDto>();
                
                foreach (var item in jsonElement.EnumerateArray())
                {
                    if (item.TryGetProperty("symbol", out var symbolProp) &&
                        item.TryGetProperty("value", out var valueProp))
                    {
                        var symbol = symbolProp.GetString() ?? "";
                        var value = valueProp.GetDecimal();

                        indicators.Add(new MarketIndicatorDto(
                            Name: GetIndicatorName(symbol),
                            Value: value,
                            Trend: GetTrendDirection(item),
                            Description: GetIndicatorDescription(symbol),
                            Date: date
                        ));
                    }
                }

                return indicators;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing indicators response from EOD");
        }

        return Array.Empty<MarketIndicatorDto>();
    }

    private IEnumerable<SectorSentimentDto> ParseSectorSentiments(JsonElement jsonElement)
    {
        var sectors = new List<SectorSentimentDto>();
        
        if (jsonElement.TryGetProperty("sectors", out var sectorsArray) && sectorsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var sector in sectorsArray.EnumerateArray())
            {
                if (sector.TryGetProperty("name", out var nameProp) &&
                    sector.TryGetProperty("sentiment", out var sentimentProp))
                {
                    var sentimentScore = sentimentProp.GetDecimal();
                    sectors.Add(new SectorSentimentDto(
                        SectorName: nameProp.GetString() ?? "",
                        SentimentScore: sentimentScore,
                        Trend: GetTrendFromSentiment(sentimentScore),
                        KeyFactors: Array.Empty<string>()
                    ));
                }
            }
        }

        return sectors;
    }

    private string GetIndexName(string symbol) => symbol switch
    {
        "SPY" => "S&P 500",
        "QQQ" => "NASDAQ-100",
        "DIA" => "Dow Jones Industrial Average",
        "VIX" => "Volatility Index",
        _ => symbol
    };

    private string GetIndicatorName(string symbol) => symbol switch
    {
        "VIX" => "VIX Volatility Index",
        "SPY" => "S&P 500 ETF",
        "QQQ" => "NASDAQ-100 ETF",
        _ => symbol
    };

    private string GetIndicatorDescription(string symbol) => symbol switch
    {
        "VIX" => "Market volatility and fear indicator",
        "SPY" => "S&P 500 index tracking ETF",
        "QQQ" => "NASDAQ-100 index tracking ETF",
        _ => $"Market indicator for {symbol}"
    };

    private string GetSentimentLabel(decimal sentiment) => sentiment switch
    {
        >= 0.8m => "Very Positive",
        >= 0.6m => "Positive",
        >= 0.4m => "Neutral",
        >= 0.2m => "Negative",
        _ => "Very Negative"
    };

    private string GetTrendFromSentiment(decimal sentiment) => sentiment switch
    {
        >= 0.6m => "Positive",
        >= 0.4m => "Neutral",
        _ => "Negative"
    };

    private string GetTrendDirection(JsonElement item)
    {
        if (item.TryGetProperty("change", out var changeProp))
        {
            var change = changeProp.GetDecimal();
            return change > 0 ? "Increasing" : change < 0 ? "Decreasing" : "Stable";
        }
        return "Unknown";
    }
}