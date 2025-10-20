# AI Chat Endpoint Test

## Test the basic chat functionality

### 1. Start the API
```bash
dotnet run --project services/api/src/FtoConsulting.PortfolioManager.Api
```

### 2. Test Chat Query
```bash
curl -X POST "http://localhost:5001/api/ai/chat/query" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "How is my portfolio performing today?",
    "accountId": 1,
    "contextDate": "2025-10-17T00:00:00Z"
  }'
```

### 3. Test Available Tools
```bash
curl -X GET "http://localhost:5001/api/ai/chat/tools" \
  -H "Accept: application/json"
```

### 4. Test MCP Server Health
```bash
curl -X GET "http://localhost:5001/api/ai/mcp/health" \
  -H "Accept: application/json"
```

### 5. Test MCP Tools
```bash
curl -X GET "http://localhost:5001/api/ai/mcp/tools" \
  -H "Accept: application/json"
```

### 6. Execute MCP Tool
```bash
curl -X POST "http://localhost:5001/api/ai/mcp/execute" \
  -H "Content-Type: application/json" \
  -d '{
    "toolName": "get_portfolio_holdings",
    "parameters": {
      "accountId": 1,
      "date": "2025-10-17"
    }
  }'
```

## Expected Responses

### Chat Query Response
```json
{
  "response": "Your portfolio is currently valued at $42,500.00. Today's change: $425.00 (1.01%). You have 3 holdings in your portfolio.",
  "queryType": "Performance",
  "portfolioSummary": {
    "accountId": 1,
    "date": "2024-01-15T00:00:00Z",
    "totalValue": 42500.00,
    "dayChange": 425.00,
    "dayChangePercentage": 0.0101,
    "holdingsCount": 3,
    "topHoldings": ["AAPL", "MSFT", "GOOGL"]
  },
  "insights": [
    {
      "type": "Performance",
      "title": "Strong Daily Performance",
      "description": "Your portfolio gained 1.01% today, outperforming typical market movements.",
      "severity": "Positive"
    }
  ],
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### MCP Tools Response
```json
[
  {
    "name": "get_portfolio_holdings",
    "description": "Retrieve portfolio holdings for a specific account and date",
    "schema": {
      "type": "object",
      "properties": {
        "accountId": {"type": "integer", "description": "Account ID"},
        "date": {"type": "string", "description": "Date in YYYY-MM-DD format"}
      },
      "required": ["accountId", "date"]
    }
  }
]
```

## Notes
- The AI responses are currently using mock data and basic logic
- Microsoft Agent Framework integration will be added in the next phase
- Market intelligence features use mock data until real APIs are integrated
- All endpoints support CORS for UI integration