# Test AI Chat with October 2025 Data

# Test 1: Basic portfolio performance query
curl -X POST "http://localhost:5125/api/ai/chat/query" `
  -H "Content-Type: application/json" `
  -d '{
    "query": "How is my portfolio performing today?",
    "accountId": 1
  }'

Write-Host "`n=== Test 1 Complete ===`n"

# Test 2: Specific date query (October 17, 2025)
curl -X POST "http://localhost:5125/api/ai/chat/query" `
  -H "Content-Type: application/json" `
  -d '{
    "query": "Show me my portfolio performance for October 17th, 2025",
    "accountId": 1
  }'

Write-Host "`n=== Test 2 Complete ===`n"

# Test 3: Holdings inquiry
curl -X POST "http://localhost:5125/api/ai/chat/query" `
  -H "Content-Type: application/json" `
  -d '{
    "query": "What holdings do I have in my portfolio?",
    "accountId": 1
  }'

Write-Host "`n=== Test 3 Complete ===`n"

# Test 4: Risk analysis
curl -X POST "http://localhost:5125/api/ai/chat/query" `
  -H "Content-Type: application/json" `
  -d '{
    "query": "What is my portfolio risk profile?",
    "accountId": 1
  }'

Write-Host "`n=== Test 4 Complete ===`n"

# Test 5: Market context
curl -X POST "http://localhost:5125/api/ai/chat/query" `
  -H "Content-Type: application/json" `
  -d '{
    "query": "What is the market sentiment for my holdings?",
    "accountId": 1
  }'

Write-Host "`n=== Test 5 Complete ===`n"

# Test 6: MCP Tools
curl -X GET "http://localhost:5125/api/ai/mcp/tools" `
  -H "Accept: application/json"

Write-Host "`n=== Test 6 Complete ===`n"

# Test 7: MCP Tool Execution
curl -X POST "http://localhost:5125/api/ai/mcp/execute" `
  -H "Content-Type: application/json" `
  -d '{
    "toolName": "get_portfolio_holdings",
    "parameters": {
      "accountId": 1,
      "date": "2025-10-17"
    }
  }'

Write-Host "`n=== All Tests Complete ===`n"