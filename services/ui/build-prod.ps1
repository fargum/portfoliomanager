# Build production Docker image for Azure Container Registry
# This script reads from .env.production and passes values as build args

# Load environment variables from .env.production
Get-Content .env.production | ForEach-Object {
    if ($_ -match '^([^=]+)=(.*)$') {
        $name = $matches[1].Trim()
        $value = $matches[2].Trim()
        Set-Variable -Name $name -Value $value
    }
}

# Get ACR server from environment or use default
if (-not $ACR_SERVER) {
    $ACR_SERVER = "portfoliomanageracr.azurecr.io"
}

Write-Host "Building UI image for production..." -ForegroundColor Green
Write-Host "API URL: $NEXT_PUBLIC_API_BASE_URL" -ForegroundColor Cyan
Write-Host "ACR Server: $ACR_SERVER" -ForegroundColor Cyan

# Build the Docker image with production settings
docker build `
  --build-arg NEXT_PUBLIC_API_BASE_URL="$NEXT_PUBLIC_API_BASE_URL" `
  --build-arg NEXT_PUBLIC_AZURE_CLIENT_ID="$NEXT_PUBLIC_AZURE_CLIENT_ID" `
  --build-arg NEXT_PUBLIC_AZURE_TENANT_ID="$NEXT_PUBLIC_AZURE_TENANT_ID" `
  --build-arg NEXT_PUBLIC_AZURE_API_CLIENT_ID="$NEXT_PUBLIC_AZURE_API_CLIENT_ID" `
  -t "$ACR_SERVER/pm-ui:prod" `
  .

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBuild successful!" -ForegroundColor Green
    Write-Host "Image tagged as: $ACR_SERVER/pm-ui:prod" -ForegroundColor Green
    Write-Host "`nTo push to ACR, run:" -ForegroundColor Yellow
    Write-Host "  docker push $ACR_SERVER/pm-ui:prod" -ForegroundColor White
} else {
    Write-Host "`nBuild failed!" -ForegroundColor Red
    exit 1
}
