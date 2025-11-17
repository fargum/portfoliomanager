# Test Smart Date Handling for Portfolio AI Tools
# This script tests the updated portfolio tools to ensure they handle "today" queries properly

$baseUrl = "http://localhost:5001"

Write-Host "Testing Portfolio Manager Smart Date Handling" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green

# Wait for API to be ready
Write-Host "Waiting for API to be ready..." -ForegroundColor Yellow
$ready = $false
$attempts = 0
$maxAttempts = 30

while (-not $ready -and $attempts -lt $maxAttempts) {
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/health" -Method Get -TimeoutSec 5
        if ($response.status -eq "Healthy") {
            $ready = $true
            Write-Host "API is ready!" -ForegroundColor Green
        }
    }
    catch {
        $attempts++
        Write-Host "Attempt $attempts/$maxAttempts - API not ready yet..." -ForegroundColor Yellow
        Start-Sleep -Seconds 2
    }
}

if (-not $ready) {
    Write-Host "API failed to start within timeout period" -ForegroundColor Red
    exit 1
}

# Test 1: Check available MCP tools
Write-Host "`nTest 1: Checking available MCP tools..." -ForegroundColor Cyan
try {
    $tools = Invoke-RestMethod -Uri "$baseUrl/api/ai/mcp/tools" -Method Get
    Write-Host "Available tools:" -ForegroundColor Green
    foreach ($tool in $tools) {
        Write-Host "  - $($tool.Name): $($tool.Description)" -ForegroundColor White
    }
    
    # Check if our updated descriptions are there
    $holdingsTool = $tools | Where-Object { $_.Name -eq "GetPortfolioHoldings" }
    $analysisTool = $tools | Where-Object { $_.Name -eq "AnalyzePortfolioPerformance" }
    
    if ($holdingsTool.Description -like "*today*") {
        Write-Host "✓ GetPortfolioHoldings tool has updated description with 'today' support" -ForegroundColor Green
    } else {
        Write-Host "✗ GetPortfolioHoldings tool description not updated" -ForegroundColor Red
    }
    
    if ($analysisTool.Description -like "*today*") {
        Write-Host "✓ AnalyzePortfolioPerformance tool has updated description with 'today' support" -ForegroundColor Green
    } else {
        Write-Host "✗ AnalyzePortfolioPerformance tool description not updated" -ForegroundColor Red
    }
}
catch {
    Write-Host "✗ Failed to get MCP tools: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: Test GetPortfolioHoldings with "today"
Write-Host "`nTest 2: Testing GetPortfolioHoldings with 'today'..." -ForegroundColor Cyan
try {
    $todayRequest = @{
        toolName = "GetPortfolioHoldings"
        parameters = @{
            accountId = 1
            date = "today"
        }
    } | ConvertTo-Json -Depth 3

    $todayResponse = Invoke-RestMethod -Uri "$baseUrl/api/ai/mcp/execute" -Method Post -Body $todayRequest -ContentType "application/json"
    
    if ($todayResponse.IsRealTimeData -eq $true) {
        Write-Host "✓ 'today' request successfully returned real-time data" -ForegroundColor Green
        Write-Host "  Requested Date: $($todayResponse.RequestedDate)" -ForegroundColor White
        Write-Host "  Effective Date: $($todayResponse.EffectiveDate)" -ForegroundColor White
        Write-Host "  Holdings Count: $($todayResponse.HoldingsCount)" -ForegroundColor White
    } else {
        Write-Host "✗ 'today' request did not return real-time data flag" -ForegroundColor Yellow
        Write-Host "Response: $($todayResponse | ConvertTo-Json -Depth 2)" -ForegroundColor Gray
    }
}
catch {
    Write-Host "✗ Failed to test GetPortfolioHoldings with 'today': $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Test AnalyzePortfolioPerformance with "today"
Write-Host "`nTest 3: Testing AnalyzePortfolioPerformance with 'today'..." -ForegroundColor Cyan
try {
    $analysisRequest = @{
        toolName = "AnalyzePortfolioPerformance"
        parameters = @{
            accountId = 1
            analysisDate = "today"
        }
    } | ConvertTo-Json -Depth 3

    $analysisResponse = Invoke-RestMethod -Uri "$baseUrl/api/ai/mcp/execute" -Method Post -Body $analysisRequest -ContentType "application/json"
    
    if ($analysisResponse.IsRealTimeAnalysis -eq $true) {
        Write-Host "✓ 'today' analysis request successfully returned real-time analysis" -ForegroundColor Green
        Write-Host "  Requested Date: $($analysisResponse.RequestedDate)" -ForegroundColor White
        Write-Host "  Effective Date: $($analysisResponse.EffectiveDate)" -ForegroundColor White
    } else {
        Write-Host "✗ 'today' analysis request did not return real-time analysis flag" -ForegroundColor Yellow
        Write-Host "Response: $($analysisResponse | ConvertTo-Json -Depth 2)" -ForegroundColor Gray
    }
}
catch {
    Write-Host "✗ Failed to test AnalyzePortfolioPerformance with 'today': $($_.Exception.Message)" -ForegroundColor Red
}

# Test 4: Test with historical date to ensure backward compatibility
Write-Host "`nTest 4: Testing with historical date for backward compatibility..." -ForegroundColor Cyan
try {
    $historicalRequest = @{
        toolName = "GetPortfolioHoldings"
        parameters = @{
            accountId = 1
            date = "2024-01-15"
        }
    } | ConvertTo-Json -Depth 3

    $historicalResponse = Invoke-RestMethod -Uri "$baseUrl/api/ai/mcp/execute" -Method Post -Body $historicalRequest -ContentType "application/json"
    
    if ($historicalResponse.IsRealTimeData -eq $false) {
        Write-Host "✓ Historical date request correctly identified as non-real-time" -ForegroundColor Green
        Write-Host "  Requested Date: $($historicalResponse.RequestedDate)" -ForegroundColor White
        Write-Host "  Effective Date: $($historicalResponse.EffectiveDate)" -ForegroundColor White
    } else {
        Write-Host "✗ Historical date request incorrectly flagged as real-time" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "✗ Failed to test historical date compatibility: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nSmart Date Handling Tests Complete!" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green