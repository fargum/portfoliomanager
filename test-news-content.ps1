# Test script to check what news content is being returned for GEN.LSE

$apiUrl = "http://localhost:8080/api/MarketIntelligence/market-context"
$body = @{
    tickers = @("GEN.LSE")
    date = "17/11/2025"
} | ConvertTo-Json

Write-Host "Testing news content for GEN.LSE..." -ForegroundColor Green

try {
    $response = Invoke-RestMethod -Uri $apiUrl -Method POST -Body $body -ContentType "application/json"
    
    Write-Host "`nMarket Context Response:" -ForegroundColor Yellow
    Write-Host ($response | ConvertTo-Json -Depth 10)
    
    if ($response.Context -and $response.Context.RelevantNews) {
        Write-Host "`nNews Items Found:" -ForegroundColor Cyan
        foreach ($newsItem in $response.Context.RelevantNews) {
            Write-Host "Title: $($newsItem.Title)" -ForegroundColor White
            Write-Host "Summary: $($newsItem.Summary)" -ForegroundColor Gray
            Write-Host "Sentiment: $($newsItem.SentimentScore)" -ForegroundColor Magenta
            Write-Host "Source: $($newsItem.Source)" -ForegroundColor DarkGray
            Write-Host "---"
        }
    } else {
        Write-Host "No news items found in response" -ForegroundColor Red
    }
}
catch {
    Write-Host "Error testing API: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
}