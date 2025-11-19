using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai.Guardrails;

/// <summary>
/// Output validation guardrails for AI agent responses
/// Ensures responses are appropriate for financial advisory context
/// </summary>
public class OutputValidationGuardrails
{
    private readonly ILogger<OutputValidationGuardrails> _logger;

    public OutputValidationGuardrails(ILogger<OutputValidationGuardrails> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validate AI agent output before sending to user
    /// </summary>
    public GuardrailValidationResult ValidateOutput(string output, int accountId, string originalQuery)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return new GuardrailValidationResult 
            { 
                IsValid = false, 
                Reason = "Empty response generated",
                Severity = GuardrailSeverity.Medium 
            };
        }

        // Check for prompt leakage
        var promptLeakageResult = CheckForPromptLeakage(output, accountId, originalQuery);
        if (!promptLeakageResult.IsValid)
        {
            return promptLeakageResult;
        }

        return new GuardrailValidationResult { IsValid = true };
    }


    /// <summary>
    /// Check for prompt or system information leakage
    /// </summary>
    private GuardrailValidationResult CheckForPromptLeakage(string output, int accountId, string query)
    {
        // Look for signs of prompt injection success or system information leakage
        var patterns = new[]
        {
            @"system\s*:\s*you\s+are",
            @"instructions\s*:\s*you\s+are",
            @"assistant\s*:\s*you\s+are",
            @"role\s*:\s*you\s+are",
            @"prompt\s*:\s*you\s+are",
            @"\\[SYSTEM\\]",
            @"\\[ASSISTANT\\]",
            @"\\[USER\\]",
            @"<\\|.*\\|>",
            @"my\s+instructions\s+(are|say)",
            @"i\s+was\s+(told|instructed)\s+to"
        };

        var lowerOutput = output.ToLowerInvariant();

        foreach (var pattern in patterns)
        {
            if (Regex.IsMatch(lowerOutput, pattern, RegexOptions.IgnoreCase))
            {
                _logger.LogError("Potential prompt leakage detected in output for account {AccountId}. Pattern: '{Pattern}', Query: '{Query}'", 
                    accountId, pattern, query);
                
                return new GuardrailValidationResult 
                { 
                    IsValid = false, 
                    Reason = "I apologize, but I encountered an issue processing your request. Please try rephrasing your question about your portfolio.",
                    Severity = GuardrailSeverity.Critical,
                    ViolationType = GuardrailViolationType.PromptLeakage
                };
            }
        }

        return new GuardrailValidationResult { IsValid = true };
    }


    /// <summary>
    /// Sanitize output by removing or replacing problematic content
    /// </summary>
    public string SanitizeOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return "I apologize, but I couldn't generate a proper response. Please try rephrasing your question.";
        }

        // Remove potential prompt markers
        var sanitized = Regex.Replace(output, @"(\[SYSTEM\]|\[ASSISTANT\]|\[USER\])", "", RegexOptions.IgnoreCase);
        
        // Remove special tokens
        sanitized = Regex.Replace(sanitized, @"<\|.*?\|>", "", RegexOptions.IgnoreCase);
        
        // Clean up extra whitespace
        sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim();

        return sanitized;
    }
}

/// <summary>
/// Additional guardrail violation types for output validation
/// </summary>
public enum OutputGuardrailViolationType
{
    InappropriateFinancialContent,
    RiskyFinancialAdvice,
    PromptLeakage,
    SystemInformationLeak,
    MissingDisclaimer
}