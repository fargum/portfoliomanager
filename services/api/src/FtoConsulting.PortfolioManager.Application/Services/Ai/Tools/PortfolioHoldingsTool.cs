using System.ComponentModel;
using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Application.Utilities;
using Microsoft.Extensions.AI;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;


namespace FtoConsulting.PortfolioManager.Application.Services.Ai.Tools;

/// <summary>
/// MCP tool for retrieving portfolio holdings
/// </summary>
public class PortfolioHoldingsTool
{
    private readonly IHoldingService _holdingService;

    public PortfolioHoldingsTool(IHoldingService holdingService)
    {
        _holdingService = holdingService;
    }

    [Description("Retrieve portfolio holdings for a specific account and date. For current/today performance, use today's date to get real-time data. For historical analysis, specify the desired date.")]
    public async Task<object> GetPortfolioHoldings(
        [Description("Account ID")] int accountId,
        [Description("Date for holdings analysis. Use 'today' or current date (YYYY-MM-DD) for real-time data, or specify historical date in various formats (YYYY-MM-DD, DD/MM/YYYY, DD MMMM YYYY, etc.)")] string date,
        CancellationToken cancellationToken = default)
    {
        // Smart date handling: if asking for 'today', 'current', or similar, use today's date
        var effectiveDate = date;
        if (string.IsNullOrEmpty(date) || 
            date.ToLowerInvariant().Contains("today") || 
            date.ToLowerInvariant().Contains("current") ||
            date.ToLowerInvariant().Contains("now"))
        {
            effectiveDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        }
        
        var parsedDate = DateUtilities.ParseDate(effectiveDate);
        var holdings = await _holdingService.GetHoldingsByAccountAndDateAsync(accountId, parsedDate, cancellationToken);
        
        return new
        {
            AccountId = accountId,
            RequestedDate = date,
            EffectiveDate = effectiveDate,
            IsRealTimeData = parsedDate == DateOnly.FromDateTime(DateTime.UtcNow),
            HoldingsCount = holdings.Count(),
            Holdings = holdings.Select(h => new
            {
                h.Id,
                h.InstrumentId,
                InstrumentTicker = h.Instrument?.Ticker,
                InstrumentName = h.Instrument?.Name,
                h.UnitAmount,
                h.BoughtValue,
                h.CurrentValue,
                UnrealizedGainLoss = h.CurrentValue - h.BoughtValue,
                DailyProfitLoss = h.DailyProfitLoss,
                DailyProfitLossPercentage = h.DailyProfitLossPercentage
            })
        };
    }
}
