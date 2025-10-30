using System.ComponentModel;
using FtoConsulting.PortfolioManager.Application.Services;
using Microsoft.Extensions.AI;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai.Tools;

/// <summary>
/// MCP tool for retrieving portfolio holdings
/// </summary>
public class PortfolioHoldingsTool
{
    private readonly IHoldingsRetrieval _holdingsRetrieval;

    public PortfolioHoldingsTool(IHoldingsRetrieval holdingsRetrieval)
    {
        _holdingsRetrieval = holdingsRetrieval;
    }

    [Description("Retrieve portfolio holdings for a specific account and date")]
    public async Task<object> GetPortfolioHoldings(
        [Description("Account ID")] int accountId,
        [Description("Date in YYYY-MM-DD format")] string date,
        CancellationToken cancellationToken = default)
    {
        var parsedDate = DateOnly.Parse(date);
        var holdings = await _holdingsRetrieval.GetHoldingsByAccountAndDateAsync(accountId, parsedDate, cancellationToken);
        
        return new
        {
            AccountId = accountId,
            Date = date,
            Holdings = holdings.Select(h => new
            {
                h.Id,
                h.InstrumentId,
                InstrumentTicker = h.Instrument?.Ticker,
                InstrumentName = h.Instrument?.Name,
                h.UnitAmount,
                h.BoughtValue,
                h.CurrentValue,
                UnrealizedGainLoss = h.CurrentValue - h.BoughtValue
            })
        };
    }
}