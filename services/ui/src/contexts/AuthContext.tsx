'use client';

import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { useIsAuthenticated, useMsal } from '@azure/msal-react';
import { loginRequest } from '@/lib/auth-config';
import { apiClient } from '@/lib/api-client';

interface AuthState {
  isAuthenticated: boolean;
  isAcquiringToken: boolean;
  accessToken: string | null;
  userInfo: { name?: string; email?: string } | null;
  login: () => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthState | null>(null);

interface AuthContextProviderProps {
  children: ReactNode;
}

export function AuthContextProvider({ children }: AuthContextProviderProps) {
  const { instance, accounts, inProgress } = useMsal();
  const isAuthenticated = useIsAuthenticated();
  const [accessToken, setAccessToken] = useState<string | null>(null);
  const [userInfo, setUserInfo] = useState<{ name?: string; email?: string } | null>(null);
  const [isAcquiringToken, setIsAcquiringToken] = useState(false);

  // Decode JWT token to get expiration time
  const getTokenExpiration = (token: string): number | null => {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.exp ? payload.exp * 1000 : null; // Convert to milliseconds
    } catch (error) {
      console.error('Failed to decode token:', error);
      return null;
    }
  };

  // Check if token needs refresh (expires in less than 5 minutes)
  const shouldRefreshToken = (token: string): boolean => {
    const expirationTime = getTokenExpiration(token);
    if (!expirationTime) return false;
    
    const now = Date.now();
    const timeUntilExpiry = expirationTime - now;
    const fiveMinutes = 5 * 60 * 1000; // 5 minutes in milliseconds
    
    return timeUntilExpiry <= fiveMinutes;
  };

  // Acquire token when authentication state changes
  useEffect(() => {
    if (isAuthenticated && !accessToken && !isAcquiringToken && inProgress === 'none') {
      acquireToken();
    } else if (!isAuthenticated && accessToken) {
      // User logged out - clear all state
      clearAuthState();
    }
  }, [isAuthenticated, accessToken, inProgress]);

  // Set up automatic token refresh
  useEffect(() => {
    if (!accessToken || !isAuthenticated) return;

    console.log('Setting up automatic token refresh...');
    
    // Check every 2 minutes if token needs refresh
    const refreshInterval = setInterval(async () => {
      if (accessToken && shouldRefreshToken(accessToken)) {
        const expirationTime = getTokenExpiration(accessToken);
        const timeUntilExpiry = expirationTime ? (expirationTime - Date.now()) / 1000 : 0;
        console.log(`Token expires in ${Math.round(timeUntilExpiry)} seconds. Refreshing...`);
        await acquireToken();
      }
    }, 2 * 60 * 1000); // Check every 2 minutes

    // Also do an immediate check
    if (shouldRefreshToken(accessToken)) {
      console.log('Token needs immediate refresh');
      acquireToken();
    }

    return () => {
      console.log('Clearing token refresh interval');
      clearInterval(refreshInterval);
    };
  }, [accessToken, isAuthenticated]);

  const clearAuthState = () => {
    console.log('Clearing authentication state');
    setAccessToken(null);
    setUserInfo(null);
    apiClient.setAccessToken(null);
  };

  const acquireToken = async () => {
    if (!isAuthenticated || isAcquiringToken) return;
    
    console.log('Starting token acquisition process...');
    setIsAcquiringToken(true);
    try {
      const tokenRequest = {
        ...loginRequest,
        account: accounts[0],
      };

      console.log('Attempting silent token acquisition for scopes:', tokenRequest.scopes);
      const response = await instance.acquireTokenSilent(tokenRequest);
      console.log('Token acquired successfully:', {
        hasAccessToken: !!response.accessToken,
        tokenLength: response.accessToken?.length,
        scopes: response.scopes,
        account: response.account?.username
      });
      
      setAccessToken(response.accessToken);
      apiClient.setAccessToken(response.accessToken);

      // Extract user info from token claims
      const claims = response.account?.idTokenClaims as any;
      if (claims) {
        setUserInfo({
          name: claims.name || claims.preferred_username,
          email: claims.email || claims.preferred_username,
        });
      }
      
      console.log('Access token set successfully in API client');
    } catch (error) {
      console.error('Failed to acquire token silently:', error);
      // Try interactive token acquisition
      try {
        console.log('Attempting interactive token acquisition...');
        const response = await instance.acquireTokenPopup(loginRequest);
        console.log('Interactive token acquired:', {
          hasAccessToken: !!response.accessToken,
          tokenLength: response.accessToken?.length,
          scopes: response.scopes
        });
        
        setAccessToken(response.accessToken);
        apiClient.setAccessToken(response.accessToken);
        
        const claims = response.account?.idTokenClaims as any;
        if (claims) {
          setUserInfo({
            name: claims.name || claims.preferred_username,
            email: claims.email || claims.preferred_username,
          });
        }
        
        console.log('Interactive access token set successfully in API client');
      } catch (popupError) {
        console.error('Failed to acquire token via popup:', popupError);
      }
    } finally {
      setIsAcquiringToken(false);
    }
  };

  const login = async () => {
    try {
      await instance.loginPopup(loginRequest);
    } catch (error) {
      console.error('Login failed:', error);
    }
  };

  const logout = () => {
    console.log('Starting logout process');
    clearAuthState();
    instance.logoutPopup({
      postLogoutRedirectUri: window.location.origin,
    });
  };

  const authState: AuthState = {
    isAuthenticated: isAuthenticated && !!accessToken,
    isAcquiringToken,
    accessToken,
    userInfo,
    login,
    logout,
  };

  return (
    <AuthContext.Provider value={authState}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthState {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthContextProvider');
  }
  return context;
}