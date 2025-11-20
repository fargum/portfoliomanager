using System.Diagnostics.Metrics;

namespace FtoConsulting.PortfolioManager.Api.Services;

/// <summary>
/// Service for collecting custom business metrics for the Portfolio Manager API
/// </summary>
public class MetricsService : IMetricsService
{
    private readonly Meter _meter;
    
    // Counters
    private readonly Counter<long> _portfolioIngestionsTotal;
    private readonly Counter<long> _holdingsRequestsTotal;
    private readonly Counter<long> _priceRequestsTotal;
    private readonly Counter<long> _revaluationRequestsTotal;
    private readonly Counter<long> _aiChatRequestsTotal;
    
    // Histograms
    private readonly Histogram<double> _portfolioIngestDuration;
    private readonly Histogram<double> _holdingsRequestDuration;
    private readonly Histogram<double> _priceRequestDuration;
    private readonly Histogram<double> _revaluationRequestDuration;
    private readonly Histogram<double> _aiChatRequestDuration;

    public MetricsService()
    {
        _meter = new Meter("PortfolioManager.Business", "1.0.0");
        
        // Initialize counters
        _portfolioIngestionsTotal = _meter.CreateCounter<long>(
            "portfolio_ingestions_total",
            description: "Total number of portfolio ingestions"
        );
        
        _holdingsRequestsTotal = _meter.CreateCounter<long>(
            "holdings_requests_total", 
            description: "Total number of holdings requests"
        );
        
        _priceRequestsTotal = _meter.CreateCounter<long>(
            "price_requests_total",
            description: "Total number of price requests"
        );
        
        _revaluationRequestsTotal = _meter.CreateCounter<long>(
            "revaluation_requests_total",
            description: "Total number of revaluation requests"
        );
        
        _aiChatRequestsTotal = _meter.CreateCounter<long>(
            "ai_chat_requests_total",
            description: "Total number of AI chat requests"
        );
        
        // Initialize histograms
        _portfolioIngestDuration = _meter.CreateHistogram<double>(
            "portfolio_ingest_duration_seconds",
            unit: "s",
            description: "Duration of portfolio ingestion operations"
        );
        
        _holdingsRequestDuration = _meter.CreateHistogram<double>(
            "holdings_request_duration_seconds",
            unit: "s",
            description: "Duration of holdings requests"
        );
        
        _priceRequestDuration = _meter.CreateHistogram<double>(
            "price_request_duration_seconds",
            unit: "s",
            description: "Duration of price requests"
        );
        
        _revaluationRequestDuration = _meter.CreateHistogram<double>(
            "revaluation_request_duration_seconds",
            unit: "s",
            description: "Duration of revaluation requests"
        );
        
        _aiChatRequestDuration = _meter.CreateHistogram<double>(
            "ai_chat_request_duration_seconds",
            unit: "s",
            description: "Duration of AI chat requests"
        );
    }
    
    // Counter methods
    public void IncrementPortfolioIngestions(string? accountId = null, string? status = null)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("account_id", accountId),
            new("status", status)
        }.Where(kvp => kvp.Value != null).ToArray();
        
        _portfolioIngestionsTotal.Add(1, tags);
    }
    
    public void IncrementHoldingsRequests(string? accountId = null, string? status = null)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("account_id", accountId),
            new("status", status)
        }.Where(kvp => kvp.Value != null).ToArray();
        
        _holdingsRequestsTotal.Add(1, tags);
    }
    
    public void IncrementPriceRequests(string? symbol = null, string? status = null)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("symbol", symbol),
            new("status", status)
        }.Where(kvp => kvp.Value != null).ToArray();
        
        _priceRequestsTotal.Add(1, tags);
    }
    
    public void IncrementRevaluationRequests(string? accountId = null, string? status = null)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("account_id", accountId),
            new("status", status)
        }.Where(kvp => kvp.Value != null).ToArray();
        
        _revaluationRequestsTotal.Add(1, tags);
    }
    
    public void IncrementAiChatRequests(string? accountId = null, string? status = null, string? model = null)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("account_id", accountId),
            new("status", status),
            new("model", model)
        }.Where(kvp => kvp.Value != null).ToArray();
        
        _aiChatRequestsTotal.Add(1, tags);
    }
    
    // Histogram methods
    public void RecordPortfolioIngestDuration(double duration, string? accountId = null, string? status = null)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("account_id", accountId),
            new("status", status)
        }.Where(kvp => kvp.Value != null).ToArray();
        
        _portfolioIngestDuration.Record(duration, tags);
    }
    
    public void RecordHoldingsRequestDuration(double duration, string? accountId = null, string? status = null)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("account_id", accountId),
            new("status", status)
        }.Where(kvp => kvp.Value != null).ToArray();
        
        _holdingsRequestDuration.Record(duration, tags);
    }
    
    public void RecordPriceRequestDuration(double duration, string? symbol = null, string? status = null)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("symbol", symbol),
            new("status", status)
        }.Where(kvp => kvp.Value != null).ToArray();
        
        _priceRequestDuration.Record(duration, tags);
    }
    
    public void RecordRevaluationRequestDuration(double duration, string? accountId = null, string? status = null)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("account_id", accountId),
            new("status", status)
        }.Where(kvp => kvp.Value != null).ToArray();
        
        _revaluationRequestDuration.Record(duration, tags);
    }
    
    public void RecordAiChatRequestDuration(double duration, string? accountId = null, string? status = null, string? model = null)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("account_id", accountId),
            new("status", status),
            new("model", model)
        }.Where(kvp => kvp.Value != null).ToArray();
        
        _aiChatRequestDuration.Record(duration, tags);
    }
    
    public void Dispose()
    {
        _meter.Dispose();
    }
}