# Portfolio Manager - OpenTelemetry & Observability

This guide explains how to use the OpenTelemetry telemetry and observability features implemented in Portfolio Manager.

## 🚀 Quick Start

### Development with Aspire Dashboard

1. **Start the development environment with telemetry:**
   ```bash
   docker-compose -f docker-compose.dev.yml up -d
   ```

2. **Access the Aspire Dashboard:**
   - Dashboard UI: http://localhost:18888
   - OTLP Endpoint: http://localhost:18889

3. **Test AI interactions:**
   - Make API calls to http://localhost:8080/api/ai/chat/query
   - Watch real-time telemetry in the Aspire Dashboard

## 📊 What's Instrumented

### Automatic Agent Framework Telemetry
- **Chat completions** - Azure OpenAI calls with timing and token usage
- **Tool executions** - MCP server tool calls and responses  
- **Agent conversations** - Full conversation flows with context
- **Memory operations** - Chat history and context retrieval

### Custom Business Logic Telemetry
- **Portfolio queries** - Account ID, query type, response metrics
- **Guardrail operations** - Input/output validation, security incidents
- **Streaming responses** - Real-time token delivery tracking
- **Memory context** - Conversation thread and history management

### HTTP & Infrastructure Telemetry
- **ASP.NET Core requests** - All API endpoints with timing
- **HTTP client calls** - Outbound requests to external services
- **Database operations** - Entity Framework queries (if enabled)

## 🏷️ Key Telemetry Tags

| Tag | Description | Example |
|-----|-------------|---------|
| `account.id` | Portfolio account identifier | `"1"` |
| `thread.id` | Conversation thread ID | `"12345"` |
| `query.type` | Type of portfolio query | `"Performance"`, `"Holdings"` |
| `query.length` | Query text length | `"45"` |
| `validation.result` | Guardrail validation outcome | `"passed"`, `"failed"` |
| `streaming` | Whether response is streaming | `"true"`, `"false"` |

## 🔧 Configuration

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `OpenTelemetry__OtlpEndpoint` | `http://localhost:18889` | OTLP endpoint for telemetry export |
| `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS` | `true` | Allow anonymous Aspire Dashboard access |

### Development vs Production

**Development (docker-compose.dev.yml):**
- Aspire Dashboard for local visualization
- Console exporter for debugging
- Sensitive data disabled for security

**Production (future):**
- Azure Application Insights integration
- Performance metrics and alerting
- Enhanced security and compliance

## 🔍 Using the Aspire Dashboard

### Navigation
1. **Traces** - View individual request traces with timing
2. **Logs** - Structured application logs with context
3. **Metrics** - Performance counters and custom metrics

### Key Trace Operations
- `ProcessPortfolioQueryWithMemory` - Main AI orchestration
- `agent.run` - Agent Framework execution  
- `chat.completion` - Azure OpenAI calls
- `tool.execute` - MCP tool executions

### Filtering Traces
- Filter by account ID: `account.id = "1"`
- Filter by query type: `query.type = "Performance"`
- Filter by errors: `otel.status_code = "ERROR"`

## 🚀 Next Steps

### Phase 2: Enhanced Observability
- Custom metrics for business KPIs
- Security incident alerting
- Performance baseline monitoring

### Phase 3: Production Deployment
- Azure Application Insights integration
- Aspire deployment to Azure
- Advanced alerting and dashboards

## 🛠️ Troubleshooting

### Aspire Dashboard Not Loading
```bash
# Check container status
docker ps | grep aspire

# View logs
docker logs aspire-dashboard-dev
```

### No Telemetry Data
1. Verify OTLP endpoint configuration
2. Check application logs for OpenTelemetry initialization
3. Ensure Agent Framework is using telemetry-enabled chat client

### Performance Impact
- OpenTelemetry overhead: < 5% in typical scenarios
- Aspire Dashboard: Local development only
- Production: Use sampling for high-volume scenarios

---

**🎯 Success Metrics:**
- Real-time visibility into AI agent conversations
- Tool call execution tracing and debugging
- Security incident monitoring and alerting
- Performance optimization through detailed metrics