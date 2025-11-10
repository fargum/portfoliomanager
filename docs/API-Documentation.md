# Portfolio Manager API

## Overview

The Portfolio Manager API provides endpoints for ingesting and managing investment portfolio data. The API automatically handles instrument deduplication by ISIN codes and maintains referential integrity between portfolios, holdings, and instruments.

## Base URL

- **Development**: `https://localhost:7001`
- **HTTP**: `http://localhost:5001`

## Swagger Documentation

The API includes comprehensive Swagger/OpenAPI documentation available at:
- **Swagger UI**: `https://localhost:7001/` (root URL)
- **OpenAPI JSON**: `https://localhost:7001/swagger/v1/swagger.json`

## Endpoints

### 1. Ingest Portfolio Holdings

**Endpoint**: `POST /api/portfolios/ingest`

**Description**: Ingests portfolio holdings data with automatic instrument management.

**Key Features**:
- ✅ **Automatic Instrument Deduplication**: Instruments are identified by ISIN. Existing instruments are reused, new ones are created.
- ✅ **Transaction Safety**: All operations wrapped in database transactions with automatic rollback.
- ✅ **Comprehensive Validation**: Input validation with detailed error messages.
- ✅ **Detailed Response**: Returns complete portfolio summary with profit/loss calculations.

**Request Body**:
```json
{
  "portfolioName": "string",
  "accountId": "guid",
  "holdings": [
    {
      "valuationDate": "datetime",
      "platformId": "guid",
      "unitAmount": "decimal",
      "boughtValue": "decimal", 
      "currentValue": "decimal",
      "dailyProfitLoss": "decimal (optional)",
      "dailyProfitLossPercentage": "decimal (optional)",
      "instrument": {
        "isin": "string (12 chars)",
        "name": "string",
        "description": "string (optional)",
        "sedol": "string (7 chars, optional)",
        "instrumentTypeId": "guid"
      }
    }
  ]
}
```

**Response**:
```json
{
  "portfolioId": "guid",
  "portfolioName": "string",
  "accountId": "guid",
  "holdingsCount": "integer",
  "newInstrumentsCreated": "integer",
  "instrumentsUpdated": "integer",
  "totalValue": "decimal",
  "totalProfitLoss": "decimal",
  "ingestedAt": "datetime",
  "holdings": [
    {
      "holdingId": "guid",
      "instrumentISIN": "string",
      "instrumentName": "string",
      "unitAmount": "decimal",
      "currentValue": "decimal",
      "profitLoss": "decimal"
    }
  ]
}
```

### 2. Batch Ingest Portfolios

**Endpoint**: `POST /api/portfolios/ingest-batch`

**Description**: Ingests multiple portfolios in a single optimized transaction.

**Benefits**:
- ✅ **Performance**: Optimized instrument processing across multiple portfolios
- ✅ **Consistency**: All portfolios succeed or fail together
- ✅ **Efficiency**: Shared instruments processed only once

**Request/Response**: Array of the single portfolio ingest format.

## Sample Request

See `docs/sample-portfolio-request.json` for a complete example with multiple holdings.

## Error Handling

The API returns structured error responses:

```json
{
  "message": "string",
  "details": "string (optional)",
  "validationErrors": {
    "fieldName": ["error1", "error2"]
  },
  "timestamp": "datetime"
}
```

**HTTP Status Codes**:
- `200 OK`: Successful ingestion
- `400 Bad Request`: Validation errors or invalid data
- `500 Internal Server Error`: Unexpected errors

## Authentication

*Note: Authentication is not yet implemented. This will be added in future versions.*

## Rate Limiting

*Note: Rate limiting is not yet implemented. Consider implementing for production use.*

## Database Requirements

The API requires:
1. PostgreSQL database with proper connection string configuration
2. Database migrations applied (`dotnet ef database update`)
3. User secrets configured for development (`dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."`)

## Testing with Swagger UI

1. Navigate to `https://localhost:7001/` 
2. Expand the `/api/portfolios/ingest` endpoint
3. Click "Try it out"
4. Paste the sample JSON from `docs/sample-portfolio-request.json`
5. Update the GUIDs with valid values from your database
5. Execute the request

### 2. Holdings Revaluation

**Endpoint**: `POST /api/holdingrevaluation/revalue/{valuationDate}`

**Description**: Revalues all holdings for a specific valuation date using current market prices stored in the database.

**Key Features**:
- ✅ **Uses Existing Prices**: Leverages prices from the instrument_prices table
- ✅ **Quote Unit Conversion**: Handles GBX to GBP conversions automatically
- ✅ **Daily Change Calculation**: Computes profit/loss from previous valuations
- ✅ **Bulk Processing**: Efficiently processes all holdings in one operation

**Example**: `POST /api/holdingrevaluation/revalue/2025-11-07`

**Endpoint**: `POST /api/holdingrevaluation/revalue/today`

**Description**: Revalues all holdings for today's date using current market prices.

### 3. Combined Price Fetch and Revaluation

**Endpoint**: `POST /api/holdingrevaluation/fetch-prices-and-revalue/{valuationDate}`

**Description**: **NEW**: Performs a combined operation that first fetches current market prices from external data sources, then uses those prices to revalue all holdings.

**Key Features**:
- ✅ **Two-Step Process**: Fetches prices first, then revalues holdings
- ✅ **External Data Integration**: Uses EOD Historical Data API for current prices
- ✅ **Complete Operation**: Returns both price fetch and revaluation statistics
- ✅ **Flexible Date Parsing**: Accepts multiple date formats (YYYY-MM-DD, DD/MM/YYYY, DD MMMM YYYY)

**Example**: `POST /api/holdingrevaluation/fetch-prices-and-revalue/2025-11-07`

**Endpoint**: `POST /api/holdingrevaluation/fetch-prices-and-revalue/today`

**Description**: Performs the combined price fetch and revaluation operation for today's date.

**Response Example**:
```json
{
  "valuationDate": "2025-11-07",
  "priceFetchResult": {
    "totalTickers": 25,
    "successfulPrices": 23,
    "failedPrices": 2,
    "fetchDuration": "00:00:45.123"
  },
  "holdingRevaluationResult": {
    "valuationDate": "2025-11-07",
    "totalHoldings": 50,
    "successfulRevaluations": 48,
    "failedRevaluations": 2,
    "duration": "00:00:02.456"
  },
  "overallSuccess": true,
  "totalDuration": "00:00:47.579",
  "summary": "Fetched 23/25 prices, revalued 48/50 holdings in 47579ms"
}
```

## Example cURL Requests

### Portfolio Ingestion
```bash
curl -X POST "https://localhost:7001/api/portfolios/ingest" \
  -H "Content-Type: application/json" \
  -d @docs/sample-portfolio-request.json
```

### Holdings Revaluation  
```bash
curl -X POST "https://localhost:7001/api/holdingrevaluation/revalue/2025-11-07" \
  -H "Content-Type: application/json"
```

### Combined Price Fetch and Revaluation
```bash
curl -X POST "https://localhost:7001/api/holdingrevaluation/fetch-prices-and-revalue/2025-11-07" \
  -H "Content-Type: application/json"
```

## Architecture

The API follows Domain-Driven Design (DDD) principles:

- **Controllers**: Handle HTTP requests/responses and validation
- **Application Services**: `IPortfolioIngest` handles business logic
- **Domain Entities**: `Portfolio`, `Holding`, `Instrument` with business rules
- **Infrastructure**: Repository pattern with EF Core and PostgreSQL

## Next Steps

1. **Authentication & Authorization**: Add JWT/OAuth support
2. **Additional Endpoints**: Portfolio retrieval, updates, deletion
3. **File Import**: CSV/Excel import functionality  
4. **External Integrations**: Broker API data feeds
5. **Real-time Updates**: SignalR for live portfolio updates