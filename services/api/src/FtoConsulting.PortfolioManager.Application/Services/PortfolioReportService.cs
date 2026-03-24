using System.Text;
using Azure.Communication.Email;
using FtoConsulting.PortfolioManager.Application.Configuration;
using FtoConsulting.PortfolioManager.Application.DTOs;
using FtoConsulting.PortfolioManager.Application.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Generates AI-powered portfolio reports by reusing the existing chat agent,
/// then sends the result as an HTML email via Azure Communication Services.
/// </summary>
public class PortfolioReportService : IPortfolioReportService
{
    private readonly ILogger<PortfolioReportService> _logger;
    private readonly PortfolioReportOptions _options;
    private readonly IAiOrchestrationService _aiOrchestrationService;

    public PortfolioReportService(
        ILogger<PortfolioReportService> logger,
        IOptions<PortfolioReportOptions> options,
        IAiOrchestrationService aiOrchestrationService)
    {
        _logger = logger;
        _options = options.Value;
        _aiOrchestrationService = aiOrchestrationService;
    }

    public async Task<PortfolioReportDto> GenerateAndSendReportAsync(
        ReportType reportType,
        int accountId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating {ReportType} portfolio report for account {AccountId}", reportType, accountId);

        // Build the prompt — the agent will call all the right tools itself
        var prompt = reportType == ReportType.Morning
            ? "Generate a concise morning portfolio briefing for me. Check my current holdings with live prices, " +
              "identify any significant movers, search for relevant news on those movers only, and give me a " +
              "150-200 word summary I can read in under a minute. Focus on what matters for today's session."
            : "Generate a concise end-of-day portfolio summary for me. Check my current holdings with live prices, " +
              "identify today's significant movers, search for relevant news explaining what drove them, and give me a " +
              "150-200 word summary. Highlight anything I should watch heading into tomorrow.";

        // Stream the agent response into a StringBuilder — same pipeline as the chat UI
        var narrativeBuilder = new StringBuilder();
        await _aiOrchestrationService.ProcessPortfolioQueryAsync(
            query: prompt,
            accountId: accountId,
            onStatusUpdate: null,
            onTokenReceived: token =>
            {
                narrativeBuilder.Append(token);
                return Task.CompletedTask;
            },
            threadId: null,
            modelId: null,
            cancellationToken: cancellationToken);

        var narrative = narrativeBuilder.ToString().Trim();
        if (string.IsNullOrEmpty(narrative))
        {
            narrative = "Portfolio report generated. Agent returned no content.";
        }

        _logger.LogInformation("{ReportType} report narrative generated ({Length} chars)", reportType, narrative.Length);

        // Wrap in a simple HTML email shell
        var reportHtml = RenderHtmlEmail(reportType, narrative);

        // Send via SendGrid
        var (emailSent, emailError) = await SendEmailAsync(reportType, reportHtml, cancellationToken);

        return new PortfolioReportDto(
            ReportType: reportType,
            GeneratedAt: DateTime.UtcNow,
            AccountId: accountId,
            AiNarrative: narrative,
            ReportHtml: reportHtml,
            RecipientEmail: _options.RecipientEmail,
            EmailSent: emailSent,
            EmailError: emailError
        );
    }

    // -------------------------------------------------------------------------
    // HTML rendering
    // -------------------------------------------------------------------------

    private static string RenderHtmlEmail(ReportType reportType, string narrative)
    {
        var title = reportType == ReportType.Morning
            ? "📈 Morning Portfolio Briefing"
            : "📊 Evening Portfolio Summary";
        var now = DateTime.UtcNow;

        // Convert plain line breaks to <br/> and escape HTML entities in the narrative
        var safeNarrative = HtmlEncode(narrative).Replace("\n", "<br/>");

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head>");
        sb.Append("<meta charset=\"UTF-8\"/>");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"/>");
        sb.Append("<style>");
        sb.Append("body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;background:#f4f6f9;margin:0;padding:0;color:#333}");
        sb.Append(".wrapper{max-width:620px;margin:24px auto;background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.1)}");
        sb.Append(".header{background:#1a2f5e;color:#fff;padding:24px 32px}");
        sb.Append(".header h1{margin:0 0 4px;font-size:20px;font-weight:600}");
        sb.Append(".header p{margin:0;font-size:13px;opacity:.8}");
        sb.Append(".body{padding:28px 32px;font-size:15px;line-height:1.75;color:#444}");
        sb.Append(".footer{text-align:center;padding:16px;font-size:11px;color:#aaa;background:#f8f9fb;border-top:1px solid #e5e8ef}");
        sb.Append("</style></head><body><div class=\"wrapper\">");
        sb.Append($"<div class=\"header\"><h1>{title}</h1>");
        sb.Append($"<p>{now:dddd, d MMMM yyyy} &middot; {now:HH:mm} UTC</p></div>");
        sb.Append($"<div class=\"body\">{safeNarrative}</div>");
        sb.Append($"<div class=\"footer\">Portfolio Manager &middot; Automated Report &middot; {now.Year} &middot; Not financial advice.</div>");
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Email delivery
    // -------------------------------------------------------------------------

    private async Task<(bool Sent, string? Error)> SendEmailAsync(
        ReportType reportType,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.AcsConnectionString))
        {
            _logger.LogWarning("ACS connection string not configured — skipping email send");
            return (false, "ACS connection string not configured");
        }

        if (string.IsNullOrEmpty(_options.RecipientEmail))
        {
            _logger.LogWarning("Recipient email not configured — skipping email send");
            return (false, "Recipient email not configured");
        }

        try
        {
            var subject = reportType == ReportType.Morning
                ? $"📈 Morning Portfolio Briefing — {DateTime.UtcNow:d MMM yyyy}"
                : $"📊 Evening Portfolio Summary — {DateTime.UtcNow:d MMM yyyy}";

            var emailClient = new EmailClient(_options.AcsConnectionString);

            var message = new EmailMessage(
                senderAddress: _options.SenderEmail,
                recipients: new EmailRecipients([new EmailAddress(_options.RecipientEmail)]),
                content: new EmailContent(subject) { Html = htmlBody });

            var operation = await emailClient.SendAsync(
                Azure.WaitUntil.Completed, message, cancellationToken);

            if (operation.HasValue && operation.Value.Status == EmailSendStatus.Succeeded)
            {
                _logger.LogInformation("Report email sent to {Recipient}", _options.RecipientEmail);
                return (true, null);
            }

            var status = operation.HasValue ? operation.Value.Status.ToString() : "Unknown";
            _logger.LogError("ACS email send failed with status: {Status}", status);
            return (false, $"ACS send status: {status}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send report email via ACS");
            return (false, ex.Message);
        }
    }

    private static string HtmlEncode(string text)
        => text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
}
