# Portfolio Ingest Service

## Overview

The `IPortfolioIngest` service provides a robust mechanism for ingesting portfolio data, including holdings and instruments, into the PortfolioManager system. It handles the complexity of creating new instruments when they don't exist and associating holdings with the correct instrument references.

## Key Features

### ✅ **Instrument Deduplication**
- Identifies instruments by ISIN code
- Creates new instruments when they don't exist
- Updates existing instruments with new information when needed
- Maintains referential integrity between holdings and instruments

### ✅ **Transaction Management**
- All operations wrapped in database transactions
- Automatic rollback on errors
- Batch processing support for multiple portfolios

### ✅ **Domain-Driven Design**
- Uses proper domain entity constructors and methods
- Respects entity invariants and business rules
- Maintains aggregate consistency

### ✅ **Comprehensive Logging**
- Detailed logging for debugging and monitoring
- Performance tracking for large ingests
- Error context for troubleshooting

## Interface

```csharp
public interface IPortfolioIngest
{
    Task<Portfolio> IngestPortfolioAsync(Portfolio portfolio, CancellationToken cancellationToken = default);
    Task<IEnumerable<Portfolio>> IngestPortfoliosAsync(IEnumerable<Portfolio> portfolios, CancellationToken cancellationToken = default);
}
```

## Usage

### Single Portfolio Ingestion

```csharp
public async Task<Portfolio> IngestPortfolio(IPortfolioIngest service)
{
    // Create instruments with ISIN codes
    var appleStock = new Instrument("US0378331005", "Apple Inc", equityTypeId, null, null, "AAPL", "USD", "USD");
    var microsoftStock = new Instrument("US5949181045", "Microsoft Corporation", equityTypeId, null, null, "MSFT", "USD", "USD");

    // Create portfolio
    var portfolio = new Portfolio("Tech Portfolio", accountId);

    // Create holdings - the service will handle instrument matching by ISIN
    var holdings = new List<Holding>
    {
        new Holding(DateTime.Today, appleStock.Id, platformId, portfolio.Id, 100m, 15000m, 18000m),
        new Holding(DateTime.Today, microsoftStock.Id, platformId, portfolio.Id, 50m, 12000m, 14000m)
    };

    foreach (var holding in holdings)
    {
        portfolio.AddHolding(holding);
    }

    // Ingest - automatically handles instrument creation/matching
    return await service.IngestPortfolioAsync(portfolio);
}
```

### Batch Portfolio Ingestion

```csharp
public async Task<IEnumerable<Portfolio>> IngestMultiplePortfolios(IPortfolioIngest service, List<Portfolio> portfolios)
{
    // Batch ingest with optimized instrument processing
    return await service.IngestPortfoliosAsync(portfolios);
}
```

## How It Works

### 1. **Instrument Processing**
```
For each unique instrument (by ISIN):
├── Check if instrument exists in database
├── If exists:
│   ├── Compare with incoming data
│   └── Update if changes detected
└── If not exists:
    └── Create new instrument record
```

### 2. **Portfolio Processing**
```
For each portfolio:
├── Check if portfolio exists
├── If exists:
│   └── Update name and metadata
└── If not exists:
    └── Create new portfolio
```

### 3. **Holding Processing**
```
For each holding:
├── Resolve instrument ID by ISIN
├── Create new holding with correct references
├── Set portfolio and instrument associations
└── Preserve daily P&L data if available
```

## Integration

### Dependency Injection Setup

```csharp
// In your startup/program configuration
services.AddApplicationServices(); // Registers IPortfolioIngest

// Or manually:
services.AddScoped<IPortfolioIngest, PortfolioIngestService>();
```

### Required Dependencies

The service requires these repository interfaces:
- `IPortfolioRepository`
- `IInstrumentRepository` 
- `IHoldingRepository`
- `IUnitOfWork`

## Error Handling

### Transaction Rollback
- All operations are wrapped in transactions
- Automatic rollback on any failure
- Maintains data consistency

### Common Scenarios
- **Duplicate ISIN**: Uses existing instrument, updates if needed
- **Invalid portfolio data**: Validates using domain rules
- **Database constraints**: Proper error messages and rollback

## Performance Considerations

### Batch Processing
- `IngestPortfoliosAsync` optimizes instrument processing across multiple portfolios
- Reduces database round trips for duplicate instruments
- Single transaction for entire batch

### Memory Management
- Streams large datasets where possible
- Cancellation token support for long-running operations

## Testing

The service includes comprehensive unit tests covering:
- ✅ New instrument creation
- ✅ Existing instrument updates
- ✅ Duplicate instrument handling
- ✅ Transaction rollback scenarios
- ✅ Error handling and validation

## Examples

See `PortfolioIngestExample.cs` for complete working examples including:
- Creating portfolios with sample data
- Ingesting external data sources (CSV, API, etc.)
- Handling different data formats and structures

## Next Steps

This service provides the foundation for:
1. **API Endpoints**: REST controllers for portfolio ingestion
2. **File Processing**: CSV/Excel import functionality  
3. **External Integrations**: Broker API data feeds
4. **Scheduled Jobs**: Automated portfolio updates
5. **Validation Rules**: Enhanced business rule validation