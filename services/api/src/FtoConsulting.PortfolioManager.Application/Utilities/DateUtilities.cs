using System.Globalization;

namespace FtoConsulting.PortfolioManager.Application.Utilities;

/// <summary>
/// Utility class for consistent date parsing and formatting
/// </summary>
public static class DateUtilities
{
    /// <summary>
    /// Standard date formats accepted by the API
    /// </summary>
    private static readonly string[] AcceptedDateFormats = new[]
    {
        "yyyy-MM-dd",           // ISO format (preferred)
        "dd/MM/yyyy",           // UK format
        "MM/dd/yyyy",           // US format
        "dd-MM-yyyy",           // Alternative UK format
        "MM-dd-yyyy",           // Alternative US format
        "yyyy/MM/dd",           // Alternative ISO format
        "d MMMM yyyy",          // e.g., "6 November 2025"
        "MMMM d, yyyy",         // e.g., "November 6, 2025"
        "dd MMMM yyyy",         // e.g., "06 November 2025"
        "MMMM dd, yyyy"         // e.g., "November 06, 2025"
    };

    /// <summary>
    /// Parse a date string using multiple formats with UK culture preference
    /// Supports relative dates: 'today', 'yesterday', 'tomorrow'
    /// </summary>
    /// <param name="dateString">The date string to parse</param>
    /// <returns>Parsed DateOnly</returns>
    /// <exception cref="FormatException">Thrown when the date cannot be parsed</exception>
    public static DateOnly ParseDate(string dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            throw new ArgumentException("Date string cannot be null or empty", nameof(dateString));

        // Handle relative date terms
        var normalizedDateString = dateString.Trim().ToLowerInvariant();
        switch (normalizedDateString)
        {
            case "today":
                return DateOnly.FromDateTime(DateTime.UtcNow);
            case "yesterday":
                return DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            case "tomorrow":
                return DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        }

        // Try parsing with UK culture first (dd/MM/yyyy preference)
        var ukCulture = CultureInfo.GetCultureInfo("en-GB");
        
        // First try with UK culture and accepted formats
        if (DateOnly.TryParseExact(dateString, AcceptedDateFormats, ukCulture, DateTimeStyles.None, out var result))
        {
            return result;
        }

        // Try with invariant culture
        if (DateOnly.TryParseExact(dateString, AcceptedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
        {
            return result;
        }

        // Last resort - try generic parsing with UK culture
        if (DateOnly.TryParse(dateString, ukCulture, out result))
        {
            return result;
        }

        throw new FormatException($"Unable to parse date string '{dateString}'. Expected formats: {string.Join(", ", AcceptedDateFormats)}, or relative terms: today, yesterday, tomorrow");
    }

    /// <summary>
    /// Parse a date string as DateTime using multiple formats with UK culture preference
    /// </summary>
    /// <param name="dateString">The date string to parse</param>
    /// <returns>Parsed DateTime</returns>
    /// <exception cref="FormatException">Thrown when the date cannot be parsed</exception>
    public static DateTime ParseDateTime(string dateString)
    {
        var dateOnly = ParseDate(dateString);
        return dateOnly.ToDateTime(TimeOnly.MinValue);
    }

    /// <summary>
    /// Format a DateOnly as API standard format (yyyy-MM-dd)
    /// </summary>
    /// <param name="date">The date to format</param>
    /// <returns>Formatted date string</returns>
    public static string FormatForApi(DateOnly date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Format a DateTime as API standard format (yyyy-MM-dd)
    /// </summary>
    /// <param name="date">The date to format</param>
    /// <returns>Formatted date string</returns>
    public static string FormatForApi(DateTime date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Get the previous working day (Monday-Friday) for API calls when current/today data is requested
    /// Since portfolio data is not real-time, this provides the most recent business day
    /// </summary>
    /// <returns>Previous working day formatted for API</returns>
    public static string GetPreviousWorkingDayForApi()
    {
        var today = DateTime.Today;
        var previousWorkingDay = GetPreviousWorkingDay(today);
        return FormatForApi(previousWorkingDay);
    }

    /// <summary>
    /// Get the previous working day (Monday-Friday) from a given date
    /// </summary>
    /// <param name="fromDate">Date to calculate from</param>
    /// <returns>Previous working day</returns>
    public static DateTime GetPreviousWorkingDay(DateTime fromDate)
    {
        var date = fromDate.AddDays(-1);
        
        // Keep going back until we find a weekday
        while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
        {
            date = date.AddDays(-1);
        }
        
        return date;
    }

    /// <summary>
    /// Get the previous working day (Monday to Friday) from the given date
    /// </summary>
    /// <param name="fromDate">The date to calculate from (defaults to today)</param>
    /// <returns>Previous working day as DateOnly</returns>
    public static DateOnly GetPreviousWorkingDay(DateOnly? fromDate = null)
    {
        var date = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        
        // Go back one day
        date = date.AddDays(-1);
        
        // Keep going back until we hit a weekday (Monday = 1, Friday = 5)
        while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
        {
            date = date.AddDays(-1);
        }
        
        return date;
    }

    /// <summary>
    /// Get the previous working day as DateTime
    /// </summary>
    /// <param name="fromDate">The date to calculate from (defaults to today)</param>
    /// <returns>Previous working day as DateTime</returns>
    public static DateTime GetPreviousWorkingDateTime(DateTime? fromDate = null)
    {
        var date = fromDate ?? DateTime.UtcNow;
        var dateOnly = DateOnly.FromDateTime(date);
        var previousWorkingDay = GetPreviousWorkingDay(dateOnly);
        return previousWorkingDay.ToDateTime(TimeOnly.MinValue);
    }

    /// <summary>
    /// Get the previous working day formatted for API calls
    /// </summary>
    /// <param name="fromDate">The date to calculate from (defaults to today)</param>
    /// <returns>Previous working day in yyyy-MM-dd format</returns>
    public static string GetPreviousWorkingDayForApi(DateOnly? fromDate = null)
    {
        var previousWorkingDay = GetPreviousWorkingDay(fromDate);
        return FormatForApi(previousWorkingDay);
    }
}