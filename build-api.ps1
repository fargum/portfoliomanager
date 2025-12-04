# Build and Deploy API Script
# This script builds the Portfolio Manager API and deploys to Azure Container Apps

param(
    [string]$Registry = "portfoliomanageracr",
    [string]$ResourceGroup = "neiltest",
    [string]$AppName = "pm-api"
)

Write-Host "Building Portfolio Manager API..." -ForegroundColor Cyan

# Build the image using Azure Container Registry build
Write-Host "`nBuilding Docker image in Azure Container Registry..." -ForegroundColor Cyan
az acr build `
    --registry $Registry `
    --image portfoliomanager-api:latest `
    --file services/api/Dockerfile `
    --platform linux/amd64 `
    services/api

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nBuild failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`nBuild successful! Deploying to Container App..." -ForegroundColor Green

# Deploy the image
az containerapp update `
    --name $AppName `
    --resource-group $ResourceGroup `
    --image "$Registry.azurecr.io/portfoliomanager-api:latest"

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nDeployment failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`nDeployment successful!" -ForegroundColor Green
Write-Host "`nAPI is now available at: https://pm-api.thankfulbay-b96a666e.uksouth.azurecontainerapps.io" -ForegroundColor Cyan
Write-Host "Swagger UI: https://pm-api.thankfulbay-b96a666e.uksouth.azurecontainerapps.io/swagger" -ForegroundColor Cyan
