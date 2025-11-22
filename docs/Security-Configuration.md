# Security Configuration Guide

## Overview
This document outlines the security configuration for the Portfolio Manager application, including environment variable management and Azure AD authentication setup.

## Environment Variables

### API Service
All sensitive configuration is stored in `.env` file (git-ignored):

**Required Variables:**
- `DB_USERNAME` / `DB_PASSWORD` - Database credentials
- `EOD_API_TOKEN` - EOD Historical Data API token
- `AZURE_FOUNDRY_API_KEY` - Azure Foundry API key
- `AZURE_AD_TENANT_ID` - Azure AD tenant ID
- `AZURE_AD_CLIENT_ID` - Azure AD API application client ID
- `AZURE_AD_UI_CLIENT_ID` - Azure AD UI application client ID
- `AZURE_AD_AUDIENCE` - Azure AD API audience

**Setup:**
1. Copy `.env.template` to `.env`
2. Fill in actual values
3. Never commit `.env` to version control

### UI Service
All sensitive configuration is stored in `.env.local` file (git-ignored):

**Required Variables:**
- `NEXT_PUBLIC_AZURE_CLIENT_ID` - Azure AD UI application client ID
- `NEXT_PUBLIC_AZURE_TENANT_ID` - Azure AD tenant ID
- `NEXT_PUBLIC_AZURE_API_CLIENT_ID` - Azure AD API application client ID (for scopes)

**Setup:**
1. Copy `services/ui/.env.local.template` to `services/ui/.env.local`
2. Fill in actual values
3. Never commit `.env.local` to version control

## Azure AD Configuration

### API Application
- **Purpose**: Validates JWT tokens from UI
- **Client ID**: Stored in `AZURE_AD_CLIENT_ID`
- **Audience**: `api://{AZURE_AD_CLIENT_ID}`
- **Scopes**: `Portfolio.ReadWrite`

### UI Application
- **Purpose**: Acquires tokens to call API
- **Client ID**: Stored in `AZURE_AD_UI_CLIENT_ID`
- **Authentication**: Implicit flow enabled
- **Redirect URIs**: Must include deployment URLs (e.g., `http://localhost:3000`)

## Security Best Practices

### ✅ What's Secure:
- Environment variables in `.env` and `.env.local` files (git-ignored)
- No hard-coded secrets in source code
- Environment variable validation with `!` operator
- Separate client IDs for API and UI applications

### ❌ Never Commit:
- `.env` files with actual values
- `.env.local` files with actual values
- Hard-coded Azure AD client IDs, tenant IDs, or API keys
- Database passwords or connection strings

### 🔧 Configuration Flow:
1. **Development**: Use `.env.local` files for local development
2. **Docker**: Environment variables passed via docker-compose from `.env`
3. **Production**: Environment variables managed by hosting platform

## Deployment Security

### Docker Compose
- Build arguments pass environment variables at build time
- Runtime environment variables override defaults
- No secrets stored in Dockerfile or docker-compose.yml

### Production Considerations
- Use Azure Key Vault or similar secret management
- Rotate API keys and client secrets regularly
- Monitor access logs and failed authentication attempts
- Enable Azure AD conditional access policies

## Troubleshooting

### Common Issues:
1. **401 Unauthorized**: Check if `AZURE_AD_CLIENT_ID` matches token audience
2. **Missing Environment Variables**: Ensure all required variables are set
3. **CORS Errors**: Verify redirect URIs in Azure AD configuration
4. **Token Validation**: Check tenant ID and authority URL

### Debug Steps:
1. Verify environment variables are loaded: Check build logs
2. Check Azure AD configuration: Ensure redirect URIs match deployment URLs
3. Validate token claims: Use JWT decoder tools
4. Review API logs: Check authentication middleware logs