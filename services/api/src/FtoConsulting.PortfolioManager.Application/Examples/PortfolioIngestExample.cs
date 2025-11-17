using FtoConsulting.PortfolioManager.Application.Services;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;


namespace FtoConsulting.PortfolioManager.Application.Examples;

/// <summary>
/// Example usage of the PortfolioIngest service
/// </summary>
public class PortfolioIngestExample
{
    private readonly IPortfolioIngest _portfolioIngest;

    public PortfolioIngestExample(IPortfolioIngest portfolioIngest)
    {
        _portfolioIngest = portfolioIngest;
    }

    /// <summary>
    /// Example of ingesting a portfolio with holdings and instruments
    /// </summary>
    public async Task<Portfolio> IngestSamplePortfolioAsync()
    {
        // Create some sample instrument types (these would typically exist already)
        var equityTypeId = 1;
        var bondTypeId = 2;

        // Create sample instruments (using ticker as the primary business identifier)
        var appleStock = new Instrument("Apple Inc", "AAPL", equityTypeId, "Apple Inc Common Stock", "USD", "USD");
        
var microsoftStock = new Instrument("Microsoft Corporation", "MSFT", equityTypeId, "Microsoft Corporation Common Stock", "USD", "USD");
        var treasuryBond = new Instrument("US Treasury Bond 2.75% 2025", "UST25", bondTypeId, "US Treasury Bond maturing 2025", "USD", "USD");

        // Create sample platforms (using integers instead of GUIDs)
        var fidelityPlatformId = 1;
        
var schwabPlatformId = 2;

        // Create sample account and portfolio (using integers instead of GUIDs)
        var accountId = 1;
        
var portfolio = new Portfolio("My Investment Portfolio", accountId);

        // Create holdings for the portfolio
        var holdings = new List<Holding>
        {
            // Apple holding on Fidelity
            new Holding(
                valuationDate: DateTime.Today,
                instrumentId: appleStock.Id,
                platformId: fidelityPlatformId,
                portfolioId: portfolio.Id,
                unitAmount: 100m,
                boughtValue: 15000m,
                currentValue: 18500m
            ),

            // Microsoft holding on Schwab
            new Holding(
                valuationDate: DateTime.Today,
                instrumentId: microsoftStock.Id,
                platformId: schwabPlatformId,
                portfolioId: portfolio.Id,
                unitAmount: 50m,
                boughtValue: 12000m,
                currentValue: 14250m
            ),

            // Treasury bond holding on Fidelity
            new Holding(
                valuationDate: DateTime.Today,
                instrumentId: treasuryBond.Id,
                platformId: fidelityPlatformId,
                portfolioId: portfolio.Id,
                unitAmount: 10000m, // Face value
                boughtValue: 9800m,
                currentValue: 9950m
            )
        };

        // Add holdings to portfolio (this sets up the navigation properties)
        foreach (var holding in holdings)
        {
            portfolio.AddHolding(holding);
        }

        // Ingest the portfolio - this will:
        // 1. Check if instruments exist by ticker, create them if they don't
        // 2. Create or update the portfolio
        // 3. Add all holdings with correct instrument references
        var ingestedPortfolio = await _portfolioIngest.IngestPortfolioAsync(portfolio);

        return ingestedPortfolio;
    }

    /// <summary>
    /// Example of ingesting portfolio data from external source (CSV, API, etc.)
    /// </summary>
    public async Task<Portfolio> IngestExternalPortfolioDataAsync(ExternalPortfolioData externalData)
    {
        // Create portfolio
        var portfolio = new Portfolio(externalData.PortfolioName, externalData.AccountId);

        // Convert external holdings to domain entities
        var holdings = new List<Holding>();
        
        foreach (var externalHolding in externalData.Holdings)
        {
            // Create instrument if we have the data
            Instrument? instrument = null;
            if (!string.IsNullOrEmpty(externalHolding.Ticker))
            {
                instrument = new Instrument(
                    externalHolding.InstrumentName,
                    externalHolding.Ticker,
                    externalHolding.InstrumentTypeId,
                    externalHolding.Description,
                    externalHolding.CurrencyCode,
                    externalHolding.QuoteUnit
                );
            }

            // Create holding
            var holding = new Holding(
                externalHolding.ValuationDate,
                instrument?.Id ?? externalHolding.InstrumentId, // Use existing ID if no instrument data
                externalHolding.PlatformId,
                portfolio.Id,
                externalHolding.Units,
                externalHolding.BookValue,
                externalHolding.MarketValue
            );

            // Set daily P&L if available
            if (externalHolding.DailyPnL.HasValue)
            {
                var dailyPnLPercentage = externalHolding.MarketValue > 0 
                    ? (externalHolding.DailyPnL.Value / (externalHolding.MarketValue - externalHolding.DailyPnL.Value)) * 100
                    : 0;
                
                holding.SetDailyProfitLoss(externalHolding.DailyPnL.Value, dailyPnLPercentage);
            }

            holdings.Add(holding);
            
            // Add to portfolio for navigation properties
            portfolio.AddHolding(holding);
        }

        // Ingest the portfolio
        return await _portfolioIngest.IngestPortfolioAsync(portfolio);
    }
}

/// <summary>
/// Example external data structure that might come from CSV, API, etc.
/// </summary>
public class ExternalPortfolioData
{
    public string PortfolioName { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public List<ExternalHoldingData> Holdings { get; set; } = new();
}

/// <summary>
/// Example external holding data structure
/// </summary>
public class ExternalHoldingData
{
    public DateTime ValuationDate { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string InstrumentName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CurrencyCode { get; set; }
    public string? QuoteUnit { get; set; }
    public int InstrumentId { get; set; } // Fallback if no ticker data
    public int InstrumentTypeId { get; set; }
    public int PlatformId { get; set; }
    public decimal Units { get; set; }
    public decimal BookValue { get; set; }
    public decimal MarketValue { get; set; }
    public decimal? DailyPnL { get; set; }
}
