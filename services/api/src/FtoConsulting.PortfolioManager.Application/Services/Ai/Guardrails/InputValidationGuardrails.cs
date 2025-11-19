using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace FtoConsulting.PortfolioManager.Application.Services.Ai.Guardrails;

/// <summary>
/// Input validation guardrails for portfolio management AI agent
/// Protects against prompt injection, jailbreaking, and malicious inputs
/// </summary>
public class InputValidationGuardrails
{
    private readonly ILogger<InputValidationGuardrails> _logger;

    // Patterns that suggest prompt injection attempts
    private static readonly string[] SuspiciousPatterns = new[]
    {
        @"ignore\s+(previous|all|above)\s+instructions",
        @"you\s+are\s+now\s+a\s+different",
        @"forget\s+(everything|all|instructions)",
        @"system\s*:\s*you\s+are",
        @"pretend\s+to\s+be",
        @"role\s*:\s*you\s+are",
        @"new\s+system\s+message",
        @"override\s+(your|the)\s+instructions",
        @"act\s+as\s+if\s+you\s+are",
        @"simulate\s+(being|a)",
        @"return\s+raw\s+data",
        @"show\s+(me\s+)?your\s+(prompt|instructions|system)",
        @"what\s+(are\s+)?your\s+(instructions|rules)",
        @"tell\s+me\s+your\s+secret",
        @"bypass\s+(security|safety|restrictions)",
        @"malicious|harmful|dangerous",
        @"<!--.*-->",  // HTML comments that might hide instructions
        @"<script.*?>.*?</script>",  // Script tags
        @"javascript:",  // JavaScript protocols
        @"data:text/html"  // Data URLs
    };


    public InputValidationGuardrails(ILogger<InputValidationGuardrails> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validate user input for security and appropriateness
    /// </summary>
    public GuardrailValidationResult ValidateInput(string input, int accountId)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new GuardrailValidationResult 
            { 
                IsValid = false, 
                Reason = "Empty input provided",
                Severity = GuardrailSeverity.Low 
            };
        }

        // Check input length
        if (input.Length > 5000)
        {
            _logger.LogWarning("Oversized input detected for account {AccountId}: {Length} characters", 
                accountId, input.Length);
            
            return new GuardrailValidationResult 
            { 
                IsValid = false, 
                Reason = "Input too long. Please keep your question under 5000 characters.",
                Severity = GuardrailSeverity.Medium 
            };
        }

        // Check for prompt injection patterns
        var injectionResult = CheckForPromptInjection(input, accountId);
        if (!injectionResult.IsValid)
        {
            return injectionResult;
        }

        // Check for excessive special characters (might indicate encoding attacks)
        var specialCharResult = CheckSpecialCharacters(input, accountId);
        if (!specialCharResult.IsValid)
        {
            return specialCharResult;
        }

        return new GuardrailValidationResult { IsValid = true };
    }

    /// <summary>
    /// Check for prompt injection attempts
    /// </summary>
    private GuardrailValidationResult CheckForPromptInjection(string input, int accountId)
    {
        var lowerInput = input.ToLowerInvariant();

        foreach (var pattern in SuspiciousPatterns)
        {
            if (Regex.IsMatch(lowerInput, pattern, RegexOptions.IgnoreCase))
            {
                _logger.LogWarning("Potential prompt injection detected for account {AccountId}: Pattern '{Pattern}' matched in input", 
                    accountId, pattern);
                
                return new GuardrailValidationResult 
                { 
                    IsValid = false, 
                    Reason = "I can't process that request. Please ask a straightforward question about your portfolio.",
                    Severity = GuardrailSeverity.High,
                    ViolationType = GuardrailViolationType.PromptInjection
                };
            }
        }

        return new GuardrailValidationResult { IsValid = true };
    }


    /// <summary>
    /// Check for suspicious character patterns
    /// </summary>
    private GuardrailValidationResult CheckSpecialCharacters(string input, int accountId)
    {
        // Count special characters and control characters
        var specialCharCount = input.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && !char.IsPunctuation(c));
        var controlCharCount = input.Count(char.IsControl);

        // If more than 20% of the input is special/control characters, it might be an attack
        var totalChars = input.Length;
        var suspiciousCharRatio = (double)(specialCharCount + controlCharCount) / totalChars;

        if (suspiciousCharRatio > 0.2 && totalChars > 50)
        {
            _logger.LogWarning("High ratio of special characters detected for account {AccountId}: {Ratio:P2} ({Count}/{Total})", 
                accountId, suspiciousCharRatio, specialCharCount + controlCharCount, totalChars);
            
            return new GuardrailValidationResult 
            { 
                IsValid = false, 
                Reason = "Your message contains unusual characters. Please use normal text for your portfolio questions.",
                Severity = GuardrailSeverity.Medium,
                ViolationType = GuardrailViolationType.SuspiciousEncoding
            };
        }

        return new GuardrailValidationResult { IsValid = true };
    }
}

/// <summary>
/// Result of guardrail validation
/// </summary>
public class GuardrailValidationResult
{
    public bool IsValid { get; set; }
    public string? Reason { get; set; }
    public GuardrailSeverity Severity { get; set; } = GuardrailSeverity.Low;
    public GuardrailViolationType? ViolationType { get; set; }
}

/// <summary>
/// Severity levels for guardrail violations
/// </summary>
public enum GuardrailSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Types of guardrail violations
/// </summary>
public enum GuardrailViolationType
{
    PromptInjection,
    InappropriateFinancialRequest,
    SuspiciousEncoding,
    OutputManipulation,
    InappropriateFinancialContent,
    RiskyFinancialAdvice,
    PromptLeakage
}