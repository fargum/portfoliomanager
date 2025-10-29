'use client';

import { PortfolioDashboard } from '@/components/PortfolioDashboard';
import { useState, useEffect } from 'react';
import { apiClient } from '@/lib/api-client';
import { Building2, AlertCircle, CheckCircle, Bot } from 'lucide-react';

const HARDCODED_ACCOUNT_ID = 1;

export default function HomePage() {
  const [apiStatus, setApiStatus] = useState<'checking' | 'online' | 'offline'>('checking');
  const [aiStatus, setAiStatus] = useState<'checking' | 'online' | 'offline'>('checking');

  useEffect(() => {
    // Check API and AI service connectivity on page load
    const checkServices = async () => {
      try {
        // Check main API
        const isApiOnline = await apiClient.testConnection();
        setApiStatus(isApiOnline ? 'online' : 'offline');

        // Check AI chat service
        const aiHealth = await apiClient.getChatHealthStatus();
        setAiStatus(aiHealth.error ? 'offline' : 'online');
      } catch {
        setApiStatus('offline');
        setAiStatus('offline');
      }
    };

    checkServices();
  }, []);

  return (
    <div className="min-h-screen bg-gradient-to-br from-financial-gray-50 to-financial-gray-100">
      {/* Header */}
      <header className="bg-white shadow-financial border-b border-financial-gray-200">
        <div className="w-full px-4 sm:px-6 lg:px-8 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-3">
              <Building2 className="h-8 w-8 text-primary-600" />
              <div>
                <h1 className="text-2xl font-bold text-financial-gray-900">Portfolio Manager</h1>
                <p className="text-sm text-financial-gray-500">AI-Powered Investment Management</p>
              </div>
            </div>
            
            {/* Service Status Indicators */}
            <div className="flex items-center space-x-4">
              {/* API Status */}
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

              {/* AI Status */}
              <div className="flex items-center space-x-2">
                {aiStatus === 'checking' && (
                  <div className="flex items-center space-x-2 text-financial-gray-500">
                    <div className="animate-spin h-4 w-4 border-2 border-financial-gray-300 border-t-blue-600 rounded-full"></div>
                    <span className="text-sm">Checking AI...</span>
                  </div>
                )}
                {aiStatus === 'online' && (
                  <div className="flex items-center space-x-2 text-blue-600">
                    <Bot className="h-4 w-4" />
                    <span className="text-sm font-medium">AI Online</span>
                  </div>
                )}
                {aiStatus === 'offline' && (
                  <div className="flex items-center space-x-2 text-financial-red">
                    <AlertCircle className="h-4 w-4" />
                    <span className="text-sm font-medium">AI Offline</span>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="w-full px-4 sm:px-6 lg:px-8 py-8">
        {/* Service Status Warnings */}
        {(apiStatus === 'offline' || aiStatus === 'offline') && (
          <div className="mb-8 space-y-4">
            {apiStatus === 'offline' && (
              <div className="bg-red-50 border border-red-200 rounded-xl p-6">
                <div className="flex items-center space-x-3">
                  <AlertCircle className="h-6 w-6 text-red-500" />
                  <div>
                    <h3 className="text-lg font-semibold text-red-800">API Connection Error</h3>
                    <p className="text-red-600 mt-1">
                      Unable to connect to the Portfolio Manager API. Portfolio data may not be available.
                    </p>
                    <p className="text-sm text-red-500 mt-2">
                      Expected API endpoint: {process.env.NEXT_PUBLIC_API_BASE_URL || 'http://localhost:8080'}
                    </p>
                  </div>
                </div>
              </div>
            )}
            
            {aiStatus === 'offline' && (
              <div className="bg-yellow-50 border border-yellow-200 rounded-xl p-6">
                <div className="flex items-center space-x-3">
                  <Bot className="h-6 w-6 text-yellow-600" />
                  <div>
                    <h3 className="text-lg font-semibold text-yellow-800">AI Assistant Unavailable</h3>
                    <p className="text-yellow-700 mt-1">
                      The AI assistant is currently offline. Chat functionality will be limited.
                    </p>
                    <p className="text-sm text-yellow-600 mt-2">
                      Portfolio data will still be available if the main API is online.
                    </p>
                  </div>
                </div>
              </div>
            )}
          </div>
        )}

        {/* Portfolio Dashboard */}
        <div className="w-full h-auto">
          <PortfolioDashboard accountId={HARDCODED_ACCOUNT_ID} />
        </div>

        {/* Footer */}
        <footer className="mt-12 text-center text-financial-gray-500">
          <div className="bg-white rounded-lg p-6 shadow-financial">
            <p className="text-sm">
              Portfolio Manager v2.0 - AI-Powered Portfolio Analysis
            </p>
            <p className="text-xs mt-2">
              Account ID: {HARDCODED_ACCOUNT_ID} | Built with Next.js, TypeScript, and Azure OpenAI
            </p>
            <div className="flex justify-center items-center space-x-4 mt-3 text-xs">
              <span className="flex items-center space-x-1">
                <div className={`w-2 h-2 rounded-full ${apiStatus === 'online' ? 'bg-green-500' : 'bg-red-500'}`}></div>
                <span>Portfolio API</span>
              </span>
              <span className="flex items-center space-x-1">
                <div className={`w-2 h-2 rounded-full ${aiStatus === 'online' ? 'bg-blue-500' : 'bg-red-500'}`}></div>
                <span>AI Assistant</span>
              </span>
            </div>
          </div>
        </footer>
      </main>
    </div>
  );
}