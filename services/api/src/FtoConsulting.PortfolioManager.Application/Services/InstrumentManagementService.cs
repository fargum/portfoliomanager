using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service for managing instrument creation and validation
/// </summary>
public class InstrumentManagementService : IInstrumentManagementService
{
    private readonly IInstrumentRepository _instrumentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InstrumentManagementService> _logger;

    public InstrumentManagementService(
        IInstrumentRepository instrumentRepository,
        IUnitOfWork unitOfWork,
        ILogger<InstrumentManagementService> logger)
    {
        _instrumentRepository = instrumentRepository ?? throw new ArgumentNullException(nameof(instrumentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Instrument> EnsureInstrumentExistsAsync(Instrument instrumentData, CancellationToken cancellationToken = default)
    {
        if (instrumentData == null)
            throw new ArgumentNullException(nameof(instrumentData));

        if (string.IsNullOrEmpty(instrumentData.Ticker))
            throw new ArgumentException("Instrument ticker cannot be null or empty", nameof(instrumentData));

        _logger.LogDebug("Ensuring instrument exists for ticker {Ticker}", instrumentData.Ticker);

        // Check if instrument already exists by ticker
        var existingInstrument = await _instrumentRepository.GetByTickerAsync(instrumentData.Ticker);

        if (existingInstrument == null)
        {
            // Create new instrument
            await _instrumentRepository.AddAsync(instrumentData);
            await _unitOfWork.SaveChangesAsync();
            
            _logger.LogInformation("Created new instrument {Ticker} - {Name}", instrumentData.Ticker, instrumentData.Name);
            return instrumentData;
        }
        else
        {
            // Update existing instrument if needed
            if (ShouldUpdateInstrument(existingInstrument, instrumentData))
            {
                existingInstrument.UpdateDetails(
                    instrumentData.Name, 
                    instrumentData.Ticker, 
                    instrumentData.Description, 
                    instrumentData.CurrencyCode, 
                    instrumentData.QuoteUnit);
                
                if (existingInstrument.InstrumentTypeId != instrumentData.InstrumentTypeId)
                {
                    existingInstrument.UpdateInstrumentType(instrumentData.InstrumentTypeId);
                }

                await _unitOfWork.SaveChangesAsync();
                _logger.LogDebug("Updated existing instrument {Ticker}", instrumentData.Ticker);
            }

            return existingInstrument;
        }
    }

    public async Task<Instrument> GetOrCreateInstrumentAsync(
        string ticker,
        string name,
        string? description = null,
        int? instrumentTypeId = null,
        string? currencyCode = null,
        string? quoteUnit = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ticker))
            throw new ArgumentException("Ticker cannot be null or empty", nameof(ticker));

        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));

        // Try to get existing instrument first
        var existingInstrument = await _instrumentRepository.GetByTickerAsync(ticker);
        if (existingInstrument != null)
        {
            _logger.LogDebug("Found existing instrument for ticker {Ticker}", ticker);
            return existingInstrument;
        }

        // Create new instrument with provided details
        var newInstrument = new Instrument(
            name: name,
            ticker: ticker,
            description: description,
            currencyCode: currencyCode,
            quoteUnit: quoteUnit,
            instrumentTypeId: instrumentTypeId ?? 1); // Default to equity if not specified

        await _instrumentRepository.AddAsync(newInstrument);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created new instrument {Ticker} - {Name}", ticker, name);
        return newInstrument;
    }

    public bool ShouldUpdateInstrument(Instrument existing, Instrument incoming)
    {
        if (existing == null || incoming == null)
            return false;

        // Check if any of the key properties have changed
        return !string.Equals(existing.Name, incoming.Name, StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(existing.Description, incoming.Description, StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(existing.CurrencyCode, incoming.CurrencyCode, StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(existing.QuoteUnit, incoming.QuoteUnit, StringComparison.OrdinalIgnoreCase) ||
               existing.InstrumentTypeId != incoming.InstrumentTypeId;
    }
}