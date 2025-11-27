using FtoConsulting.PortfolioManager.Application.DTOs;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using FtoConsulting.PortfolioManager.Domain.Constants;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service for calculating holding values using current market prices
/// </summary>
public class PricingCalculationHelper(
    IInstrumentPriceRepository instrumentPriceRepository,
    IPricingCalculationService pricingCalculationService,
    ILogger<PricingCalculationHelper> logger) : IPricingCalculationHelper
{

    public async Task<HoldingPriceResult> FetchAndCalculateHoldingValueAsync(
        int instrumentId,
        string ticker,
        decimal units,
        string? quoteUnit,
        string? priceCurrency,
        DateOnly valuationDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ticker))
            throw new ArgumentException("Ticker cannot be null or empty", nameof(ticker));

        if (units < 0)
            throw new ArgumentException("Units cannot be negative", nameof(units));

        logger.LogDebug("Calculating holding value for {Ticker} with {Units} units on {ValuationDate}", 
            ticker, units, valuationDate);

        // Check if this is a CASH instrument - no pricing needed
        if (ticker.Equals(ExchangeConstants.CASH_TICKER, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug("CASH instrument detected, using units as current value");
            return new HoldingPriceResult
            {
                CurrentPrice = 1.0m,
                CurrentValue = units, // For cash, units = value
                Success = true,
                IsCashInstrument = true
            };
        }

        try
        {
            // Try to get price for the specific date first
            var instrumentPrice = await instrumentPriceRepository.GetByInstrumentAndDateAsync(instrumentId, valuationDate, cancellationToken);
            
            if (instrumentPrice == null)
            {
                // Try to get the latest available price before or on the valuation date
                instrumentPrice = await instrumentPriceRepository.GetLatestPriceAsync(instrumentId, valuationDate, cancellationToken);
            }

            if (instrumentPrice == null)
            {
                logger.LogWarning("No price data available for instrument {Ticker} (ID: {InstrumentId}) on or before {ValuationDate}",
                    ticker, instrumentId, valuationDate);
                
                return new HoldingPriceResult
                {
                    Success = false,
                    ErrorMessage = $"No price data available for {ticker} on or before {valuationDate:yyyy-MM-dd}"
                };
            }

            // Use the existing IPricingCalculationService for all pricing calculations
            // This handles scaling factors, currency conversion, and unit conversion
            var currentValue = await pricingCalculationService.CalculateCurrentValueAsync(
                units,
                instrumentPrice.Price,
                quoteUnit,
                priceCurrency ?? instrumentPrice.Currency,
                valuationDate);

            // Get the scaled price for display purposes
            var scaledPrice = pricingCalculationService.ApplyScalingFactor(instrumentPrice.Price, ticker);

            logger.LogDebug("Price calculation successful for {Ticker}: OriginalPrice={OriginalPrice}, ScaledPrice={ScaledPrice}, Units={Units}, Value={Value}",
                ticker, instrumentPrice.Price, scaledPrice, units, currentValue);

            return new HoldingPriceResult
            {
                CurrentPrice = scaledPrice,
                CurrentValue = currentValue,
                Success = true,
                IsCashInstrument = false
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calculating holding value for {Ticker} (ID: {InstrumentId})", ticker, instrumentId);
            
            return new HoldingPriceResult
            {
                Success = false,
                ErrorMessage = $"Error calculating price for {ticker}: {ex.Message}"
            };
        }
    }
}