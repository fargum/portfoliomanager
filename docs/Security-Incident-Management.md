# Security Incident Management

## Overview

The Portfolio Manager now includes comprehensive security incident management for AI guardrail violations. When the AI guardrails detect potentially harmful or inappropriate input/output, security incidents are automatically logged to the database for audit trails and security monitoring.

## Key Components

### 1. SecurityIncident Entity
- **Database Table**: `app.security_incidents`
- **Purpose**: Persistent storage of all guardrail violations
- **Key Fields**:
  - `account_id`: Links incident to specific user account
  - `violation_type`: Type of guardrail violation (PromptInjection, InappropriateFinancialRequest, etc.)
  - `severity`: Severity level (Low, Medium, High, Critical)
  - `threat_level`: HIGH/MEDIUM threat classification
  - `is_resolved`: Whether incident has been addressed
  - `additional_data`: JSON metadata for detailed analysis

### 2. Enhanced AgentFrameworkGuardrails
The `LogSecurityIncident` method has been upgraded to:
- **Log to Database**: Incidents are persisted to `security_incidents` table
- **Fallback Logging**: If database write fails, falls back to structured logging
- **Async Operation**: Uses async pattern for database operations
- **Rich Metadata**: Stores JSON additional data for detailed analysis

### 3. SecurityIncidentRepository
Specialized repository providing:
- **Account-specific queries**: Get incidents by account ID
- **Threat monitoring**: Query by threat level and severity
- **Date range filtering**: Incidents within specific time periods
- **Aggregated reporting**: Counts by severity for dashboards
- **Performance optimized**: With database indexes for fast queries

## Database Schema

```sql
CREATE TABLE app.security_incidents (
    id SERIAL PRIMARY KEY,
    account_id INTEGER NOT NULL REFERENCES app.accounts(id),
    violation_type VARCHAR(100) NOT NULL,
    severity VARCHAR(20) NOT NULL,
    reason VARCHAR(1000) NOT NULL,
    context VARCHAR(2000) NOT NULL,
    threat_level VARCHAR(20) NOT NULL,
    additional_data JSONB,
    is_resolved BOOLEAN NOT NULL DEFAULT FALSE,
    resolved_at TIMESTAMP WITH TIME ZONE,
    resolution VARCHAR(1000),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE
);

-- Key indexes for performance
CREATE INDEX ix_security_incidents_account_id ON app.security_incidents(account_id);
CREATE INDEX ix_security_incidents_threat_level ON app.security_incidents(threat_level);
CREATE INDEX ix_security_incidents_severity ON app.security_incidents(severity);
CREATE INDEX ix_security_incidents_created_at ON app.security_incidents(created_at);
```

## Usage Examples

### Automatic Incident Logging
Security incidents are automatically logged when guardrails detect violations:

```csharp
// In AiOrchestrationService - automatic logging
var inputValidation = await _guardrails.ValidateInputAsync(query, accountId);
if (!inputValidation.IsValid)
{
    // This automatically creates a database record
    await _guardrails.LogSecurityIncident(inputValidation, accountId, "ProcessPortfolioQueryWithMemoryAsync");
    return new ChatResponseDto(
        Response: _guardrails.CreateFallbackResponse(inputValidation, accountId),
        QueryType: "SecurityFiltered"
    );
}
```

### Querying Security Incidents
```csharp
// Get all incidents for an account
var incidents = await _securityIncidentRepository.GetByAccountIdAsync(accountId);

// Get unresolved high-threat incidents
var unresolvedIncidents = await _securityIncidentRepository.GetUnresolvedIncidentsAsync();
var highThreats = unresolvedIncidents.Where(i => i.ThreatLevel == "HIGH");

// Get incident counts for monitoring dashboard
var severityCounts = await _securityIncidentRepository.GetIncidentCountsBySeverityAsync();
```

### Resolving Incidents
```csharp
// Mark incident as resolved
var incident = await _securityIncidentRepository.GetByIdAsync(incidentId);
incident.Resolve("Confirmed false positive - user query was legitimate portfolio analysis");
await _securityIncidentRepository.UpdateAsync(incident);
await _unitOfWork.SaveChangesAsync();
```

## Security Monitoring

### Threat Levels
- **HIGH**: Critical violations requiring immediate attention
  - Prompt injection attempts
  - Attempts to bypass financial guardrails
  - Suspicious encoding or obfuscation
- **MEDIUM**: Violations requiring monitoring
  - Borderline inappropriate requests
  - Minor guardrail rule violations

### Recommended Monitoring
1. **Real-time Alerts**: Monitor HIGH threat level incidents
2. **Daily Reports**: Summary of incidents by account and severity
3. **Trend Analysis**: Track incident patterns over time
4. **Account Flagging**: Accounts with multiple HIGH incidents

## Integration Points

### Guardrails Integration
- All guardrail violations with Medium+ severity are automatically logged
- Input validation failures logged before request processing
- Output validation failures logged before response delivery
- Rich context information captured for analysis

### Repository Pattern
- Follows existing repository patterns in the application
- Registered in dependency injection container
- Supports Unit of Work pattern for transactions

### Audit Trail
- Complete audit trail of all security incidents
- Immutable incident records (resolved incidents maintain history)
- JSON metadata for extensible incident details
- Links to account for user tracking

## Performance Considerations

- Database indexes optimize common query patterns
- Async operations prevent blocking
- Fallback to logging if database operations fail
- Efficient JSON storage for metadata

This security incident management system provides enterprise-grade audit trails and monitoring capabilities for the AI guardrails system, ensuring comprehensive security oversight of AI interactions.