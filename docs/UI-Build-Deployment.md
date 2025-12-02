# UI Build and Deployment Guide

## Critical: Understanding Next.js Build-Time Variables

The UI uses **Next.js with `NEXT_PUBLIC_` environment variables**. These are **baked into the JavaScript bundle at build time** and **cannot be changed at runtime**.

### Why This Matters

1. **Build-time binding**: When you run `npm run build`, Next.js reads `NEXT_PUBLIC_*` variables and replaces them directly in the code
2. **Permanent values**: Once built, these values are in the JavaScript files and can't be changed without rebuilding
3. **Container image limitation**: Even if you set environment variables on the Container App, Next.js won't see them because they're already compiled into the JS bundle

### Critical Variables

These **must** be correct during build:

```bash
NEXT_PUBLIC_AZURE_CLIENT_ID=4884728d-b70f-4695-a359-a291ddfa3574  # UI App Registration
NEXT_PUBLIC_AZURE_TENANT_ID=1f6e5d9e-5f2d-44ae-a729-05e20ecd4f75   # Tenant ID
NEXT_PUBLIC_AZURE_API_CLIENT_ID=78661cad-e894-4462-9d21-5c7d10c56a3e  # API App Registration
NEXT_PUBLIC_API_BASE_URL=https://pm-api.thankfulbay-b96a666e.uksouth.azurecontainerapps.io
```

## Building the UI

### Option 1: Use the Build Script (Recommended)

```powershell
.\build-ui.ps1
```

This script:
- Has the correct client IDs hardcoded
- Builds with proper `--build-arg` parameters
- Deploys to Azure Container Apps
- Provides clear feedback

### Option 2: Manual Build

```powershell
az acr build `
    --registry portfoliomanageracr `
    --image portfoliomanager-ui:latest `
    --file services/ui/Dockerfile `
    --build-arg NEXT_PUBLIC_AZURE_CLIENT_ID=4884728d-b70f-4695-a359-a291ddfa3574 `
    --build-arg NEXT_PUBLIC_AZURE_TENANT_ID=1f6e5d9e-5f2d-44ae-a729-05e20ecd4f75 `
    --build-arg NEXT_PUBLIC_AZURE_API_CLIENT_ID=78661cad-e894-4462-9d21-5c7d10c56a3e `
    --build-arg NEXT_PUBLIC_API_BASE_URL=https://pm-api.thankfulbay-b96a666e.uksouth.azurecontainerapps.io `
    --platform linux `
    services/ui
```

### Option 3: Local Development

For local development, create `services/ui/.env.local`:

```bash
NEXT_PUBLIC_AZURE_CLIENT_ID=4884728d-b70f-4695-a359-a291ddfa3574
NEXT_PUBLIC_AZURE_TENANT_ID=1f6e5d9e-5f2d-44ae-a729-05e20ecd4f75
NEXT_PUBLIC_AZURE_API_CLIENT_ID=78661cad-e894-4462-9d21-5c7d10c56a3e
NEXT_PUBLIC_API_BASE_URL=http://localhost:8080
```

## Azure AD App Registrations

We have **TWO separate app registrations**:

### 1. PortfolioManager.Ui
- **Client ID**: `4884728d-b70f-4695-a359-a291ddfa3574`
- **Purpose**: User authentication in the browser
- **Redirect URI**: `https://pm-ui.thankfulbay-b96a666e.uksouth.azurecontainerapps.io`
- **Used by**: MSAL.js in the browser

### 2. PortfolioManager.Api
- **Client ID**: `78661cad-e894-4462-9d21-5c7d10c56a3e`
- **Purpose**: API scope/audience validation
- **Exposed API**: `api://78661cad-e894-4462-9d21-5c7d10c56a3e/Portfolio.ReadWrite`
- **Used by**: Backend JWT validation

## Common Mistakes to Avoid

### ❌ Building without --build-arg
```powershell
# This will FAIL or use wrong values:
az acr build --registry portfoliomanageracr --image portfoliomanager-ui:latest --file services/ui/Dockerfile services/ui
```

### ❌ Using placeholder values
```powershell
# This will BUILD but authentication will be BROKEN:
--build-arg NEXT_PUBLIC_AZURE_CLIENT_ID=placeholder
```

### ❌ Trying to set env vars at runtime
```powershell
# This WON'T WORK - values are already baked into the JS:
az containerapp update --name pm-ui --set-env-vars NEXT_PUBLIC_AZURE_CLIENT_ID=xyz
```

### ✅ Correct approach
```powershell
# Always rebuild with correct --build-arg values:
.\build-ui.ps1
```

## Troubleshooting

### Authentication not working after deployment

1. **Check if wrong values were baked in**:
   - Open browser dev tools → Sources tab
   - Find `/_next/static/chunks/...js` files
   - Search for the client ID - if it's wrong, you need to rebuild

2. **Rebuild with correct values**:
   ```powershell
   .\build-ui.ps1
   ```

3. **Clear browser cache**:
   - MSAL caches tokens in localStorage
   - Open dev tools → Application → Local Storage → Clear all
   - Refresh the page

### How to verify correct values were baked in

After deployment, check the built JavaScript:

```powershell
# Get the latest image digest
az acr repository show --name portfoliomanageracr --image portfoliomanager-ui:latest --query "digest"

# Or check in browser:
# View page source → Look for chunks/362-*.js → Search for clientId
```

## Deployment Checklist

- [ ] Verify client IDs are correct in build script
- [ ] Run `.\build-ui.ps1`
- [ ] Wait for build to complete (2-3 minutes)
- [ ] Wait for deployment to complete
- [ ] Test authentication in incognito window
- [ ] Verify redirect URI matches Azure AD configuration
- [ ] Test with multiple accounts to confirm isolation

## Related Documentation

- Azure AD Configuration: `docs/Security-Configuration.md`
- API Deployment: `docs/Docker-Deployment-Guide.md`
- Security Audit: `docs/SECURITY-AUDIT.md`
