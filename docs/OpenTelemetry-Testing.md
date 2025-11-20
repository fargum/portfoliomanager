# OpenTelemetry Integration Test

## Test 1: Basic Telemetry Functionality

1. **Start Development Environment:**
   ```bash
   docker-compose -f docker-compose.dev.yml up -d
   ```

2. **Access Aspire Dashboard:**
   - Open: http://localhost:18888
   - Verify the dashboard loads with no traces initially

3. **Test API Endpoint:**
   ```bash
   # Test health endpoint (should generate HTTP telemetry)
   curl http://localhost:8080/health
   
   # Test AI chat endpoint (should generate full Agent Framework telemetry)
   curl -X POST http://localhost:8080/api/ai/chat/query \
     -H "Content-Type: application/json" \
     -d '{
       "query": "What is the current value of my portfolio?",
       "accountId": 1
     }'
   ```

4. **Verify Telemetry in Aspire Dashboard:**
   - Navigate to **Traces** section
   - Look for traces with these operations:
     - `ProcessPortfolioQueryWithMemory` (custom span)
     - `agent.run` (Agent Framework span)  
     - `chat.completion` (Azure OpenAI span)
     - HTTP request spans for `/api/ai/chat/query`

## Expected Telemetry Tags

When viewing traces, you should see these tags:
- `account.id: "1"`
- `query.type: "Performance"` (or similar)
- `query.length: "42"` (length of query)
- `validation.result: "passed"`
- `service.name: "PortfolioManager.AI"`

## Success Criteria

✅ **Phase 1 Complete** when you can see:
1. Aspire Dashboard loads successfully
2. HTTP requests generate telemetry spans  
3. Custom business logic spans appear with proper tags
4. Agent Framework spans show chat completions and tool calls
5. Full request trace from API to Azure OpenAI and back

---

**Next:** Phase 2 will add custom metrics and enhanced security incident telemetry.