namespace FtoConsulting.PortfolioManager.Application.DTOs;

/// <summary>
/// Identifies whether this is a morning (market open) or evening (market close) report
/// </summary>
public enum ReportType
{
    Morning,
    Evening
}

/// <summary>
/// Result of generating and (optionally) sending a portfolio report
/// </summary>
public record PortfolioReportDto(
    ReportType ReportType,
    DateTime GeneratedAt,
    int AccountId,
    string AiNarrative,
    string ReportHtml,
    string RecipientEmail,
    bool EmailSent,
    string? EmailError
);
