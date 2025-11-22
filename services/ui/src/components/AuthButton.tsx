'use client';

import { useMsal } from '@azure/msal-react';
import { LogIn, LogOut, User, Shield, AlertCircle } from 'lucide-react';
import { useAuth } from '@/contexts/AuthContext';

export function AuthButton() {
  const { inProgress } = useMsal();
  const { isAuthenticated, isAcquiringToken, accessToken, userInfo, login, logout } = useAuth();

  if (inProgress === 'login') {
    return (
      <div className="flex items-center space-x-2 text-blue-600">
        <div className="animate-spin h-4 w-4 border-2 border-blue-600 border-t-transparent rounded-full"></div>
        <span className="text-sm">Signing in...</span>
      </div>
    );
  }

  if (isAcquiringToken) {
    return (
      <div className="flex items-center space-x-2 text-blue-600">
        <div className="animate-spin h-4 w-4 border-2 border-blue-600 border-t-transparent rounded-full"></div>
        <span className="text-sm">Getting access token...</span>
      </div>
    );
  }

  if (!isAuthenticated) {
    return (
      <button
        onClick={login}
        className="flex items-center space-x-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
      >
        <LogIn className="h-4 w-4" />
        <span className="text-sm font-medium">Sign In</span>
      </button>
    );
  }

  return (
    <div className="flex items-center space-x-3">
      {/* Authentication Status */}
      <div className="flex items-center space-x-2">
        {accessToken ? (
          <div className="flex items-center space-x-2 text-green-600">
            <Shield className="h-4 w-4" />
            <span className="text-sm font-medium">Authenticated</span>
          </div>
        ) : (
          <div className="flex items-center space-x-2 text-yellow-600">
            <AlertCircle className="h-4 w-4" />
            <span className="text-sm font-medium">Getting token...</span>
          </div>
        )}
      </div>

      {/* User Info */}
      {userInfo && (
        <div className="flex items-center space-x-2 text-gray-700">
          <User className="h-4 w-4" />
          <span className="text-sm">{userInfo.name || userInfo.email}</span>
        </div>
      )}

      {/* Logout Button */}
      <button
        onClick={logout}
        className="flex items-center space-x-2 px-3 py-2 text-gray-600 hover:text-gray-900 hover:bg-gray-100 rounded-lg transition-colors"
      >
        <LogOut className="h-4 w-4" />
        <span className="text-sm">Sign Out</span>
      </button>
    </div>
  );
}