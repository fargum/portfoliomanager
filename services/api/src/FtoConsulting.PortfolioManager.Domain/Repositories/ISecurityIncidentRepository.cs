using FtoConsulting.PortfolioManager.Domain.Entities;

namespace FtoConsulting.PortfolioManager.Domain.Repositories;

/// <summary>
/// Repository interface for SecurityIncident entities
/// Provides specialized methods for security incident management and monitoring
/// </summary>
public interface ISecurityIncidentRepository : IRepository<SecurityIncident>
{
    /// <summary>
    /// Get all security incidents for a specific account
    /// </summary>
    Task<IEnumerable<SecurityIncident>> GetByAccountIdAsync(int accountId);

    /// <summary>
    /// Get unresolved security incidents
    /// </summary>
    Task<IEnumerable<SecurityIncident>> GetUnresolvedIncidentsAsync();

    /// <summary>
    /// Get security incidents by threat level
    /// </summary>
    Task<IEnumerable<SecurityIncident>> GetByThreatLevelAsync(string threatLevel);

    /// <summary>
    /// Get security incidents by violation type
    /// </summary>
    Task<IEnumerable<SecurityIncident>> GetByViolationTypeAsync(string violationType);

    /// <summary>
    /// Get security incidents within a date range
    /// </summary>
    Task<IEnumerable<SecurityIncident>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Get count of incidents by severity for monitoring
    /// </summary>
    Task<Dictionary<string, int>> GetIncidentCountsBySeverityAsync(DateTime? since = null);

    /// <summary>
    /// Get recent high-threat incidents for monitoring
    /// </summary>
    Task<IEnumerable<SecurityIncident>> GetRecentHighThreatIncidentsAsync(int hours = 24);
}