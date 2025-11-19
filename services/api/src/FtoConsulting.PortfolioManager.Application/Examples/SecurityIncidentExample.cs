using System.Text.Json;
using FtoConsulting.PortfolioManager.Application.Services.Ai.Guardrails;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FtoConsulting.PortfolioManager.Application.Examples;

/// <summary>
/// Example demonstrating the new Security Incident database logging functionality
/// Shows how guardrail violations are now persisted to the database for audit and monitoring
/// </summary>
public class SecurityIncidentExample
{
    private readonly ISecurityIncidentRepository _securityIncidentRepository;
    private readonly ILogger<SecurityIncidentExample> _logger;

    public SecurityIncidentExample(
        ISecurityIncidentRepository securityIncidentRepository,
        ILogger<SecurityIncidentExample> logger)
    {
        _securityIncidentRepository = securityIncidentRepository;
        _logger = logger;
    }

    /// <summary>
    /// Example of manually creating and logging a security incident
    /// This would typically be done automatically by the guardrails system
    /// </summary>
    public async Task<SecurityIncident> CreateExampleSecurityIncident(int accountId)
    {
        // Create a security incident (normally done by AgentFrameworkGuardrails.LogSecurityIncident)
        var incident = new SecurityIncident(
            accountId: accountId,
            violationType: GuardrailViolationType.PromptInjection.ToString(),
            severity: GuardrailSeverity.High.ToString(),
            reason: "Detected potential prompt injection attempt with system role manipulation",
            context: "User query contained suspicious patterns attempting to override AI instructions",
            threatLevel: "HIGH",
            additionalData: JsonSerializer.Serialize(new
            {
                DetectionTimestamp = DateTime.UtcNow,
                UserAgent = "Portfolio Management AI Chat",
                SessionInfo = "Example session for demonstration"
            })
        );

        // Save to database
        var savedIncident = await _securityIncidentRepository.AddAsync(incident);
        
        _logger.LogWarning("Example security incident created: {IncidentId} for account {AccountId}", 
            savedIncident.Id, accountId);

        return savedIncident;
    }

    /// <summary>
    /// Example of querying security incidents for monitoring
    /// </summary>
    public async Task<SecurityIncidentSummary> GetAccountSecuritySummary(int accountId)
    {
        // Get all incidents for the account
        var allIncidents = await _securityIncidentRepository.GetByAccountIdAsync(accountId);
        
        // Get unresolved incidents
        var unresolvedIncidents = await _securityIncidentRepository.GetUnresolvedIncidentsAsync();
        var accountUnresolved = unresolvedIncidents.Where(i => i.AccountId == accountId);

        // Get recent high-threat incidents
        var recentHighThreats = await _securityIncidentRepository.GetRecentHighThreatIncidentsAsync(24);
        var accountHighThreats = recentHighThreats.Where(i => i.AccountId == accountId);

        // Get counts by severity
        var severityCounts = await _securityIncidentRepository.GetIncidentCountsBySeverityAsync();

        return new SecurityIncidentSummary
        {
            AccountId = accountId,
            TotalIncidents = allIncidents.Count(),
            UnresolvedIncidents = accountUnresolved.Count(),
            RecentHighThreatIncidents = accountHighThreats.Count(),
            SeverityCounts = severityCounts,
            LastIncidentDate = allIncidents.OrderByDescending(i => i.CreatedAt).FirstOrDefault()?.CreatedAt
        };
    }

    /// <summary>
    /// Example of resolving a security incident
    /// </summary>
    public async Task<SecurityIncident> ResolveSecurityIncident(int incidentId, string resolution)
    {
        var incident = await _securityIncidentRepository.GetByIdAsync(incidentId);
        if (incident == null)
        {
            throw new ArgumentException($"Security incident {incidentId} not found");
        }

        // Mark as resolved
        incident.Resolve(resolution);
        
        await _securityIncidentRepository.UpdateAsync(incident);

        _logger.LogInformation("Security incident {IncidentId} resolved: {Resolution}", 
            incidentId, resolution);

        return incident;
    }
}

/// <summary>
/// Summary of security incidents for an account
/// </summary>
public class SecurityIncidentSummary
{
    public int AccountId { get; set; }
    public int TotalIncidents { get; set; }
    public int UnresolvedIncidents { get; set; }
    public int RecentHighThreatIncidents { get; set; }
    public Dictionary<string, int> SeverityCounts { get; set; } = new();
    public DateTime? LastIncidentDate { get; set; }
}