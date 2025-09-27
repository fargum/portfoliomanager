# Holdings Retrieval API Documentation

## Overview
The Holdings API provides endpoints for retrieving portfolio holdings data with comprehensive details about portfolios, instruments, and platforms.

## Endpoints

### GET /api/holdings/account/{accountId}/date/{valuationDate}

Retrieves all holdings for a specific account on a given valuation date, returning flattened data that combines information from holdings, portfolios, instruments, and platforms.

#### Parameters

| Parameter | Type | Required | Description | Example |
|-----------|------|----------|-------------|---------|
| `accountId` | GUID | Yes | Unique identifier of the account | `12345678-1234-5678-9012-123456789012` |
| `valuationDate` | DateTime | Yes | Valuation date in YYYY-MM-DD format | `2025-09-27` |

#### Response Structure

```json
{
  "accountId": "12345678-1234-5678-9012-123456789012",
  "valuationDate": "2025-09-27",
  "totalHoldings": 25,
  "totalCurrentValue": 125750.50,
  "totalBoughtValue": 118250.25,
  "totalGainLoss": 7500.25,
  "totalGainLossPercentage": 6.34,
  "holdings": [
    {
      "holdingId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
      "valuationDate": "2025-09-27",
      "unitAmount": 100.50,
      "boughtValue": 1500.75,
      "currentValue": 1650.25,
      "gainLoss": 149.50,
      "gainLossPercentage": 9.97,
      "portfolioId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "portfolioName": "Growth Portfolio",
      "accountId": "12345678-1234-5678-9012-123456789012",
      "accountName": "john.doe",
      "instrumentId": "b2c3d4e5-f6g7-8901-bcde-f23456789012",
      "isin": "US0378331005",
      "sedol": "2046251",
      "instrumentName": "Apple Inc. Common Stock",
      "instrumentDescription": "Common shares of Apple Inc., a technology company",
      "instrumentType": "Equity",
      "platformId": "c3d4e5f6-g7h8-9012-cdef-345678901234",
      "platformName": "Interactive Brokers"
    }
  ]
}
```

#### Response Fields

##### Summary Fields
- **accountId**: The account identifier for which holdings were retrieved
- **valuationDate**: The valuation date requested
- **totalHoldings**: Total number of holdings returned
- **totalCurrentValue**: Sum of all current values
- **totalBoughtValue**: Sum of all bought values
- **totalGainLoss**: Total gain/loss amount (totalCurrentValue - totalBoughtValue)
- **totalGainLossPercentage**: Overall gain/loss percentage

##### Individual Holding Fields
Each holding in the `holdings` array contains:

**Holding Information:**
- **holdingId**: Unique identifier for the holding
- **valuationDate**: Date of the valuation
- **unitAmount**: Number of units held
- **boughtValue**: Original purchase value
- **currentValue**: Current market value
- **gainLoss**: Calculated gain/loss (currentValue - boughtValue)
- **gainLossPercentage**: Gain/loss as a percentage

**Portfolio Information:**
- **portfolioId**: Unique identifier for the portfolio
- **portfolioName**: Name of the portfolio
- **accountId**: Account identifier
- **accountName**: Account username

**Instrument Information:**
- **instrumentId**: Unique identifier for the instrument
- **isin**: International Securities Identification Number
- **sedol**: Stock Exchange Daily Official List (optional)
- **instrumentName**: Name of the instrument
- **instrumentDescription**: Description of the instrument (optional)
- **instrumentType**: Type of instrument (e.g., "Equity", "Bond")

**Platform Information:**
- **platformId**: Unique identifier for the platform
- **platformName**: Name of the platform where the holding is held

#### HTTP Status Codes

- **200 OK**: Holdings retrieved successfully
- **400 Bad Request**: Invalid account ID or date format
- **404 Not Found**: No holdings found for the specified account and date
- **500 Internal Server Error**: Server error occurred

#### Example Usage

```bash
# Retrieve holdings for a specific account on September 27, 2025
GET /api/holdings/account/12345678-1234-5678-9012-123456789012/date/2025-09-27
```

#### Error Responses

**400 Bad Request - Invalid Account ID:**
```json
{
  "title": "Invalid Account ID",
  "detail": "Account ID cannot be empty",
  "status": 400
}
```

**400 Bad Request - Invalid Date Format:**
```json
{
  "title": "Invalid Date Format",
  "detail": "Date must be in YYYY-MM-DD format",
  "status": 400
}
```

**404 Not Found:**
```json
{
  "title": "Holdings Not Found",
  "detail": "No holdings found for account 12345678-1234-5678-9012-123456789012 on date 2025-09-27",
  "status": 404
}
```

## Features

- **Flattened Data Structure**: All related data is included in a single response to minimize additional API calls
- **Calculated Fields**: Gain/loss amounts and percentages are automatically calculated
- **Comprehensive Information**: Includes data from holdings, portfolios, instruments, and platforms
- **Ordered Results**: Holdings are sorted by portfolio name, then by instrument name
- **Detailed Error Handling**: Clear error messages for various failure scenarios
- **OpenAPI/Swagger Documentation**: Full API documentation available at `/swagger`

## Data Relationships

The API returns data from multiple related entities:

```
Account → Portfolio → Holding → Instrument
                   ↓
                Platform
```

All relationships are resolved and flattened into a single response structure for efficient consumption by client applications.