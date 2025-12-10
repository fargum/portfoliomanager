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

  // Acquire token when authentication state changes
  useEffect(() => {
    if (isAuthenticated && !accessToken && !isAcquiringToken && inProgress === 'none') {
      acquireToken();
    } else if (!isAuthenticated && accessToken) {
      // User logged out - clear all state
      clearAuthState();
    }
  }, [isAuthenticated, accessToken, inProgress]);

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