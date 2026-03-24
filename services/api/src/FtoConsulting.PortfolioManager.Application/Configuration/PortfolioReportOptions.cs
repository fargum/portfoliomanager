namespace FtoConsulting.PortfolioManager.Application.Configuration;

/// <summary>
/// Configuration options for scheduled portfolio email reports
/// </summary>
public class PortfolioReportOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "PortfolioReport";

    /// <summary>
    /// Azure Communication Services connection string
    /// </summary>
    public string AcsConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Sender email address (the DoNotReply@....azurecomm.net address from your managed domain)
    /// </summary>
    public string SenderEmail { get; set; } = string.Empty;

    /// <summary>
    /// Sender display name
    /// </summary>
    public string SenderName { get; set; } = "Portfolio Manager";

    /// <summary>
    /// Email address to send portfolio reports to
    /// </summary>
    public string RecipientEmail { get; set; } = string.Empty;

    /// <summary>
    /// Whether morning reports are enabled
    /// </summary>
    public bool MorningReportEnabled { get; set; } = true;

    /// <summary>
    /// Whether evening reports are enabled
    /// </summary>
    public bool EveningReportEnabled { get; set; } = true;
}
