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
}