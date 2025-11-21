# Automated EOD Revaluation Background Service

## Overview
The `AutomatedRevaluationBackgroundService` automatically fetches End-of-Day (EOD) market prices and revalues portfolio holdings on a scheduled basis. This eliminates the need for manual intervention in production environments.

## Architecture
- **Location**: `FtoConsulting.PortfolioManager.Application.Services`
- **Type**: Hosted Background Service (`BackgroundService`)
- **Scheduling**: Uses Cronos library for cron-based scheduling
- **Dependency**: Utilizes existing `IHoldingRevaluationService.FetchPricesAndRevalueHoldingsAsync`

## Configuration

### appsettings.json
```json
{
  "AutomatedRevaluation": {
    "CronSchedule": "0 6 * * 1-5",      // 6:00 AM UTC, Monday-Friday
    "UseCurrentDate": false,             // Use today's date vs previous business day
    "DaysBack": 1                        // How many days back to revalue
  }
}
```

### Environment-Specific Settings

#### Development (`appsettings.Development.json`)
- **Schedule**: `*/2 * * * *` (every 2 minutes for testing)
- **Purpose**: Rapid testing and development

#### Production (`appsettings.json`)
- **Schedule**: `0 6 * * 1-5` (6:00 AM UTC, weekdays)
- **Purpose**: Early morning processing after all EOD data is available

## Features

### Optimal Timing Strategy
The service runs **early morning (6:00 AM UTC)** rather than immediately after market close for several important reasons:

#### **Multi-Market Portfolio Support**
- **Global Markets**: Holdings span US, European, Asian markets with different close times
- **Fund Pricing**: Mutual funds and ETFs often publish NAV prices hours after market close
- **Data Availability**: EOD data providers need time to process and validate all market data
- **Clean Processing**: Ensures all previous day's pricing data is complete and final

#### **Timing Benefits**
- ✅ **Complete Data**: All EOD prices from previous day available
- ✅ **Fund NAVs**: Mutual fund prices published overnight are included  
- ✅ **Pre-Market**: Processing completes before new trading day begins
- ✅ **Reliable**: Avoids partial or preliminary pricing data
- ✅ **Global Coverage**: Captures closing prices from all time zones

### Smart Date Logic
- **Default**: Processes previous business day (handles weekends)
- **Configurable**: Can process current date or N days back
- **Weekend Handling**: Automatically skips to Friday for weekend dates

### Comprehensive Logging
```csharp
// Startup logging
"Automated revaluation service configured with schedule '{Schedule}' in UTC timezone"

// Execution logging
"Next automated revaluation scheduled for {NextRun} ({Delay} from now)"
"Starting automated revaluation at {StartTime}"
"Performing automated revaluation for date {TargetDate}"

// Results logging
"Automated revaluation completed successfully in {Duration:mm\:ss}. 
Prices - Success: {PriceSuccess}, Failed: {PriceFailed}. 
Holdings - Success: {HoldingSuccess}, Failed: {HoldingFailed}. 
Overall Success: {OverallSuccess}"
```

### OpenTelemetry Integration
- **Activity Source**: `PortfolioManager.AutomatedRevaluation`
- **Telemetry Tags**:
  - `trigger`: "scheduled"
  - `target.date`: Target revaluation date
  - `prices.successful/failed`: Price fetch statistics
  - `holdings.successful/failed`: Revaluation statistics
  - `overall.success`: Combined operation success
  - `duration.ms`: Total execution time

### Error Handling
- **Graceful Degradation**: Service continues on errors
- **Retry Logic**: Waits 5 minutes after unexpected errors
- **Logging**: Comprehensive error logging with context
- **No Service Termination**: Errors don't stop the background service

## Cron Schedule Examples

| Schedule | Description |
|----------|-------------|
| `0 6 * * 1-5` | 6:00 AM UTC, Monday-Friday (Production - early morning) |
| `*/2 * * * *` | Every 2 minutes (Development/Testing) |
| `0 7 * * 1-5` | 7:00 AM UTC, Monday-Friday (Alternative early morning) |
| `0 5 * * 2-6` | 5:00 AM UTC, Tuesday-Saturday (After global markets close) |

## Registration
The service is automatically registered in `ServiceCollectionExtensions.AddApplicationServices()`:

```csharp
services.AddHostedService<AutomatedRevaluationBackgroundService>();
```

## Dependencies
- **IHoldingRevaluationService**: Core revaluation functionality
- **IConfiguration**: Configuration access
- **ILogger**: Structured logging
- **IServiceProvider**: Scoped service creation
- **Cronos**: Cron expression parsing and scheduling

## Monitoring
- **Logs**: All operations logged with structured data
- **Telemetry**: Full OpenTelemetry tracing with metrics
- **Health**: Service health can be monitored through hosting infrastructure
- **Graceful Shutdown**: Responds to cancellation tokens properly

## Benefits
1. **Production Ready**: Eliminates manual daily operations
2. **Configurable**: Easy environment-specific configuration
3. **Observable**: Full logging and telemetry integration
4. **Resilient**: Handles errors without service disruption
5. **Clean Architecture**: Proper separation of concerns in Application layer