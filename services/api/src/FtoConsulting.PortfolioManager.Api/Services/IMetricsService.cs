namespace FtoConsulting.PortfolioManager.Api.Services;

/// <summary>
/// Interface for collecting custom business metrics for the Portfolio Manager API
/// </summary>
public interface IMetricsService
{
    // Counter methods
    void IncrementPortfolioIngestions(string? accountId = null, string? status = null);
    void IncrementHoldingsRequests(string? accountId = null, string? status = null);
    void IncrementPriceRequests(string? symbol = null, string? status = null);
    void IncrementRevaluationRequests(string? accountId = null, string? status = null);
    void IncrementAiChatRequests(string? accountId = null, string? status = null, string? model = null);
    
    // Histogram methods
    void RecordPortfolioIngestDuration(double duration, string? accountId = null, string? status = null);
    void RecordHoldingsRequestDuration(double duration, string? accountId = null, string? status = null);
    void RecordPriceRequestDuration(double duration, string? symbol = null, string? status = null);
    void RecordRevaluationRequestDuration(double duration, string? accountId = null, string? status = null);
    void RecordAiChatRequestDuration(double duration, string? accountId = null, string? status = null, string? model = null);
}