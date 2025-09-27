'use client';

import { HoldingsGrid } from '@/components/HoldingsGrid';
import { useState, useEffect } from 'react';
import { apiClient } from '@/lib/api-client';
import { Building2, AlertCircle, CheckCircle } from 'lucide-react';

const HARDCODED_ACCOUNT_ID = '49bc4123-5312-43d9-81bd-6c6f81e80bac';

export default function HomePage() {
  const [apiStatus, setApiStatus] = useState<'checking' | 'online' | 'offline'>('checking');

  useEffect(() => {
    // Check API connectivity on page load
    const checkApiStatus = async () => {
      try {
        const isOnline = await apiClient.testConnection();
        setApiStatus(isOnline ? 'online' : 'offline');
      } catch {
        setApiStatus('offline');
      }
    };

    checkApiStatus();
  }, []);

  return (
    <div className="min-h-screen bg-gradient-to-br from-financial-gray-50 to-financial-gray-100">
      {/* Header */}
      <header className="bg-white shadow-financial border-b border-financial-gray-200">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-3">
              <Building2 className="h-8 w-8 text-primary-600" />
              <div>
                <h1 className="text-2xl font-bold text-financial-gray-900">Portfolio Manager</h1>
                <p className="text-sm text-financial-gray-500">Professional Investment Management</p>
              </div>
            </div>
            
            {/* API Status Indicator */}
            <div className="flex items-center space-x-2">
              {apiStatus === 'checking' && (
                <div className="flex items-center space-x-2 text-financial-gray-500">
                  <div className="animate-spin h-4 w-4 border-2 border-financial-gray-300 border-t-primary-600 rounded-full"></div>
                  <span className="text-sm">Checking API...</span>
                </div>
              )}
              {apiStatus === 'online' && (
                <div className="flex items-center space-x-2 text-financial-green">
                  <CheckCircle className="h-4 w-4" />
                  <span className="text-sm font-medium">API Online</span>
                </div>
              )}
              {apiStatus === 'offline' && (
                <div className="flex items-center space-x-2 text-financial-red">
                  <AlertCircle className="h-4 w-4" />
                  <span className="text-sm font-medium">API Offline</span>
                </div>
              )}
            </div>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {apiStatus === 'offline' && (
          <div className="mb-8 bg-red-50 border border-red-200 rounded-xl p-6">
            <div className="flex items-center space-x-3">
              <AlertCircle className="h-6 w-6 text-red-500" />
              <div>
                <h3 className="text-lg font-semibold text-red-800">API Connection Error</h3>
                <p className="text-red-600 mt-1">
                  Unable to connect to the Portfolio Manager API. Please ensure the API service is running and accessible.
                </p>
                <p className="text-sm text-red-500 mt-2">
                  Expected API endpoint: {process.env.NEXT_PUBLIC_API_BASE_URL || 'http://localhost:8080'}
                </p>
              </div>
            </div>
          </div>
        )}

        {/* Holdings Grid */}
        <div className="bg-white rounded-xl shadow-financial-lg overflow-hidden">
          <HoldingsGrid accountId={HARDCODED_ACCOUNT_ID} />
        </div>

        {/* Footer */}
        <footer className="mt-12 text-center text-financial-gray-500">
          <div className="bg-white rounded-lg p-6 shadow-financial">
            <p className="text-sm">
              Portfolio Manager v1.0 - Built with Next.js, TypeScript, and AG Grid
            </p>
            <p className="text-xs mt-2">
              Account ID: {HARDCODED_ACCOUNT_ID}
            </p>
          </div>
        </footer>
      </main>
    </div>
  );
}