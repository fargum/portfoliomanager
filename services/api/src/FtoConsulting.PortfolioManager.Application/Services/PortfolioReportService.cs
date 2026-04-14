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
    // Per-type semaphore: prevents a second report being generated while the first
    // is still in-flight (e.g. Logic App retry fired before prior request completed).
    private static readonly Dictionary<ReportType, SemaphoreSlim> s_reportLocks = new()
    {
        [ReportType.Morning] = new SemaphoreSlim(1, 1),
        [ReportType.Evening] = new SemaphoreSlim(1, 1),
    };

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
        var semaphore = s_reportLocks[reportType];
        if (!await semaphore.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning(
                "{ReportType} report request rejected — a report generation is already in progress for account {AccountId}",
                reportType, accountId);
            throw new ReportAlreadyInProgressException(reportType);
        }

        try
        {
        _logger.LogInformation("Generating {ReportType} portfolio report for account {AccountId}", reportType, accountId);

        // Build the prompt — the agent will call all the right tools itself
        var prompt = reportType == ReportType.Morning
            ? "Generate a morning portfolio briefing. Follow this exact structure — do not deviate, do not add a closing question or offer further help:\n\n" +
              "1. **Portfolio Snapshot** — total portfolio value in GBP, today's overall gain/loss in GBP and percent.\n\n" +
              "2. **Top 5 Gainers** — format as an HTML table with columns: Holding | Price | Today (GBP) | Today (%)\n\n" +
              "3. **Top 5 Losers** — same HTML table format.\n\n" +
              "4. **Market & News Summary** — 3-4 sentences on what is driving markets today and any relevant news for the above movers. Include hyperlinks using HTML <a href='url'>text</a> format.\n\n" +
              "Rules: output plain text and HTML only — no markdown, no asterisks, no hash headings, no bullet dashes. Use <strong> for bold. Do not end with a question or offer of further assistance."
            : "Generate an end-of-day portfolio summary. Follow this exact structure — do not deviate, do not add a closing question or offer further help:\n\n" +
              "1. **Portfolio Snapshot** — total portfolio value in GBP, today's overall gain/loss in GBP and percent, and overall gain/loss since cost.\n\n" +
              "2. **Top 5 Gainers today** — format as an HTML table with columns: Holding | Price | Today (GBP) | Today (%)\n\n" +
              "3. **Top 5 Losers today** — same HTML table format.\n\n" +
              "4. **Market & News Summary** — 3-4 sentences on what drove markets today and any relevant news for the above movers. Include hyperlinks using HTML <a href='url'>text</a> format.\n\n" +
              "5. **Watch list for tomorrow** — 2-3 bullet points on what to watch overnight or at open.\n\n" +
              "Rules: output plain text and HTML only — no markdown, no asterisks, no hash headings, no bullet dashes. Use <strong> for bold. Do not end with a question or offer of further assistance.";

        // Stream the agent response into a StringBuilder — same pipeline as the chat UI,
        // but with storeInHistory:false so the HTML-format prompt/response never enters
        // the user's active conversation history (which would cause the chat UI to start
        // rendering raw HTML).
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
            modelId: "grok-4-fast-reasoning",
            storeInHistory: false,
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
        finally
        {
            semaphore.Release();
        }
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

        // The AI is instructed to emit HTML directly — just sanitise stray markdown that may slip through
        var safeNarrative = SanitiseNarrative(narrative);

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
        sb.Append(".body h2{font-size:16px;font-weight:600;color:#1a2f5e;margin:20px 0 6px}");
        sb.Append(".body table{width:100%;border-collapse:collapse;margin:8px 0 16px;font-size:14px}");
        sb.Append(".body th{background:#1a2f5e;color:#fff;padding:7px 10px;text-align:left;font-weight:600}");
        sb.Append(".body td{padding:6px 10px;border-bottom:1px solid #e5e8ef}");
        sb.Append(".body tr:nth-child(even) td{background:#f8f9fb}");
        sb.Append(".body a{color:#1a2f5e}");
        sb.Append(".body ul{margin:6px 0 12px;padding-left:20px}");
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

    // The AI is instructed to return HTML, but may still emit some stray markdown.
    // Convert residual markdown to HTML rather than HTML-encoding the whole string.
    private static string SanitiseNarrative(string text)
    {
        // Normalise line endings — LLMs often return \r\n (CRLF) which breaks
        // the newline-stripping regexes below that only match \n.
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        // Markdown links [text](url) → <a href="url">text</a>
        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"\[([^\]]+)\]\((https?://[^\)]+)\)",
            "<a href=\"$2\">$1</a>");

        // **bold** → <strong>bold</strong>
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");

        // ## Heading → <h2>Heading</h2>  (strip leading #)
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"(?m)^#{1,3}\s+(.+)$", "<h2>$1</h2>");

        // Strip newlines that are adjacent to block-level HTML elements —
        // these produce unwanted whitespace gaps in email clients.
        // Remove: newline immediately before a block-level opening or closing tag
        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"\n+(?=<(?:/)?(?:h[1-6]|table|thead|tbody|tr|th|td|ul|ol|li|div|p|strong)\b)",
            " ");

        // Remove: newline immediately after a block-level closing tag
        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"(</(?:h[1-6]|table|thead|tbody|tr|th|td|ul|ol|li|div|p)>)\n+",
            "$1");

        // Remaining bare newlines (prose paragraphs) → <br/>
        text = text.Replace("\n", "<br/>");

        // Remove <br/> tags immediately before block-level elements.
        // LLMs often emit literal <br/> tags (not just \n) between headings and
        // tables, which the newline-stripping passes above don't catch.
        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"(<br\s*/?>\s*)+(?=<(?:table|thead|tbody|tr|th|td|ul|ol|li|div|p|h[1-6])\b)",
            "");

        // Remove <br/> tags immediately after block-level closing tags.
        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"(?<=</(?:table|thead|tbody|tr|th|td|ul|ol|li|div|p|h[1-6])>)(\s*<br\s*/?>\s*)+",
            "");

        return text;
    }
}
