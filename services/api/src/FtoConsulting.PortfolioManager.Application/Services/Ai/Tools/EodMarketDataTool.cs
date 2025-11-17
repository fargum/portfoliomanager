using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using FtoConsulting.PortfolioManager.Application.Services.Ai;
using FtoConsulting.PortfolioManager.Application.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai.Tools;

/// <summary>
/// MCP tool for fetching real time market data from EOD Data MCP server
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
    /// Get real financial news from EOD  Data
    /// </summary>
    /// <param name="mcpServerService">MCP server service for making external calls (not used for direct HTTP calls)</param>
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

            if (string.IsNullOrEmpty(_eodApiOptions.Token))
            {
                _logger.LogWarning("EOD API token not configured, returning empty news");
                return Array.Empty<NewsItemDto>();
            }

            // Clean ticker symbols - just remove exchange suffixes
/*             var cleanedTickers = tickers.Select(ticker => 
            {
                // Remove any exchange suffix (.LSE, .US, .L, etc.)
                var dotIndex = ticker.LastIndexOf('.');
                if (dotIndex > 0)
                {
                    return ticker.Substring(0, dotIndex);
                }
                return ticker;
            }).ToList();

            if (!cleanedTickers.Any())
            {
                _logger.LogWarning("No valid tickers for news fetch");
                return Array.Empty<NewsItemDto>();
            } */

            if (!tickers.Any())
            {
                _logger.LogWarning("No valid tickers for news fetch");
                return Array.Empty<NewsItemDto>();
            }

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(_eodApiOptions.TimeoutSeconds);

            // Use EOD's news API: https://eodhd.com/api/news?s=gen.lse&offset=0&limit=10&api_token=TOKEN&fmt=json
            var tickerParam = string.Join(",", tickers);
            var url = $"{_eodApiOptions.BaseUrl}/news?s={tickerParam}&api_token={_eodApiOptions.Token}&offset=0&limit=10&fmt=json";
            
            _logger.LogInformation("Fetching news from URL: {Url}", url.Replace(_eodApiOptions.Token, "***"));

            var response = await httpClient.GetAsync(url, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return ParseDirectNewsResponse(jsonContent);
            }
            else
            {
                _logger.LogWarning("HTTP error fetching news: {StatusCode} - {Reason}", 
                    response.StatusCode, response.ReasonPhrase);
                return Array.Empty<NewsItemDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching financial news from EOD for tickers: {Tickers}", string.Join(", ", tickers));
            return Array.Empty<NewsItemDto>();
        }
    }


    /// <summary>
    /// Get market sentiment data from EOD Historical Data using direct API calls
    /// </summary>
    /// <param name="tickers">Stock tickers for sentiment analysis</param>
    /// <param name="date">Date for sentiment data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Market sentiment data</returns>
    public async Task<MarketSentimentDto?> GetMarketSentimentAsync(
        IEnumerable<string> tickers,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching market sentiment from EOD for tickers: {Tickers}", string.Join(", ", tickers));

            if (string.IsNullOrEmpty(_eodApiOptions.Token))
            {
                _logger.LogWarning("EOD API token not configured, returning default sentiment");
                return CreateDefaultSentimentResponse(date);
            }

            if (!tickers.Any())
            {
                _logger.LogWarning("No tickers provided for sentiment analysis");
                return CreateDefaultSentimentResponse(date);
            }

            // Use the first ticker for sentiment analysis
            var ticker = tickers.First();
            var fromDate = date.AddDays(-7).ToString("yyyy-MM-dd"); // Get sentiment for past week
            var toDate = date.ToString("yyyy-MM-dd");
            
            var url = $"https://eodhd.com/api/sentiments?s={ticker}&from={fromDate}&to={toDate}&api_token={_eodApiOptions.Token}&fmt=json";
            
            _logger.LogInformation("Fetching sentiment data from EOD API for ticker {Ticker}", ticker);
            
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(_eodApiOptions.TimeoutSeconds);
            
            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _logger.LogWarning("Empty sentiment response from EOD API for ticker {Ticker}", ticker);
                return CreateDefaultSentimentResponse(date);
            }

            var sentimentData = ParseSentimentResponse(jsonContent, date);
            if (sentimentData != null)
            {
                _logger.LogInformation("Successfully retrieved sentiment data for ticker {Ticker}", ticker);
                return sentimentData;
            }
            else
            {
                _logger.LogWarning("Failed to parse sentiment data for ticker {Ticker}", ticker);
                return CreateDefaultSentimentResponse(date);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching market sentiment from EOD for tickers: {Tickers}", string.Join(", ", tickers));
            return CreateDefaultSentimentResponse(date);
        }
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

    /// <summary>
    /// Parse direct news response from EOD API JSON string
    /// </summary>
    private IEnumerable<NewsItemDto> ParseDirectNewsResponse(string jsonContent)
    {
        try
        {
            _logger.LogInformation("Parsing direct EOD news response: {Length} characters", jsonContent.Length);
            
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _logger.LogWarning("Direct EOD news response is empty");
                return Array.Empty<NewsItemDto>();
            }

            var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonContent);
            _logger.LogInformation("Successfully parsed direct news JSON, ValueKind: {ValueKind}", jsonElement.ValueKind);

            var newsList = new List<NewsItemDto>();

            if (jsonElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var newsItem in jsonElement.EnumerateArray())
                {
                    var dto = ParseSingleNewsItem(newsItem);
                    if (dto != null)
                        newsList.Add(dto);
                }
            }
            else if (jsonElement.ValueKind == JsonValueKind.Object)
            {
                // Single news item
                var dto = ParseSingleNewsItem(jsonElement);
                if (dto != null)
                    newsList.Add(dto);
            }

            _logger.LogInformation("Parsed {Count} news items from direct EOD response", newsList.Count);
            return newsList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing direct news response from EOD");
            return Array.Empty<NewsItemDto>();
        }
    }

    /// <summary>
    /// Parse a single news item from JSON element
    /// </summary>
    private NewsItemDto? ParseSingleNewsItem(JsonElement newsItem)
    {
        try
        {
            var title = newsItem.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : "No Title";
            var content = newsItem.TryGetProperty("content", out var contentProp) ? contentProp.GetString() : string.Empty;
            var link = newsItem.TryGetProperty("link", out var linkProp) ? linkProp.GetString() : string.Empty;
            var dateStr = newsItem.TryGetProperty("date", out var dateProp) ? dateProp.GetString() : string.Empty;
            
            // Handle symbols as either string or array
            var symbolsList = new List<string>();
            if (newsItem.TryGetProperty("symbols", out var symbolsProp))
            {
                if (symbolsProp.ValueKind == JsonValueKind.String)
                {
                    var symbolsString = symbolsProp.GetString();
                    if (!string.IsNullOrEmpty(symbolsString))
                    {
                        symbolsList.AddRange(symbolsString.Split(',').Select(s => s.Trim()));
                    }
                }
                else if (symbolsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var symbol in symbolsProp.EnumerateArray())
                    {
                        if (symbol.ValueKind == JsonValueKind.String)
                        {
                            var symbolStr = symbol.GetString();
                            if (!string.IsNullOrEmpty(symbolStr))
                            {
                                symbolsList.Add(symbolStr.Trim());
                            }
                        }
                    }
                }
            }

            DateTime publishedDate = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var parsedDate))
            {
                publishedDate = parsedDate;
            }

            return new NewsItemDto(
                Title: title ?? "No Title",
                Summary: content ?? string.Empty,
                Source: "EOD Historical Data",
                PublishedDate: publishedDate,
                Url: link ?? string.Empty,
                RelatedTickers: symbolsList,
                SentimentScore: 0.0m, // EOD doesn't provide sentiment in basic news
                Category: "Financial News"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing individual news item");
            return null;
        }
    }

    /// <summary>
    /// Parse sentiment response from EOD API
    /// </summary>
    private MarketSentimentDto? ParseSentimentResponse(string jsonContent, DateTime date)
    {
        try
        {
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonContent);
            
            if (jsonElement.ValueKind == JsonValueKind.Array && jsonElement.GetArrayLength() > 0)
            {
                var sentiments = jsonElement.EnumerateArray().ToList();
                var totalCount = sentiments.Count;
                
                if (totalCount == 0)
                {
                    return CreateDefaultSentimentResponse(date);
                }

                // Calculate overall sentiment score from the sentiment data
                var positiveCount = sentiments.Count(s => 
                    s.TryGetProperty("sentiment", out var sentiment) && 
                    sentiment.ValueKind == JsonValueKind.String &&
                    sentiment.GetString()?.ToLowerInvariant().Contains("positive") == true);

                var negativeCount = sentiments.Count(s => 
                    s.TryGetProperty("sentiment", out var sentiment) && 
                    sentiment.ValueKind == JsonValueKind.String &&
                    sentiment.GetString()?.ToLowerInvariant().Contains("negative") == true);

                // Calculate sentiment score (0.0 = very negative, 1.0 = very positive, 0.5 = neutral)
                var sentimentScore = totalCount > 0 ? (decimal)(positiveCount - negativeCount) / totalCount * 0.5m + 0.5m : 0.5m;
                sentimentScore = Math.Max(0m, Math.Min(1m, sentimentScore)); // Clamp between 0 and 1

                var sentimentLabel = sentimentScore switch
                {
                    >= 0.7m => "Very Positive",
                    >= 0.6m => "Positive", 
                    >= 0.4m => "Neutral",
                    >= 0.3m => "Negative",
                    _ => "Very Negative"
                };

                return new MarketSentimentDto(
                    Date: date,
                    OverallSentimentScore: sentimentScore,
                    SentimentLabel: sentimentLabel,
                    FearGreedIndex: 50m, // EOD doesn't provide Fear & Greed index, use neutral
                    SectorSentiments: Array.Empty<SectorSentimentDto>(),
                    Indicators: Array.Empty<MarketIndicatorDto>()
                );
            }

            return CreateDefaultSentimentResponse(date);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing sentiment response");
            return null;
        }
    }
    private MarketSentimentDto CreateDefaultSentimentResponse(DateTime date)
    {
        return new MarketSentimentDto(
            Date: date,
            OverallSentimentScore: 0.5m,
            SentimentLabel: "Neutral (EOD unavailable)",
            FearGreedIndex: 50m,
            SectorSentiments: Array.Empty<SectorSentimentDto>(),
            Indicators: Array.Empty<MarketIndicatorDto>()
        );
    }

    
}