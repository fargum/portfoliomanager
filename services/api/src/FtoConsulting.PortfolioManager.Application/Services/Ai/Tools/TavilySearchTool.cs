using System.ComponentModel;
using System.Text;
using System.Text.Json;
using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Application.DTOs.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai.Tools;

/// <summary>
/// MCP tool for web-powered market intelligence using the Tavily search API.
/// Handles recent news and company research. Real-time pricing and sentiment remain with EodMarketDataTool.
/// </summary>
public class TavilySearchTool
{
    private readonly ILogger<TavilySearchTool> _logger;
    private readonly TavilyOptions _tavilyOptions;

    private static readonly string[] FundamentalsDomains =
    [
        "stockanalysis.com",
        "macrotrends.net",
        "simplywall.st",
        "marketwatch.com",
        "finviz.com",
        "reuters.com",
        "finance.yahoo.com"
    ];

    public TavilySearchTool(
        ILogger<TavilySearchTool> logger,
        IOptions<TavilyOptions> tavilyOptions)
    {
        _logger = logger;
        _tavilyOptions = tavilyOptions.Value;
    }

    [Description("Search for recent news articles about specific stock tickers. Use this for up-to-date market news and events from the past week.")]
    public async Task<object> SearchRecentNews(
        [Description("List of stock tickers to search news for (e.g. AAPL, TSLA)")] string[] tickers,
        [Description("Company names corresponding to the tickers, used to improve search accuracy (e.g. 'Apple Tesla')")] string companyNames,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_tavilyOptions.ApiKey))
        {
            _logger.LogWarning("Tavily API key not configured — cannot fetch recent news");
            return new { Error = "Tavily API key not configured" };
        }

        try
        {
            var tickerString = string.Join(" ", tickers);
            var query = $"{tickerString} {companyNames} stock news".Trim();

            _logger.LogInformation("Searching Tavily for recent news: {Query}", query);

            var requestBody = new
            {
                query,
                topic = "news",
                time_range = "week",
                max_results = 10
            };

            var response = await PostToTavilyAsync("/search", requestBody, cancellationToken);
            if (response == null)
                return new { Tickers = tickers, News = Array.Empty<NewsItemDto>() };

            var news = ParseNewsResults(response.Value);

            _logger.LogInformation("Tavily returned {Count} recent news items for tickers: {Tickers}",
                news.Count, string.Join(", ", tickers));

            return new { Tickers = tickers, News = news };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Tavily for recent news for tickers: {Tickers}", string.Join(", ", tickers));
            return new { Error = "Failed to retrieve recent news", Tickers = tickers };
        }
    }

    [Description("Research company fundamentals including P/E ratio, earnings, EPS, analyst ratings, and price targets for a specific stock. Returns an AI-generated summary with sources.")]
    public async Task<object> ResearchCompanyFundamentals(
        [Description("Stock ticker symbol (e.g. AAPL)")] string ticker,
        [Description("Company name (e.g. Apple Inc)")] string companyName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_tavilyOptions.ApiKey))
        {
            _logger.LogWarning("Tavily API key not configured — cannot research company fundamentals");
            return new { Error = "Tavily API key not configured" };
        }

        try
        {
            var query = $"{companyName} {ticker} P/E ratio earnings EPS analyst rating price target valuation 2025";

            _logger.LogInformation("Researching fundamentals via Tavily for {Ticker} ({CompanyName})", ticker, companyName);

            var requestBody = new
            {
                query,
                search_depth = "advanced",
                include_answer = "advanced",
                max_results = 8,
                include_domains = FundamentalsDomains
            };

            var response = await PostToTavilyAsync("/search", requestBody, cancellationToken);
            if (response == null)
                return new { Ticker = ticker, CompanyName = companyName, Error = "No data returned from Tavily" };

            var answer = ExtractAnswer(response.Value);
            var sources = ExtractSources(response.Value);

            _logger.LogInformation("Tavily fundamentals research complete for {Ticker}. Answer length: {Length}", ticker, answer?.Length ?? 0);

            return new
            {
                Ticker = ticker,
                CompanyName = companyName,
                Summary = answer,
                Sources = sources
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error researching fundamentals via Tavily for {Ticker}", ticker);
            return new { Error = "Failed to research company fundamentals", Ticker = ticker };
        }
    }

    [Description("Get a general overview of a company including its business model, competitive position, and recent strategic developments.")]
    public async Task<object> GetCompanyOverview(
        [Description("Stock ticker symbol (e.g. AAPL)")] string ticker,
        [Description("Company name (e.g. Apple Inc)")] string companyName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_tavilyOptions.ApiKey))
        {
            _logger.LogWarning("Tavily API key not configured — cannot get company overview");
            return new { Error = "Tavily API key not configured" };
        }

        try
        {
            var query = $"{companyName} {ticker} company overview business model competitive position recent developments 2025";

            _logger.LogInformation("Getting company overview via Tavily for {Ticker} ({CompanyName})", ticker, companyName);

            var requestBody = new
            {
                query,
                search_depth = "advanced",
                include_answer = "advanced",
                max_results = 5
            };

            var response = await PostToTavilyAsync("/search", requestBody, cancellationToken);
            if (response == null)
                return new { Ticker = ticker, CompanyName = companyName, Error = "No data returned from Tavily" };

            var answer = ExtractAnswer(response.Value);
            var sources = ExtractSources(response.Value);

            _logger.LogInformation("Tavily company overview complete for {Ticker}. Answer length: {Length}", ticker, answer?.Length ?? 0);

            return new
            {
                Ticker = ticker,
                CompanyName = companyName,
                Overview = answer,
                Sources = sources
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting company overview via Tavily for {Ticker}", ticker);
            return new { Error = "Failed to get company overview", Ticker = ticker };
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<JsonElement?> PostToTavilyAsync(string endpoint, object body, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(_tavilyOptions.TimeoutSeconds);
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_tavilyOptions.ApiKey}");

        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url = $"{_tavilyOptions.BaseUrl.TrimEnd('/')}{endpoint}";
        _logger.LogInformation("Posting to Tavily: {Url}", url);

        var response = await httpClient.PostAsync(url, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Tavily returned {StatusCode}: {Body}", response.StatusCode, errorBody);
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<JsonElement>(responseJson);
    }

    private List<NewsItemDto> ParseNewsResults(JsonElement response)
    {
        var news = new List<NewsItemDto>();

        if (!response.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return news;

        foreach (var item in results.EnumerateArray())
        {
            var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            var url = item.TryGetProperty("url", out var u) ? u.GetString() ?? string.Empty : string.Empty;
            var content = item.TryGetProperty("content", out var c) ? c.GetString() ?? string.Empty : string.Empty;
            var publishedDate = DateTime.UtcNow;

            if (item.TryGetProperty("published_date", out var pd) && pd.ValueKind == JsonValueKind.String)
                DateTime.TryParse(pd.GetString(), out publishedDate);

            news.Add(new NewsItemDto(
                Title: title,
                Summary: content,
                Source: ExtractDomain(url),
                PublishedDate: publishedDate,
                Url: url,
                RelatedTickers: [],
                SentimentScore: 0m,
                Category: "Financial News"
            ));
        }

        return news;
    }

    private static string? ExtractAnswer(JsonElement response)
    {
        if (response.TryGetProperty("answer", out var answer) && answer.ValueKind == JsonValueKind.String)
            return answer.GetString();
        return null;
    }

    private static List<object> ExtractSources(JsonElement response)
    {
        var sources = new List<object>();

        if (!response.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return sources;

        foreach (var item in results.EnumerateArray())
        {
            var title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
            var url = item.TryGetProperty("url", out var u) ? u.GetString() : null;
            sources.Add(new { Title = title, Url = url });
        }

        return sources;
    }

    private static string ExtractDomain(string url)
    {
        if (string.IsNullOrEmpty(url)) return "Tavily Search";
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.Host.Replace("www.", string.Empty);
        return "Tavily Search";
    }
}
