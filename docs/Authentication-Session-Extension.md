# Authentication Session Extension Guide

## Overview
This document explains the improvements made to extend authentication session duration in the Portfolio Manager application.

## Problem
The UI was losing authentication sessions quickly (typically after 1 hour) due to:
- Default Azure AD access token lifetime (1 hour)
- No automatic token refresh mechanism
- Token expiration without proactive renewal

## Solution Implemented

### 1. Frontend Automatic Token Refresh (UI)

#### Changes in `AuthContext.tsx`
Added automatic token refresh mechanism that:
- **Decodes JWT tokens** to extract expiration time
- **Proactively refreshes tokens** 5 minutes before expiration
- **Periodic checks** every 2 minutes to ensure token validity
- **Silent token acquisition** using MSAL's cached refresh tokens

**Key Features:**
```typescript
// Token expiration detection
const getTokenExpiration = (token: string): number | null => {
  const payload = JSON.parse(atob(token.split('.')[1]));
  return payload.exp ? payload.exp * 1000 : null;
};

// Automatic refresh check (every 2 minutes)
setInterval(() => {
  if (shouldRefreshToken(accessToken)) {
    acquireToken(); // Silently refresh the token
  }
}, 2 * 60 * 1000);
```

**Benefits:**
- Seamless user experience - no interruption
- Tokens refresh automatically before expiration
- Uses MSAL's silent token acquisition (no user interaction needed)

#### Changes in `auth-config.ts`
Enhanced MSAL configuration:
- Added `navigateToLoginRequestUrl: false` for better token caching
- Added comments for production security settings
- Set `forceRefresh: false` to use cached tokens efficiently

### 2. Backend Token Validation (API)

#### Changes in `Program.cs`
Enhanced token validation with:
- **Extended clock skew tolerance**: 5 minutes (default is 5 seconds)
- **Explicit lifetime validation**: Ensures tokens are properly validated
- **Better handling of time synchronization issues**

```csharp
options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(5);
options.TokenValidationParameters.ValidateLifetime = true;
```

**Benefits:**
- Prevents premature token rejection due to clock differences
- More forgiving of slight time synchronization issues between client/server
- Reduces 401 Unauthorized errors from timing edge cases

## How It Works

### Token Lifecycle
1. **Initial Login**: User authenticates, receives access token (1 hour lifetime)
2. **Background Monitoring**: Every 2 minutes, check if token expires within 5 minutes
3. **Proactive Refresh**: At 55 minutes, silently refresh the token using MSAL
4. **New Token Acquired**: Fresh token acquired without user intervention
5. **Continuous Loop**: Process repeats indefinitely while user is active

### MSAL Silent Token Acquisition
MSAL handles the refresh automatically by:
- Using cached refresh tokens (stored in localStorage)
- Communicating with Azure AD token endpoint
- Acquiring new access tokens without user interaction
- Only prompting user if refresh token expires (typically 90 days)

## Session Duration

### Current Configuration
- **Access Token Lifetime**: 1 hour (Azure AD default)
- **Refresh Token Lifetime**: 90 days (Azure AD default)
- **Effective Session Duration**: Up to 90 days with automatic refresh

### User Experience
- **Active Sessions**: Automatically refreshed every ~55 minutes
- **Inactive Sessions**: Token refresh still occurs while browser tab is open
- **Browser Closed**: Session persists in localStorage, resumes on next visit
- **Maximum Idle Time**: 90 days (refresh token expiration)

## Further Extensions (Optional)

### Extending Token Lifetimes in Azure AD

If you need even longer token lifetimes, you can configure Azure AD policies:

#### 1. Access Token Lifetime Policy
Create a token lifetime policy in Azure AD:
```powershell
# Connect to Azure AD
Connect-AzureAD

# Create policy for longer access token lifetime
New-AzureADPolicy -Definition @('{
  "TokenLifetimePolicy":{
    "Version":1,
    "AccessTokenLifetime":"02:00:00"
  }
}') -DisplayName "ExtendedAccessTokenPolicy" -Type "TokenLifetimePolicy"

# Apply to your application
$policy = Get-AzureADPolicy -Id <PolicyId>
$sp = Get-AzureADServicePrincipal -Filter "AppId eq '<YourAppId>'"
Add-AzureADServicePrincipalPolicy -Id $sp.ObjectId -RefId $policy.Id
```

#### 2. Refresh Token Lifetime Policy
Extend refresh token lifetime:
```powershell
New-AzureADPolicy -Definition @('{
  "TokenLifetimePolicy":{
    "Version":1,
    "MaxInactiveTime":"90.00:00:00",
    "MaxAgeMultiFactor":"until-revoked"
  }
}') -DisplayName "ExtendedRefreshTokenPolicy" -Type "TokenLifetimePolicy"
```

**Note**: Microsoft recommends using default token lifetimes for security. Only extend if you have specific business requirements.

### Browser Storage Considerations

#### localStorage (Current)
- ✅ Persists across browser sessions
- ✅ Survives page refreshes
- ✅ Available across tabs
- ⚠️ Vulnerable to XSS attacks (mitigated by Azure AD security)

#### sessionStorage (Alternative)
Change in `auth-config.ts`:
```typescript
cache: {
  cacheLocation: 'sessionStorage', // Clears when browser closes
  storeAuthStateInCookie: false,
}
```

## Security Considerations

### Best Practices Implemented
1. **Token Storage**: localStorage with MSAL encryption
2. **Automatic Cleanup**: Tokens cleared on logout
3. **Silent Refresh**: No credentials stored, uses secure refresh tokens
4. **Clock Skew Tolerance**: Prevents timing-based vulnerabilities
5. **Scope Validation**: Backend validates Portfolio.ReadWrite scope

### Additional Security Recommendations
1. **HTTPS Only**: Ensure all production traffic uses HTTPS
2. **Secure Cookies**: Set `secureCookies: true` in production
3. **Content Security Policy**: Add CSP headers to prevent XSS
4. **Regular Audits**: Monitor Azure AD sign-in logs for anomalies

## Monitoring

### Frontend Logs
Monitor browser console for:
```
Setting up automatic token refresh...
Token expires in 295 seconds. Refreshing...
Token acquired successfully
```

### Backend Logs
API logs will show:
```
Successfully validated token for user: [email]
Token expiration: [timestamp]
```

### Troubleshooting

#### Session Still Expires Too Quickly
1. Check browser console for token refresh errors
2. Verify Azure AD refresh token hasn't expired
3. Check network connectivity during refresh attempts
4. Review Azure AD sign-in logs for refresh token failures

#### Token Refresh Fails
1. Clear browser cache and localStorage
2. Sign out and sign back in
3. Check Azure AD app registration permissions
4. Verify API scopes match between frontend and backend

## Testing

### Verify Automatic Refresh
1. Sign in to the application
2. Open browser developer console
3. Wait ~55 minutes (or manually trigger by decoding token and checking expiration)
4. Should see logs: "Token expires in X seconds. Refreshing..."
5. New token acquired without user interaction

### Test Session Persistence
1. Sign in to the application
2. Close browser (don't sign out)
3. Reopen browser and navigate to app
4. Should still be authenticated (if within 90 days)

## Summary

The implemented changes provide:
- ✅ **Automatic token refresh** - No user interruption
- ✅ **Extended session duration** - Up to 90 days
- ✅ **Seamless experience** - Background token renewal
- ✅ **Improved reliability** - Clock skew tolerance
- ✅ **Security maintained** - Azure AD best practices

Your authentication sessions will now persist much longer with automatic renewal happening transparently in the background!
