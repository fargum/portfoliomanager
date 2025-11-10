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

            // Clean ticker symbols - just remove exchange suffixes
            var cleanedTickers = tickers.Select(ticker => 
            {
                // Remove any exchange suffix (.LSE, .US, .L, etc.)
                var dotIndex = ticker.LastIndexOf('.');
                if (dotIndex > 0)
                {
                    return ticker.Substring(0, dotIndex);
                }
                return ticker;
            }).ToList();

            var parameters = new Dictionary<string, object>
            {
                ["ticker"] = string.Join(",", cleanedTickers), // Use cleaned tickers
                ["start_date"] = fromDate.ToString("yyyy-MM-dd"), 
                ["end_date"] = toDate.ToString("yyyy-MM-dd"),  
                ["limit"] = 10 
            };

            _logger.LogInformation("Using cleaned tickers for news: {CleanedTickers} (original: {OriginalTickers})", 
                string.Join(",", cleanedTickers), string.Join(",", tickers));

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
                
                // Limit to 4 news stories to prevent overwhelming the LLM context window
                var limitedNewsList = newsList.Take(4).ToList();
                if (limitedNewsList.Count < newsList.Count)
                {
                    _logger.LogInformation("Limited news results from {OriginalCount} to {LimitedCount} stories", 
                        newsList.Count, limitedNewsList.Count);
                }
                
                return limitedNewsList;
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

    /// <summary>
    /// Get real-time prices for specific tickers from EOD Historical Data
    /// Uses live pricing API endpoint: https://eodhd.com/api/real-time/{ticker}?api_token={token}&fmt=json
    /// </summary>
    /// <param name="mcpServerService">MCP server service for making external calls (not used for direct HTTP calls)</param>
    /// <param name="tickers">Stock tickers to get real-time prices for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Real-time price data for the tickers</returns>
    public async Task<Dictionary<string, decimal>> GetRealTimePricesAsync(
        IMcpServerService? mcpServerService,
        IEnumerable<string> tickers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching real-time prices from EOD for tickers: {Tickers}", string.Join(", ", tickers));

            if (string.IsNullOrEmpty(_eodApiOptions.Token))
            {
                _logger.LogWarning("EOD API token not configured, returning empty prices");
                return new Dictionary<string, decimal>();
            }

            var priceDict = new Dictionary<string, decimal>();
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(_eodApiOptions.TimeoutSeconds);

            // Process each ticker individually for real-time pricing
            foreach (var ticker in tickers)
            {
                try
                {
                    // Use ticker as provided without formatting
                    var url = $"{_eodApiOptions.BaseUrl}/real-time/{ticker}?api_token={_eodApiOptions.Token}&fmt=json";
                    
                    _logger.LogInformation("Fetching real-time price for ticker: {Ticker} from URL: {Url}", ticker, url.Replace(_eodApiOptions.Token, "***"));

                    var response = await httpClient.GetAsync(url, cancellationToken);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                        var price = ParseDirectRealTimePriceResponse(jsonContent, ticker);
                        
                        if (price.HasValue)
                        {
                            priceDict[ticker] = price.Value;
                            _logger.LogInformation("Successfully fetched real-time price for {Ticker}: {Price:C}", ticker, price);
                        }
                        else
                        {
                            _logger.LogWarning("No valid price found in response for ticker: {Ticker}", ticker);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("HTTP error fetching price for {Ticker}: {StatusCode} - {Reason}", 
                            ticker, response.StatusCode, response.ReasonPhrase);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching real-time price for ticker: {Ticker}", ticker);
                    // Continue with other tickers
                }
            }

            _logger.LogInformation("Successfully fetched {Count} real-time prices out of {Total} requested", 
                priceDict.Count, tickers.Count());

            return priceDict;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching real-time prices from EOD for tickers: {Tickers}", string.Join(", ", tickers));
            return new Dictionary<string, decimal>();
        }
    }

    /// <summary>
    /// Parse real-time price response from EOD MCP server
    /// </summary>
    private decimal? ParseRealTimePriceResponse(object result, string ticker)
    {
        try
        {
            if (result is JsonElement jsonElement)
            {
                // Handle MCP JSON-RPC response structure
                if (jsonElement.TryGetProperty("result", out var resultProp))
                {
                    // Try content array structure first
                    if (resultProp.TryGetProperty("content", out var contentArray) && 
                        contentArray.ValueKind == JsonValueKind.Array &&
                        contentArray.GetArrayLength() > 0)
                    {
                        var firstContent = contentArray.EnumerateArray().First();
                        if (firstContent.TryGetProperty("text", out var textProp))
                        {
                            var priceJsonString = textProp.GetString();
                            if (!string.IsNullOrEmpty(priceJsonString))
                            {
                                var priceElement = JsonSerializer.Deserialize<JsonElement>(priceJsonString);
                                return ExtractPriceFromElement(priceElement, ticker);
                            }
                        }
                    }
                    // Try direct result structure
                    else
                    {
                        return ExtractPriceFromElement(resultProp, ticker);
                    }
                }
                // Try direct element
                else
                {
                    return ExtractPriceFromElement(jsonElement, ticker);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing real-time price response for ticker: {Ticker}", ticker);
        }

        return null;
    }

    /// <summary>
    /// Extract price value from JSON element
    /// </summary>
    private decimal? ExtractPriceFromElement(JsonElement element, string ticker)
    {
        try
        {
            // Try different possible price field names
            var priceFields = new[] { "close", "price", "last", "current_price", "value" };
            
            foreach (var field in priceFields)
            {
                if (element.TryGetProperty(field, out var priceProp))
                {
                    if (priceProp.ValueKind == JsonValueKind.Number)
                    {
                        var price = priceProp.GetDecimal();
                        _logger.LogInformation("Extracted price for {Ticker} from field '{Field}': {Price}", ticker, field, price);
                        return price;
                    }
                    else if (priceProp.ValueKind == JsonValueKind.String)
                    {
                        var priceString = priceProp.GetString();
                        if (decimal.TryParse(priceString, out var price))
                        {
                            _logger.LogInformation("Parsed price for {Ticker} from string field '{Field}': {Price}", ticker, field, price);
                            return price;
                        }
                    }
                }
            }

            _logger.LogWarning("No recognized price field found for ticker: {Ticker}. Available fields: {Fields}", 
                ticker, string.Join(", ", element.EnumerateObject().Select(p => p.Name)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting price from element for ticker: {Ticker}", ticker);
        }

        return null;
    }

    /// <summary>
    /// Format ticker for EOD API (add exchange suffix if needed)
    /// </summary>
    private string FormatTickerForEod(string ticker)
    {
        // If ticker already has an exchange suffix, use as-is
        if (ticker.Contains('.'))
        {
            return ticker.ToLower(); // EOD uses lowercase for some exchanges like .lse
        }

        // Common exchange mappings - this should be enhanced based on your data
        // For now, we'll try to detect the exchange or default to US
        var upperTicker = ticker.ToUpper();
        
        // UK stocks often have specific patterns
        if (upperTicker.Length == 4 && (upperTicker.EndsWith("L") || IsLikelyUkStock(upperTicker)))
        {
            return $"{ticker.ToLower()}.lse";
        }
        
        // Default to US exchange
        return $"{ticker.ToUpper()}.US";
    }

    /// <summary>
    /// Simple heuristic to identify likely UK stocks
    /// </summary>
    private bool IsLikelyUkStock(string ticker)
    {
        // This is a simple heuristic - in practice you'd want a proper mapping
        var commonUkPatterns = new[] { "LGEN", "BARC", "LLOY", "TSCO", "ULVR", "SHEL", "AZN", "GSK" };
        return commonUkPatterns.Contains(ticker);
    }

    /// <summary>
    /// Parse real-time price response from direct EOD API call
    /// </summary>
    private decimal? ParseDirectRealTimePriceResponse(string jsonContent, string ticker)
    {
        try
        {
            _logger.LogInformation("Parsing EOD response for {Ticker}: {JsonContent}", ticker, jsonContent);
            
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonContent);
            return ExtractPriceFromElement(jsonElement, ticker);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing direct real-time price response for ticker: {Ticker}", ticker);
            return null;
        }
    }
}