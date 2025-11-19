using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Entities;

/// <summary>
/// Entity representing AI guardrail security incidents
/// Tracks violations and security events for audit and analysis
/// </summary>
public class SecurityIncident : BaseEntity
{
    public int AccountId { get; private set; }
    public string ViolationType { get; private set; } = string.Empty;
    public string Severity { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string Context { get; private set; } = string.Empty;
    public string ThreatLevel { get; private set; } = string.Empty;
    public string? AdditionalData { get; private set; }
    public bool IsResolved { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public string? Resolution { get; private set; }

    // Private constructor for EF Core
    private SecurityIncident() { }

    /// <summary>
    /// Create a new security incident
    /// </summary>
    public SecurityIncident(
        int accountId,
        string violationType,
        string severity,
        string reason,
        string context,
        string threatLevel,
        string? additionalData = null)
    {
        AccountId = accountId;
        ViolationType = violationType ?? throw new ArgumentNullException(nameof(violationType));
        Severity = severity ?? throw new ArgumentNullException(nameof(severity));
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        Context = context ?? throw new ArgumentNullException(nameof(context));
        ThreatLevel = threatLevel ?? throw new ArgumentNullException(nameof(threatLevel));
        AdditionalData = additionalData;
        IsResolved = false;
    }

    /// <summary>
    /// Mark the incident as resolved
    /// </summary>
    public void Resolve(string resolution)
    {
        if (IsResolved)
            throw new InvalidOperationException("Incident is already resolved");

        IsResolved = true;
        ResolvedAt = DateTime.UtcNow;
        Resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
        SetUpdatedAt();
    }

    /// <summary>
    /// Update additional data for the incident
    /// </summary>
    public void UpdateAdditionalData(string additionalData)
    {
        AdditionalData = additionalData;
        SetUpdatedAt();
    }
}