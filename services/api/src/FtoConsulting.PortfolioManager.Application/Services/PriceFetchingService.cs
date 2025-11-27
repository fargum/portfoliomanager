using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Application.Models;
using FtoConsulting.PortfolioManager.Domain.Constants;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;

namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Service implementation for fetching market prices using EOD Data
/// </summary>
public class PriceFetchingService(
    IHoldingRepository holdingRepository,
    IInstrumentPriceRepository instrumentPriceRepository,
    IExchangeRateRepository exchangeRateRepository,
    IUnitOfWork unitOfWork,
    ILogger<PriceFetchingService> logger,
    IOptions<EodApiOptions> eodApiOptions) : IPriceFetching, IDisposable
{
    private const string ROLLED_FORWARD_STATUS = "ROLLED_FORWARD";
    private const string DEFAULT_EXCHANGE = "LSE";
    private const string FOREX_SUFFIX = ".FOREX";
    private const string EOD_SOURCE = "EOD";
    
    private readonly EodApiOptions _eodApiOptions = eodApiOptions?.Value ?? throw new ArgumentNullException(nameof(eodApiOptions));
    private readonly SemaphoreSlim _dbSemaphore = new(1, 1); // Allow only 1 concurrent database operation

    public async Task<PriceFetchResult> FetchAndPersistPricesForDateAsync(DateOnly valuationDate, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new PriceFetchResult { FetchedAt = DateTime.UtcNow };

        try
        {
            logger.LogInformation("Starting price fetch and persist operation for valuation date {ValuationDate}", valuationDate);

            // Step 1: Get instruments
            var instrumentList = await GetDistinctInstrumentsAsync(result, stopwatch, cancellationToken);
            if (instrumentList == null) return result;

            // Step 2: Fetch and process prices
            var pricesToPersist = await FetchPricesForInstrumentsAsync(instrumentList, valuationDate, result, cancellationToken);

            // Step 3: Persist prices to database
            await PersistPricesToDatabaseAsync(pricesToPersist, valuationDate, cancellationToken);

            // Step 4: Fetch and persist exchange rates
            await FetchAndLogExchangeRatesAsync(valuationDate, cancellationToken);

            // Finalize result
            FinalizeResult(result, pricesToPersist, stopwatch);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.FetchDuration = stopwatch.Elapsed;
            logger.LogError(ex, "Error during price fetch operation for date {ValuationDate}", valuationDate);
            throw;
        }
    }

    private async Task<List<(int InstrumentId, string Ticker, string Name)>?> GetDistinctInstrumentsAsync(PriceFetchResult result, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var instruments = await holdingRepository.GetAllDistinctInstrumentsAsync(cancellationToken);
        var instrumentList = instruments.ToList();
        result.TotalTickers = instrumentList.Count;

        if (!instrumentList.Any())
        {
            logger.LogWarning("No instruments found in holdings database");
            result.FetchDuration = stopwatch.Elapsed;
            return null;
        }

        logger.LogInformation("Found {Count} distinct instruments from all holdings", instrumentList.Count);
        return instrumentList;
    }

    private async Task<List<InstrumentPrice>> FetchPricesForInstrumentsAsync(List<(int InstrumentId, string Ticker, string Name)> instrumentList, DateOnly valuationDate, PriceFetchResult result, CancellationToken cancellationToken)
    {
        var pricesToPersist = new List<InstrumentPrice>();

        var priceTasks = instrumentList.Select(async instrument =>
        {
            try
            {
                var priceData = await FetchPriceForInstrument(instrument.Ticker, instrument.Ticker, valuationDate, cancellationToken);
                
                if (priceData != null)
                {
                    await ProcessSuccessfulPriceFetch(priceData, instrument, valuationDate, pricesToPersist);
                }
                else
                {
                    await ProcessFailedPriceFetch(instrument, valuationDate, result, pricesToPersist);
                }
            }
            catch (Exception ex)
            {
                await ProcessPriceFetchException(instrument, valuationDate, result, pricesToPersist, ex);
            }
        });

        await Task.WhenAll(priceTasks);
        return pricesToPersist;
    }

    private Task ProcessSuccessfulPriceFetch(InstrumentPriceData priceData, (int InstrumentId, string Ticker, string Name) instrument, DateOnly valuationDate, List<InstrumentPrice> pricesToPersist)
    {
        var instrumentPrice = CreateInstrumentPrice(priceData, instrument, valuationDate);
        
        lock (pricesToPersist)
        {
            pricesToPersist.Add(instrumentPrice);
        }
        
        return Task.CompletedTask;
    }

    private InstrumentPrice CreateInstrumentPrice(InstrumentPriceData priceData, (int InstrumentId, string Ticker, string Name) instrument, DateOnly valuationDate)
    {
        return new InstrumentPrice
        {
            InstrumentId = instrument.InstrumentId,
            Ticker = priceData.Ticker,
            ValuationDate = valuationDate,
            Name = priceData.Name,
            Price = priceData.Price,
            Currency = priceData.Currency,
            Change = priceData.Change,
            ChangePercent = priceData.ChangePercent,
            PreviousClose = priceData.PreviousClose,
            Open = priceData.Open,
            High = priceData.High,
            Low = priceData.Low,
            Volume = priceData.Volume,
            Market = priceData.Market,
            MarketStatus = priceData.MarketStatus,
            PriceTimestamp = priceData.Timestamp?.Kind == DateTimeKind.Utc 
                ? priceData.Timestamp 
                : priceData.Timestamp?.ToUniversalTime(),
            CreatedAt = DateTime.UtcNow
        };
    }

    private async Task ProcessFailedPriceFetch((int InstrumentId, string Ticker, string Name) instrument, DateOnly valuationDate, PriceFetchResult result, List<InstrumentPrice> pricesToPersist)
    {
        logger.LogDebug("No current price available for {Ticker}, attempting to roll forward previous price", (string)instrument.Ticker);
        
        await _dbSemaphore.WaitAsync();
        try
        {
            await TryRollForwardPrice(instrument, valuationDate, result, pricesToPersist);
        }
        finally
        {
            _dbSemaphore.Release();
        }
    }

    private async Task ProcessPriceFetchException((int InstrumentId, string Ticker, string Name) instrument, DateOnly valuationDate, PriceFetchResult result, List<InstrumentPrice> pricesToPersist, Exception ex)
    {
        logger.LogWarning(ex, "Failed to fetch price for {Ticker}, attempting to roll forward previous price", (string)instrument.Ticker);
        
        try
        {
            await TryRollForwardPrice(instrument, valuationDate, result, pricesToPersist);
        }
        catch (Exception rollforwardEx)
        {
            logger.LogError(rollforwardEx, "Failed to roll forward price for {Ticker}", (string)instrument.Ticker);
            AddFailedTicker(result, instrument.Ticker, $"API fetch failed: {ex.Message}. Rollforward failed: {rollforwardEx.Message}", "FETCH_ERROR");
        }
    }

    private async Task TryRollForwardPrice((int InstrumentId, string Ticker, string Name) instrument, DateOnly valuationDate, PriceFetchResult result, List<InstrumentPrice> pricesToPersist)
    {
        var latestPrice = await instrumentPriceRepository.GetLatestPriceAsync(instrument.InstrumentId, valuationDate.AddDays(-1));
        
        if (latestPrice != null)
        {
            var rolledForwardPrice = CreateRolledForwardPrice(latestPrice, instrument, valuationDate);
            
            lock (pricesToPersist)
            {
                pricesToPersist.Add(rolledForwardPrice);
            }
        }
        else
        {
            logger.LogWarning("No previous price available to roll forward for {Ticker}", (string)instrument.Ticker);
            AddFailedTicker(result, instrument.Ticker, "No price data available and no previous price to roll forward", "NO_DATA");
        }
    }

    private InstrumentPrice CreateRolledForwardPrice(InstrumentPrice latestPrice, (int InstrumentId, string Ticker, string Name) instrument, DateOnly valuationDate)
    {
        return new InstrumentPrice
        {
            InstrumentId = instrument.InstrumentId,
            Ticker = latestPrice.Ticker,
            ValuationDate = valuationDate,
            Name = latestPrice.Name,
            Price = latestPrice.Price,
            Currency = latestPrice.Currency,
            Change = 0,
            ChangePercent = 0,
            PreviousClose = latestPrice.Price,
            Open = latestPrice.Price,
            High = latestPrice.Price,
            Low = latestPrice.Price,
            Volume = 0,
            Market = latestPrice.Market,
            MarketStatus = ROLLED_FORWARD_STATUS,
            PriceTimestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    private void AddFailedTicker(PriceFetchResult result, string ticker, string errorMessage, string errorCode)
    {
        result.FailedTickers.Add(new FailedPriceData
        {
            Ticker = ticker,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode
        });
    }

    private async Task PersistPricesToDatabaseAsync(List<InstrumentPrice> pricesToPersist, DateOnly valuationDate, CancellationToken cancellationToken)
    {
        if (!pricesToPersist.Any()) return;

        logger.LogInformation("Persisting {Count} price records to database for valuation date {ValuationDate}", 
            pricesToPersist.Count, valuationDate);
        
        await instrumentPriceRepository.BulkUpsertAsync(pricesToPersist, cancellationToken);
        await unitOfWork.SaveChangesAsync();
        
        logger.LogInformation("Successfully persisted {Count} price records to database", pricesToPersist.Count);
    }

    private async Task FetchAndLogExchangeRatesAsync(DateOnly valuationDate, CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching exchange rates for valuation date {ValuationDate}", valuationDate);
        try
        {
            var exchangeRateResult = await FetchAndPersistExchangeRatesForDateAsync(valuationDate, cancellationToken);
            logger.LogInformation("Exchange rate fetch completed: Success: {SuccessfulRates}, Rolled Forward: {RolledForwardRates}, Failed: {FailedRates}, Duration: {Duration}ms",
                exchangeRateResult.SuccessfulRates, exchangeRateResult.RolledForwardRates, exchangeRateResult.FailedRates, exchangeRateResult.FetchDuration.TotalMilliseconds);
        }
        catch (Exception exchangeEx)
        {
            logger.LogWarning(exchangeEx, "Failed to fetch exchange rates for {ValuationDate}, but price fetching was successful", valuationDate);
        }
    }

    private void FinalizeResult(PriceFetchResult result, List<InstrumentPrice> pricesToPersist, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        result.FetchDuration = stopwatch.Elapsed;
        result.SuccessfulPrices = pricesToPersist.Count;

        logger.LogInformation("Price fetch and persist operation completed. Success: {SuccessCount}, Failed: {FailedCount}, Duration: {Duration}ms",
            result.SuccessfulPrices, result.FailedPrices, result.FetchDuration.TotalMilliseconds);
    }

    private async Task<InstrumentPriceData?> FetchPriceForInstrument(string isin, string? ticker, DateOnly valuationDate, CancellationToken cancellationToken)
    {
        try
        {
            // Skip instruments without ticker symbols as EOD API requires tickers
            if (string.IsNullOrWhiteSpace(ticker))
            {
                logger.LogWarning("No ticker available for ISIN {ISIN}, skipping price fetch", isin);
                return null;
            }
            
            // Validate API token is configured
            if (string.IsNullOrWhiteSpace(_eodApiOptions.Token))
            {
                logger.LogError("EOD API token is not configured. Please set EodApi:Token in configuration.");
                throw new InvalidOperationException("EOD API token is not configured.");
            }
            
            // Use direct HTTP call to EOD API endpoint for historical data on specific date
            var symbol = ticker.Contains('.') ? ticker : $"{ticker}.{DEFAULT_EXCHANGE}"; // Default to LSE if no exchange specified
            var dateString = valuationDate.ToString("yyyy-MM-dd");
            var url = $"{_eodApiOptions.BaseUrl}/eod/{symbol}?api_token={_eodApiOptions.Token}&fmt=json&from={dateString}&to={dateString}";
            
            using var httpClient = new HttpClient();
            
httpClient.Timeout = TimeSpan.FromSeconds(_eodApiOptions.TimeoutSeconds);
            
            try
            {
                var response = await httpClient.GetAsync(url, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    logger.LogWarning("EOD API returned {StatusCode} for {Ticker}: {Error}", 
                        response.StatusCode, ticker, errorContent);
                    return null;
                }
                
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                
                // Parse the JSON response
                var priceData = JsonSerializer.Deserialize<EODHistoricalData[]>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (priceData == null || !priceData.Any())
                {
                    logger.LogWarning("No price data returned from EOD API for ISIN {ISIN} (Ticker: {Ticker}) on date {ValuationDate}. This may be a non-trading day.", 
                        isin, ticker, valuationDate);
                    return null;
                }
                
                // Get the price data for the requested date (should be only one record when using from/to date range)
                var requestedDatePrice = priceData.FirstOrDefault(p => 
                    DateTime.Parse(p.Date).Date == valuationDate.ToDateTime(TimeOnly.MinValue).Date);
                
                
                if (requestedDatePrice == null)
                {
                    logger.LogWarning("No price data available for the specific date {ValuationDate} for {Ticker}. Available dates: {AvailableDates}",
                        valuationDate, ticker, string.Join(", ", priceData.Select(p => p.Date)));
                    return null;
                }
                
                // Map the EOD data to our InstrumentPriceData
                return new InstrumentPriceData
                {
                    Ticker = ticker,
                    Price = requestedDatePrice.Close,
                    Currency = CurrencyConstants.USD, // EOD typically returns USD for most stocks
                    Symbol = ticker,
                    Name = $"Instrument {ticker}",
                    Change = requestedDatePrice.Close - requestedDatePrice.Open,
                    ChangePercent = requestedDatePrice.Open != 0 ? ((requestedDatePrice.Close - requestedDatePrice.Open) / requestedDatePrice.Open) * 100 : null,
                    PreviousClose = requestedDatePrice.Open,
                    Open = requestedDatePrice.Open,
                    High = requestedDatePrice.High,
                    Low = requestedDatePrice.Low,
                    Volume = requestedDatePrice.Volume,
                    Market = symbol.Contains('.') ? symbol.Split('.').Last() : DEFAULT_EXCHANGE,
                    MarketStatus = "Closed",
                    Timestamp = DateTime.Parse(requestedDatePrice.Date)
                };
            }
            catch (HttpRequestException httpEx)
            {
                logger.LogError(httpEx, "HTTP error while fetching price for {Ticker}: {Message}", ticker, httpEx.Message);
                throw;
            }
            catch (TaskCanceledException timeoutEx)
            {
                logger.LogError(timeoutEx, "Timeout while fetching price for {Ticker}", ticker);
                throw;
            }
            catch (JsonException jsonEx)
            {
                logger.LogError(jsonEx, "JSON parsing error for {Ticker}: {Message}", ticker, jsonEx.Message);
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception while fetching price for ISIN {ISIN} (Ticker: {Ticker})", isin, ticker);
            throw;
        }
    }

    public async Task<ExchangeRateFetchResult> FetchAndPersistExchangeRatesForDateAsync(DateOnly valuationDate, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ExchangeRateFetchResult
        {
            FetchedAt = DateTime.UtcNow
        };

        var ratesToPersist = new List<ExchangeRate>();

        try
        {
            logger.LogInformation("Starting exchange rate fetch and persist operation for valuation date {ValuationDate}", valuationDate);

            // Define required currency pairs (base currency is always GBP)
            var requiredCurrencyPairs = new[]
            {
                (CurrencyConstants.USD, CurrencyConstants.GBP),
                (CurrencyConstants.EUR, CurrencyConstants.GBP)
            };

            result.TotalCurrencyPairs = requiredCurrencyPairs.Length;

            // Fetch exchange rates for each currency pair
            var rateTasks = requiredCurrencyPairs.Select(async currencyPair =>
            {
                try
                {
                    var (baseCurrency, targetCurrency) = currencyPair;
                    var rateData = await FetchExchangeRateForPair(baseCurrency, targetCurrency, valuationDate, cancellationToken);
                    
                    if (rateData != null)
                    {
                        var exchangeRate = new ExchangeRate(
                            baseCurrency,
                            targetCurrency,
                            rateData.Value.rate,
                            valuationDate,
                            EOD_SOURCE
                        );
                        
                        lock (ratesToPersist)
                        {
                            ratesToPersist.Add(exchangeRate);
                        }
                    }
                    else
                    {
                        // Try to roll forward previous rate
                        await TryRollForwardExchangeRate(baseCurrency, targetCurrency, valuationDate, ratesToPersist, result, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to fetch exchange rate for {CurrencyPair}, attempting rollforward", $"{currencyPair.Item1}/{currencyPair.Item2}");
                    
                    // Try to roll forward previous rate when API fails
                    await TryRollForwardExchangeRate(currencyPair.Item1, currencyPair.Item2, valuationDate, ratesToPersist, result, cancellationToken);
                }
            });

            // Execute all exchange rate fetching tasks concurrently
            await Task.WhenAll(rateTasks);

            // Persist the collected rates to database
            if (ratesToPersist.Any())
            {
                logger.LogInformation("Persisting {Count} exchange rate records to database for valuation date {ValuationDate}", 
                    ratesToPersist.Count, valuationDate);
                
                await exchangeRateRepository.BulkUpsertAsync(ratesToPersist, cancellationToken);
                await unitOfWork.SaveChangesAsync();
                
                logger.LogInformation("Successfully persisted {Count} exchange rate records to database", ratesToPersist.Count);
            }

            result.SuccessfulRates = ratesToPersist.Count(r => r.Source == EOD_SOURCE);
            result.RolledForwardRates = ratesToPersist.Count(r => r.Source == ROLLED_FORWARD_STATUS);
            result.FailedRates = result.TotalCurrencyPairs - result.SuccessfulRates - result.RolledForwardRates;

            stopwatch.Stop();
            result.FetchDuration = stopwatch.Elapsed;

            logger.LogInformation("Exchange rate fetch completed for {ValuationDate}. Success: {SuccessfulRates}, Rolled Forward: {RolledForwardRates}, Failed: {FailedRates}, Duration: {Duration}ms",
                valuationDate, result.SuccessfulRates, result.RolledForwardRates, result.FailedRates, result.FetchDuration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.FetchDuration = stopwatch.Elapsed;
            logger.LogError(ex, "Error during exchange rate fetch operation for date {ValuationDate}", valuationDate);
            throw;
        }
    }

    private async Task<(decimal rate, DateTime timestamp)?> FetchExchangeRateForPair(string baseCurrency, string targetCurrency, DateOnly valuationDate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_eodApiOptions.Token))
        {
            throw new InvalidOperationException("EOD API token is not configured.");
        }

        try
        {
            // EOD Forex endpoint format with date filter
            var forexSymbol = $"{baseCurrency}{targetCurrency}{FOREX_SUFFIX}";
            var dateString = valuationDate.ToString("yyyy-MM-dd");
            var url = $"{_eodApiOptions.BaseUrl}/eod/{forexSymbol}?api_token={_eodApiOptions.Token}&fmt=json&from={dateString}&to={dateString}";

            using var httpClient = new HttpClient();
            
            httpClient.Timeout = TimeSpan.FromSeconds(_eodApiOptions.TimeoutSeconds);

            var response = await httpClient.GetStringAsync(url, cancellationToken);
            
            var forexData = JsonSerializer.Deserialize<EODHistoricalData[]>(response);

            if (forexData != null && forexData.Length > 0)
            {
                var latestRate = forexData[0]; // Should be the rate for the specific date
                return (latestRate.Close, DateTime.Parse(latestRate.Date));
            }

            logger.LogWarning("No forex data returned from EOD API for {BaseCurrency}/{TargetCurrency} on {ValuationDate}", baseCurrency, targetCurrency, valuationDate);
            return null;
        }
        catch (HttpRequestException httpEx)
        {
            logger.LogError(httpEx, "HTTP error while fetching exchange rate for {BaseCurrency}/{TargetCurrency}: {Message}", baseCurrency, targetCurrency, httpEx.Message);
            throw;
        }
        catch (TaskCanceledException timeoutEx)
        {
            logger.LogError(timeoutEx, "Timeout while fetching exchange rate for {BaseCurrency}/{TargetCurrency}", baseCurrency, targetCurrency);
            throw;
        }
        catch (JsonException jsonEx)
        {
            logger.LogError(jsonEx, "JSON parsing error while fetching exchange rate for {BaseCurrency}/{TargetCurrency}: {Message}", baseCurrency, targetCurrency, jsonEx.Message);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception while fetching exchange rate for {BaseCurrency}/{TargetCurrency}", baseCurrency, targetCurrency);
            throw;
        }
    }

    private async Task TryRollForwardExchangeRate(string baseCurrency, string targetCurrency, DateOnly valuationDate, List<ExchangeRate> ratesToPersist, ExchangeRateFetchResult result, CancellationToken cancellationToken)
    {
        try
        {
            var latestRate = await exchangeRateRepository.GetLatestRateAsync(baseCurrency, targetCurrency, valuationDate.AddDays(-1), cancellationToken);
            
            if (latestRate != null)
            {
                // Roll forward the previous rate with updated valuation date
                var rolledForwardRate = new ExchangeRate(
                    baseCurrency,
                    targetCurrency,
                    latestRate.Rate, // Keep same rate
                    valuationDate, // Use current valuation date
                    ROLLED_FORWARD_STATUS
                );
                
                lock (ratesToPersist)
                {
                    ratesToPersist.Add(rolledForwardRate);
                }
            }
            else
            {
                // No previous rate available either
                logger.LogWarning("No previous exchange rate available to roll forward for {BaseCurrency}/{TargetCurrency}", baseCurrency, targetCurrency);
                result.FailedCurrencyPairs.Add(new FailedExchangeRateData
                {
                    BaseCurrency = baseCurrency,
                    TargetCurrency = targetCurrency,
                    ErrorMessage = "No exchange rate data available and no previous rate to roll forward",
                    ErrorCode = "NO_DATA"
                });
            }
        }
        catch (Exception rollforwardEx)
        {
            logger.LogError(rollforwardEx, "Failed to roll forward exchange rate for {BaseCurrency}/{TargetCurrency}", baseCurrency, targetCurrency);
            result.FailedCurrencyPairs.Add(new FailedExchangeRateData
            {
                BaseCurrency = baseCurrency,
                TargetCurrency = targetCurrency,
                ErrorMessage = $"Rollforward failed: {rollforwardEx.Message}",
                ErrorCode = "ROLLFORWARD_ERROR"
            });
        }
    }

    // Data model for EOD Historical Data API response
    private class EODHistoricalData
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;
        
        [JsonPropertyName("open")]
        public decimal Open { get; set; }
        
        [JsonPropertyName("high")]
        public decimal High { get; set; }
        
        [JsonPropertyName("low")]
        public decimal Low { get; set; }
        
        [JsonPropertyName("close")]
        public decimal Close { get; set; }
        
        [JsonPropertyName("adjusted_close")]
        public decimal Adjusted_Close { get; set; }
        
        [JsonPropertyName("volume")]
        public long Volume { get; set; }
    }

    public void Dispose()
    {
        _dbSemaphore?.Dispose();
    }
}