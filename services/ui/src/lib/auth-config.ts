import { Configuration, LogLevel } from '@azure/msal-browser';

// Get configuration from environment variables with fallbacks for development
const getClientId = () => {
  const clientId = process.env.NEXT_PUBLIC_AZURE_CLIENT_ID;
  if (!clientId) {
    throw new Error('NEXT_PUBLIC_AZURE_CLIENT_ID environment variable is required. Please set it in your .env.local file.');
  }
  return clientId;
};

const getTenantId = () => {
  const tenantId = process.env.NEXT_PUBLIC_AZURE_TENANT_ID;
  if (!tenantId) {
    throw new Error('NEXT_PUBLIC_AZURE_TENANT_ID environment variable is required. Please set it in your .env.local file.');
  }
  return tenantId;
};

const getApiClientId = () => {
  const apiClientId = process.env.NEXT_PUBLIC_AZURE_API_CLIENT_ID;
  if (!apiClientId) {
    throw new Error('NEXT_PUBLIC_AZURE_API_CLIENT_ID environment variable is required. Please set it in your .env.local file.');
  }
  return apiClientId;
};

// MSAL configuration
export const msalConfig: Configuration = {
  auth: {
    clientId: getClientId(),
    authority: `https://login.microsoftonline.com/${getTenantId()}`,
    redirectUri: typeof window !== 'undefined' ? window.location.origin : 'http://localhost:3000',
    postLogoutRedirectUri: typeof window !== 'undefined' ? window.location.origin : 'http://localhost:3000',
    navigateToLoginRequestUrl: false, // Improves token caching
  },
  cache: {
    cacheLocation: 'localStorage', // Use localStorage for longer persistence
    storeAuthStateInCookie: false, // Set to true if you need IE11 support
    secureCookies: false, // Set to true in production with HTTPS
  },
  system: {
    loggerOptions: {
      loggerCallback: (level: LogLevel, message: string, containsPii: boolean) => {
        if (containsPii) {
          return;
        }
        switch (level) {
          case LogLevel.Error:
            console.error(message);
            return;
          case LogLevel.Info:
            console.info(message);
            return;
          case LogLevel.Verbose:
            console.debug(message);
            return;
          case LogLevel.Warning:
            console.warn(message);
            return;
        }
      },
    },
  },
};

// Add scopes here for ID token to be used at Microsoft identity platform endpoints.
export const loginRequest = {
  scopes: [`api://${getApiClientId()}/Portfolio.ReadWrite`],
  prompt: 'select_account' as const,
  forceRefresh: false, // Use cached tokens when available to improve performance
};

// Add the endpoints here for Microsoft Graph API services you'd like to use.
export const graphConfig = {
  graphMeEndpoint: 'https://graph.microsoft.com/v1.0/me',
};