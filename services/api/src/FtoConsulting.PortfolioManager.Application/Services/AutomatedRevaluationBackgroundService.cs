using Cronos;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Background service that automatically fetches EOD prices and revalues holdings on a scheduled basis
/// </summary>
public class AutomatedRevaluationBackgroundService : BackgroundService
{
    private static readonly ActivitySource s_activitySource = new("PortfolioManager.AutomatedRevaluation");
    
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutomatedRevaluationBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    private readonly CronExpression _cronExpression;
    
    public AutomatedRevaluationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AutomatedRevaluationBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Default: Run at 6:00 AM UTC Monday to Friday (before markets open, after all EOD data available)
        var cronSchedule = _configuration["AutomatedRevaluation:CronSchedule"] ?? "0 6 * * 1-5";
        
        try
        {
            _cronExpression = CronExpression.Parse(cronSchedule);
            
            _logger.LogInformation("Automated revaluation service configured with schedule '{Schedule}' in UTC timezone",
                cronSchedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse cron schedule '{Schedule}'. Using default.", 
                cronSchedule);
            
            // Fallback to default
            _cronExpression = CronExpression.Parse("0 6 * * 1-5");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Automated revaluation background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var next = _cronExpression.GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Utc);
                if (next.HasValue)
                {
                    var delay = next.Value - DateTimeOffset.UtcNow;
                    
                    _logger.LogInformation("Next automated revaluation scheduled for {NextRun} ({Delay} from now)",
                        next.Value.ToString("yyyy-MM-dd HH:mm:ss zzz"), delay);
                    
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, stoppingToken);
                    }

                    if (!stoppingToken.IsCancellationRequested)
                    {
                        await PerformAutomatedRevaluation(stoppingToken);
                    }
                }
                else
                {
                    _logger.LogWarning("No next occurrence found for cron schedule. Waiting 1 hour before retry.");
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Automated revaluation service cancellation requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in automated revaluation service main loop");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Automated revaluation background service stopped");
    }

    private async Task PerformAutomatedRevaluation(CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity("AutomatedRevaluation");
        activity?.SetTag("trigger", "scheduled");
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Starting automated revaluation at {StartTime}", startTime);

            // Get the target date for revaluation
            var targetDate = GetRevaluationTargetDate();
            activity?.SetTag("target.date", targetDate.ToString("yyyy-MM-dd"));
            
            _logger.LogInformation("Performing automated revaluation for date {TargetDate}", targetDate);

            // Create a scope to get the required service
            using var scope = _serviceProvider.CreateScope();
            var revaluationService = scope.ServiceProvider.GetRequiredService<IHoldingRevaluationService>();

            // Perform the combined fetch and revaluation operation
            var result = await revaluationService.FetchPricesAndRevalueHoldingsAsync(targetDate, cancellationToken);

            var duration = DateTime.UtcNow - startTime;
            
            // Log comprehensive results
            _logger.LogInformation(
                "Automated revaluation completed successfully in {Duration:mm\\:ss}. " +
                "Prices - Success: {PriceSuccess}, Failed: {PriceFailed}. " +
                "Holdings - Success: {HoldingSuccess}, Failed: {HoldingFailed}. " +
                "Overall Success: {OverallSuccess}",
                duration,
                result.PriceFetchResult.SuccessfulPrices,
                result.PriceFetchResult.FailedPrices,
                result.HoldingRevaluationResult.SuccessfulRevaluations,
                result.HoldingRevaluationResult.FailedRevaluations,
                result.OverallSuccess);

            // Set telemetry tags
            activity?.SetTag("prices.successful", result.PriceFetchResult.SuccessfulPrices.ToString());
            activity?.SetTag("prices.failed", result.PriceFetchResult.FailedPrices.ToString());
            activity?.SetTag("holdings.successful", result.HoldingRevaluationResult.SuccessfulRevaluations.ToString());
            activity?.SetTag("holdings.failed", result.HoldingRevaluationResult.FailedRevaluations.ToString());
            activity?.SetTag("overall.success", result.OverallSuccess.ToString());
            activity?.SetTag("duration.ms", duration.TotalMilliseconds.ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);

            // Log warning if there were failures
            if (!result.OverallSuccess || result.PriceFetchResult.FailedPrices > 0 || result.HoldingRevaluationResult.FailedRevaluations > 0)
            {
                _logger.LogWarning("Automated revaluation had some failures. Check detailed logs for specifics.");
            }
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("duration.ms", duration.TotalMilliseconds.ToString());
            
            _logger.LogError(ex, "Automated revaluation failed after {Duration:mm\\:ss}", duration);
            
            // Don't throw - we want the service to continue and try again at the next scheduled time
        }
    }

    private DateOnly GetRevaluationTargetDate()
    {
        // Get configuration for target date logic
        var useCurrentDate = _configuration.GetValue<bool>("AutomatedRevaluation:UseCurrentDate", false);
        var daysBack = _configuration.GetValue<int>("AutomatedRevaluation:DaysBack", 1);

        if (useCurrentDate)
        {
            return DateOnly.FromDateTime(DateTime.Today);
        }

        // Default behavior: Use previous business day
        var targetDate = DateTime.Today.AddDays(-daysBack);
        
        // Handle Monday morning case: if target is Sunday, go back to Friday
        // (This only happens on Monday since cron runs weekdays only)
        if (targetDate.DayOfWeek == DayOfWeek.Sunday)
        {
            targetDate = targetDate.AddDays(-2); // Sunday -> Friday
        }
        else if (targetDate.DayOfWeek == DayOfWeek.Saturday)
        {
            targetDate = targetDate.AddDays(-1); // Saturday -> Friday (edge case)
        }

        return DateOnly.FromDateTime(targetDate);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Automated revaluation service is stopping");
        await base.StopAsync(cancellationToken);
    }
}