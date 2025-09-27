using FtoConsulting.PortfolioManager.Api.Models.Requests;
using FtoConsulting.PortfolioManager.Api.Models.Responses;
using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Api.Services;

/// <summary>
/// Service for mapping between DTOs and domain entities
/// </summary>
public interface IPortfolioMappingService
{
    /// <summary>
    /// Maps an ingest request to a domain portfolio entity
    /// </summary>
    Portfolio MapToPortfolio(IngestPortfolioRequest request);

    /// <summary>
    /// Maps a domain portfolio to a response DTO
    /// </summary>
    IngestPortfolioResponse MapToResponse(Portfolio portfolio, int newInstrumentsCreated = 0, int instrumentsUpdated = 0);

    /// <summary>
    /// Maps a collection of holdings to flattened holding responses
    /// </summary>
    AccountHoldingsResponse MapToAccountHoldingsResponse(IEnumerable<Holding> holdings, Guid accountId, DateOnly valuationDate);

    /// <summary>
    /// Maps a single holding to a flattened holding response
    /// </summary>
    FlattenedHoldingResponse MapToFlattenedHoldingResponse(Holding holding);
}

/// <summary>
/// Implementation of portfolio mapping service
/// </summary>
public class PortfolioMappingService : IPortfolioMappingService
{
    public Portfolio MapToPortfolio(IngestPortfolioRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        // Create the portfolio entity
        var portfolio = new Portfolio(request.PortfolioName, request.AccountId);

        // Create holdings and their associated instruments
        foreach (var holdingDto in request.Holdings)
        {
            // Create instrument entity
            var instrument = new Instrument(
                holdingDto.Instrument.ISIN,
                holdingDto.Instrument.Name,
                holdingDto.Instrument.InstrumentTypeId,
                holdingDto.Instrument.SEDOL,
                holdingDto.Instrument.Description
            );

            // Create holding entity with the instrument reference
            var holding = new Holding(
                holdingDto.ValuationDate,
                instrument.Id,
                holdingDto.PlatformId,
                portfolio.Id,
                holdingDto.UnitAmount,
                holdingDto.BoughtValue,
                holdingDto.CurrentValue
            );

            // Use reflection to set the instrument navigation property since there's no public setter
            var instrumentProperty = typeof(Holding).GetProperty("Instrument");
            if (instrumentProperty != null)
            {
                instrumentProperty.SetValue(holding, instrument);
            }

            // Set daily P&L if provided
            if (holdingDto.DailyProfitLoss.HasValue)
            {
                var dailyPnLPercentage = holdingDto.DailyProfitLossPercentage ?? 
                    (holdingDto.CurrentValue > 0 ? (holdingDto.DailyProfitLoss.Value / (holdingDto.CurrentValue - holdingDto.DailyProfitLoss.Value)) * 100 : 0);
                
                holding.SetDailyProfitLoss(holdingDto.DailyProfitLoss.Value, dailyPnLPercentage);
            }

            // Add the holding to the portfolio
            portfolio.AddHolding(holding);
        }

        return portfolio;
    }

    public IngestPortfolioResponse MapToResponse(Portfolio portfolio, int newInstrumentsCreated = 0, int instrumentsUpdated = 0)
    {
        if (portfolio == null) throw new ArgumentNullException(nameof(portfolio));

        var holdings = portfolio.Holdings?.ToList() ?? new List<Holding>();

        return new IngestPortfolioResponse
        {
            PortfolioId = portfolio.Id,
            PortfolioName = portfolio.Name,
            AccountId = portfolio.AccountId,
            HoldingsCount = holdings.Count,
            NewInstrumentsCreated = newInstrumentsCreated,
            InstrumentsUpdated = instrumentsUpdated,
            TotalValue = portfolio.TotalValue,
            TotalProfitLoss = portfolio.TotalProfitLoss,
            IngestedAt = DateTime.UtcNow,
            Holdings = holdings.Select(h => new HoldingSummaryDto
            {
                HoldingId = h.Id,
                InstrumentISIN = h.Instrument?.ISIN ?? "Unknown",
                InstrumentName = h.Instrument?.Name ?? "Unknown",
                UnitAmount = h.UnitAmount,
                CurrentValue = h.CurrentValue,
                ProfitLoss = h.TotalProfitLoss
            }).ToList()
        };
    }

    public AccountHoldingsResponse MapToAccountHoldingsResponse(IEnumerable<Holding> holdings, Guid accountId, DateOnly valuationDate)
    {
        if (holdings == null) throw new ArgumentNullException(nameof(holdings));

        var holdingsList = holdings.ToList();
        var flattenedHoldings = holdingsList.Select(MapToFlattenedHoldingResponse).ToList();

        var totalCurrentValue = flattenedHoldings.Sum(h => h.CurrentValue);
        var totalBoughtValue = flattenedHoldings.Sum(h => h.BoughtValue);
        var totalGainLoss = totalCurrentValue - totalBoughtValue;
        var totalGainLossPercentage = totalBoughtValue != 0 ? (totalGainLoss / totalBoughtValue) * 100 : 0;

        return new AccountHoldingsResponse
        {
            AccountId = accountId,
            ValuationDate = valuationDate,
            TotalHoldings = flattenedHoldings.Count,
            TotalCurrentValue = totalCurrentValue,
            TotalBoughtValue = totalBoughtValue,
            TotalGainLoss = totalGainLoss,
            TotalGainLossPercentage = Math.Round(totalGainLossPercentage, 2),
            Holdings = flattenedHoldings
        };
    }

    public FlattenedHoldingResponse MapToFlattenedHoldingResponse(Holding holding)
    {
        if (holding == null) throw new ArgumentNullException(nameof(holding));

        var gainLoss = holding.CurrentValue - holding.BoughtValue;
        var gainLossPercentage = holding.BoughtValue != 0 ? (gainLoss / holding.BoughtValue) * 100 : 0;

        return new FlattenedHoldingResponse
        {
            HoldingId = holding.Id,
            ValuationDate = DateOnly.FromDateTime(holding.ValuationDate),
            UnitAmount = holding.UnitAmount,
            BoughtValue = holding.BoughtValue,
            CurrentValue = holding.CurrentValue,
            GainLoss = gainLoss,
            GainLossPercentage = Math.Round(gainLossPercentage, 2),

            // Portfolio Information
            PortfolioId = holding.PortfolioId,
            PortfolioName = holding.Portfolio?.Name ?? "Unknown Portfolio",
            AccountId = holding.Portfolio?.AccountId ?? Guid.Empty,
            AccountName = holding.Portfolio?.Account?.UserName ?? "Unknown Account",

            // Instrument Information
            InstrumentId = holding.InstrumentId,
            ISIN = holding.Instrument?.ISIN ?? "Unknown",
            SEDOL = holding.Instrument?.SEDOL,
            InstrumentName = holding.Instrument?.Name ?? "Unknown Instrument",
            InstrumentDescription = holding.Instrument?.Description,
            InstrumentType = holding.Instrument?.InstrumentType?.Name ?? "Unknown Type",

            // Platform Information
            PlatformId = holding.PlatformId,
            PlatformName = holding.Platform?.Name ?? "Unknown Platform"
        };
    }
}