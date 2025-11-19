using Microsoft.EntityFrameworkCore;
using FtoConsulting.PortfolioManager.Domain.Entities;
using FtoConsulting.PortfolioManager.Domain.Repositories;
using FtoConsulting.PortfolioManager.Infrastructure.Data;

namespace FtoConsulting.PortfolioManager.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SecurityIncident entities
/// Provides specialized methods for security incident management and monitoring
/// </summary>
public class SecurityIncidentRepository : Repository<SecurityIncident>, ISecurityIncidentRepository
{
    public SecurityIncidentRepository(PortfolioManagerDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Get all security incidents for a specific account
    /// </summary>
    public async Task<IEnumerable<SecurityIncident>> GetByAccountIdAsync(int accountId)
    {
        return await _dbSet
            .Where(si => si.AccountId == accountId)
            .OrderByDescending(si => si.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get unresolved security incidents
    /// </summary>
    public async Task<IEnumerable<SecurityIncident>> GetUnresolvedIncidentsAsync()
    {
        return await _dbSet
            .Where(si => !si.IsResolved)
            .OrderByDescending(si => si.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get security incidents by threat level
    /// </summary>
    public async Task<IEnumerable<SecurityIncident>> GetByThreatLevelAsync(string threatLevel)
    {
        return await _dbSet
            .Where(si => si.ThreatLevel == threatLevel)
            .OrderByDescending(si => si.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get security incidents by violation type
    /// </summary>
    public async Task<IEnumerable<SecurityIncident>> GetByViolationTypeAsync(string violationType)
    {
        return await _dbSet
            .Where(si => si.ViolationType == violationType)
            .OrderByDescending(si => si.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get security incidents within a date range
    /// </summary>
    public async Task<IEnumerable<SecurityIncident>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(si => si.CreatedAt >= startDate && si.CreatedAt <= endDate)
            .OrderByDescending(si => si.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get count of incidents by severity for monitoring
    /// </summary>
    public async Task<Dictionary<string, int>> GetIncidentCountsBySeverityAsync(DateTime? since = null)
    {
        var query = _dbSet.AsQueryable();
        
        if (since.HasValue)
        {
            query = query.Where(si => si.CreatedAt >= since.Value);
        }

        return await query
            .GroupBy(si => si.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Severity, x => x.Count);
    }

    /// <summary>
    /// Get recent high-threat incidents for monitoring
    /// </summary>
    public async Task<IEnumerable<SecurityIncident>> GetRecentHighThreatIncidentsAsync(int hours = 24)
    {
        var cutoffTime = DateTime.UtcNow.AddHours(-hours);
        
        return await _dbSet
            .Where(si => si.CreatedAt >= cutoffTime && si.ThreatLevel == "HIGH")
            .OrderByDescending(si => si.CreatedAt)
            .ToListAsync();
    }
}