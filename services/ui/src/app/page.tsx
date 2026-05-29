'use client';

import { HoldingsGrid } from '@/components/HoldingsGrid';
import { AiChat } from '@/components/AiChat';
import { AuthButton } from '@/components/AuthButton';
import { ThemeToggle } from '@/components/ThemeToggle';
import { useState, useEffect } from 'react';
import { useAuth } from '@/contexts/AuthContext';
import { apiClient } from '@/lib/api-client';
import { Building2, AlertCircle, CheckCircle, Bot, BarChart3, MessageSquare, Sparkles } from 'lucide-react';

const HARDCODED_ACCOUNT_ID = 1;

interface TabProps {
  id: string;
  label: string;
  icon: React.ReactNode;
  isActive: boolean;
  onClick: () => void;
}

function Tab({ id, label, icon, isActive, onClick }: TabProps) {
  return (
    <button
      onClick={onClick}
      className={`group flex items-center space-x-2 px-3 py-1.5 rounded-md font-medium transition-all duration-300 transform hover:scale-105 ${
        isActive
          ? 'bg-gradient-to-r from-financial-blue-600 to-financial-indigo-600 dark:from-financial-slate-700 dark:via-financial-indigo-900 dark:to-financial-slate-800 text-white shadow-lg shadow-financial-blue-600/30 dark:shadow-financial-indigo-950/60'
          : 'text-financial-slate-600 dark:text-financial-slate-300 hover:text-financial-blue-600 dark:hover:text-financial-blue-400 hover:bg-white/70 dark:hover:bg-financial-slate-800/70 hover:shadow-md'
      }`}
    >
      <span className={`transition-transform duration-300 ${
        isActive ? 'scale-110' : 'group-hover:scale-110'
      }`}>
        {icon}
      </span>
      <span className="text-xs font-semibold">{label}</span>
    </button>
  );
}

export default function HomePage() {
  const [apiStatus, setApiStatus] = useState<'checking' | 'online' | 'offline'>('checking');
  const [aiStatus, setAiStatus] = useState<'checking' | 'online' | 'offline'>('checking');
  const [activeTab, setActiveTab] = useState<'holdings' | 'chat'>('holdings');
  const { isAuthenticated } = useAuth();

  useEffect(() => {
    // Check API and AI service connectivity on page load and when auth changes
    const checkServices = async () => {
      console.log('Checking services, authenticated:', isAuthenticated);
      try {
        // Check main API
        const isApiOnline = await apiClient.testConnection();
        setApiStatus(isApiOnline ? 'online' : 'offline');

        // Check AI chat service - only if authenticated
        if (isAuthenticated) {
          setAiStatus('checking');
          try {
            const aiHealth = await apiClient.getChatHealthStatus();
            setAiStatus(aiHealth.error ? 'offline' : 'online');
          } catch (error) {
            console.log('AI service check failed:', error);
            setAiStatus('offline');
          }
        } else {
          // When not authenticated, AI service is unavailable
          console.log('Not authenticated, setting AI status to offline');
          setAiStatus('offline');
        }
      } catch {
        setApiStatus('offline');
        setAiStatus('offline');
      }
    };

    checkServices();
  }, [isAuthenticated]); // Re-run when authentication state changes

  return (
    <div className="h-screen bg-gradient-to-br from-financial-slate-50 via-financial-blue-50 to-financial-indigo-50 dark:from-financial-slate-950 dark:via-financial-slate-900 dark:to-financial-slate-900 flex flex-col transition-colors duration-300">
      {/* Header with Integrated Navigation */}
      <header className="bg-white/80 dark:bg-financial-slate-900/80 backdrop-blur-xl shadow-financial-lg border-b border-white/20 dark:border-financial-slate-800 flex-shrink-0 relative">
        <div className="absolute inset-0 bg-gradient-to-r from-financial-blue-500/5 to-financial-indigo-500/5 dark:from-financial-blue-900/20 dark:to-financial-indigo-900/20"></div>
        <div className="relative w-full px-2 sm:px-4 lg:px-8 py-2">
          {/* Mobile Layout - Stacked */}
          <div className="md:hidden flex flex-col space-y-2">
            {/* Top row - Branding and Auth */}
            <div className="flex items-center justify-between">
              <div className="flex items-center space-x-2">
                <Building2 className="h-5 w-5 text-financial-blue-600" />
                <div>
                  <h1 className="text-base font-bold bg-gradient-to-r from-financial-slate-800 to-financial-blue-600 dark:from-financial-slate-100 dark:to-financial-blue-400 bg-clip-text text-transparent">
                    Portfolio Manager
                  </h1>
                </div>
              </div>
              <div className="flex items-center space-x-2">
                <ThemeToggle />
                <AuthButton />
              </div>
            </div>
            {/* Bottom row - Tabs */}
            <div className="flex space-x-1 bg-white/60 dark:bg-financial-slate-900/50 backdrop-blur-md p-1 rounded-lg border border-white/30 dark:border-financial-slate-700 shadow-financial">
              <Tab
                id="holdings"
                label="Holdings"
                icon={<BarChart3 className="h-4 w-4" />}
                isActive={activeTab === 'holdings'}
                onClick={() => setActiveTab('holdings')}
              />
              <Tab
                id="chat"
                label="AI Chat"
                icon={<MessageSquare className="h-4 w-4" />}
                isActive={activeTab === 'chat'}
                onClick={() => setActiveTab('chat')}
              />
            </div>
          </div>

          {/* Desktop Layout - Original */}
          <div className="hidden md:flex items-center justify-between">
            {/* Left side - Branding and Navigation */}
            <div className="flex items-center space-x-6">
              <div className="flex items-center space-x-3">
                <div className="relative">
                  <Building2 className="h-6 w-6 text-financial-blue-600 dark:text-financial-blue-400 relative z-10" />
                  <div className="absolute inset-0 bg-financial-blue-400/20 rounded-lg scale-150 animate-pulse-slow"></div>
                </div>
                <div>
                  <h1 className="text-xl font-bold bg-gradient-to-r from-financial-slate-800 to-financial-blue-600 dark:from-financial-slate-100 dark:to-financial-blue-300 bg-clip-text text-transparent">
                    Portfolio Manager
                  </h1>
                  <p className="text-xs text-financial-slate-500 dark:text-financial-slate-400 font-medium">AI-Powered Investment Management</p>
                </div>
              </div>
              
              {/* Tab Navigation in Header */}
              <div className="flex space-x-1 bg-white/60 dark:bg-financial-slate-900/50 backdrop-blur-md p-1 rounded-lg border border-white/30 dark:border-financial-slate-700 shadow-financial">
                <Tab
                  id="holdings"
                  label="Portfolio Holdings"
                  icon={<BarChart3 className="h-4 w-4" />}
                  isActive={activeTab === 'holdings'}
                  onClick={() => setActiveTab('holdings')}
                />
                <Tab
                  id="chat"
                  label="AI Assistant"
                  icon={<MessageSquare className="h-4 w-4" />}
                  isActive={activeTab === 'chat'}
                  onClick={() => setActiveTab('chat')}
                />
              </div>
            </div>
            
            {/* Right side - Service Status Indicators and Auth */}
            <div className="flex items-center space-x-4">
              {/* Authentication */}
              <div className="flex-shrink-0">
                <AuthButton />
              </div>

              <ThemeToggle />

              {/* Divider - Desktop only */}
              <div className="hidden lg:block h-6 w-px bg-gray-300 dark:bg-financial-slate-700"></div>

              {/* API Status - Desktop only */}
              <div className="hidden lg:flex items-center space-x-2">
                {!isAuthenticated && (
                  <div className="flex items-center space-x-2 text-financial-gray-400">
                    <Building2 className="h-4 w-4" />
                    <span className="text-sm">API (Auth Required)</span>
                  </div>
                )}
                {isAuthenticated && apiStatus === 'checking' && (
                  <div className="flex items-center space-x-2 text-financial-gray-500">
                    <div className="animate-spin h-4 w-4 border-2 border-financial-gray-300 border-t-primary-600 rounded-full"></div>
                    <span className="text-sm">Checking API...</span>
                  </div>
                )}
                {isAuthenticated && apiStatus === 'online' && (
                  <div className="flex items-center space-x-2 text-financial-green">
                    <CheckCircle className="h-4 w-4" />
                    <span className="text-sm font-medium">API Online</span>
                  </div>
                )}
                {isAuthenticated && apiStatus === 'offline' && (
                  <div className="flex items-center space-x-2 text-financial-red">
                    <AlertCircle className="h-4 w-4" />
                    <span className="text-sm font-medium">API Offline</span>
                  </div>
                )}
              </div>

              {/* AI Status - Desktop only */}
              <div className="hidden lg:flex items-center space-x-2">
                {!isAuthenticated && (
                  <div className="flex items-center space-x-2 text-financial-gray-400">
                    <Bot className="h-4 w-4" />
                    <span className="text-sm">AI (Auth Required)</span>
                  </div>
                )}
                {isAuthenticated && aiStatus === 'checking' && (
                  <div className="flex items-center space-x-2 text-financial-gray-500">
                    <div className="animate-spin h-4 w-4 border-2 border-financial-gray-300 border-t-blue-600 rounded-full"></div>
                    <span className="text-sm">Checking AI...</span>
                  </div>
                )}
                {isAuthenticated && aiStatus === 'online' && (
                  <div className="flex items-center space-x-2 text-blue-600">
                    <Bot className="h-4 w-4" />
                    <span className="text-sm font-medium">AI Online</span>
                  </div>
                )}
                {isAuthenticated && aiStatus === 'offline' && (
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

      {/* Service Status Warnings */}
      {(apiStatus === 'offline' || (aiStatus === 'offline' && isAuthenticated) || (!isAuthenticated && activeTab === 'chat')) && (
        <div className="flex-shrink-0 px-2 sm:px-4 lg:px-8 pt-2">
          <div className="space-y-2">
            {apiStatus === 'offline' && (
                <div className="bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-900 rounded-xl p-4">
                <div className="flex items-center space-x-3">
                  <AlertCircle className="h-5 w-5 text-red-500" />
                  <div>
                      <h3 className="text-sm font-semibold text-red-800 dark:text-red-200">API Connection Error</h3>
                      <p className="text-xs text-red-600 dark:text-red-300 mt-1">
                      Unable to connect to the Portfolio Manager API. Portfolio data may not be available.
                    </p>
                  </div>
                </div>
              </div>
            )}
            
            {!isAuthenticated && activeTab === 'chat' && (
              <div className="bg-blue-50 dark:bg-blue-950/30 border border-blue-200 dark:border-blue-900 rounded-xl p-4">
                <div className="flex items-center space-x-3">
                  <Bot className="h-5 w-5 text-blue-600" />
                  <div>
                    <h3 className="text-sm font-semibold text-blue-800 dark:text-blue-200">Authentication Required</h3>
                    <p className="text-xs text-blue-700 dark:text-blue-300 mt-1">
                      Please sign in to access the AI assistant and chat functionality.
                    </p>
                  </div>
                </div>
              </div>
            )}
            
            {aiStatus === 'offline' && isAuthenticated && (
              <div className="bg-yellow-50 dark:bg-yellow-950/30 border border-yellow-200 dark:border-yellow-900 rounded-xl p-4">
                <div className="flex items-center space-x-3">
                  <Bot className="h-5 w-5 text-yellow-600" />
                  <div>
                    <h3 className="text-sm font-semibold text-yellow-800 dark:text-yellow-200">AI Assistant Unavailable</h3>
                    <p className="text-xs text-yellow-700 dark:text-yellow-300 mt-1">
                      The AI assistant is currently offline. Chat functionality will be limited.
                    </p>
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Main Content Area */}
      <main className="flex-1 overflow-hidden px-1 sm:px-2 lg:px-8 pt-1 sm:pt-2 pb-1 sm:pb-2">
        <div className="h-full bg-white/90 dark:bg-financial-slate-900/90 backdrop-blur-sm rounded-lg sm:rounded-2xl shadow-financial-lg border border-white/20 dark:border-financial-slate-800 overflow-hidden relative">
          <div className="absolute inset-0 bg-gradient-to-br from-white/50 dark:from-financial-slate-900/40 to-financial-blue-50/30 dark:to-financial-slate-900/80 pointer-events-none"></div>
          
          {/* Holdings Tab Content */}
          <div className={activeTab === 'holdings' ? 'block h-full relative z-10' : 'hidden'}>
            <div className="h-full overflow-hidden">
              <HoldingsGrid />
            </div>
          </div>

          {/* Chat Tab Content */}
          <div className={activeTab === 'chat' ? 'block h-full' : 'hidden'}>
            <div className="bg-gradient-to-r from-financial-blue-600 to-financial-indigo-600 dark:from-financial-slate-800 dark:via-financial-indigo-900 dark:to-financial-slate-800 text-white p-2 sm:p-4 relative z-10">
              <div className="flex items-center space-x-2 sm:space-x-3">
                <MessageSquare className="h-4 w-4 sm:h-5 sm:w-5 flex-shrink-0" />
                <div className="min-w-0">
                  <h2 className="text-sm sm:text-lg font-semibold">AI Portfolio Assistant</h2>
                  <p className="text-blue-100 dark:text-financial-slate-300 text-xs sm:text-sm hidden sm:block">
                    Ask questions about your portfolio and get AI-powered insights for Account {HARDCODED_ACCOUNT_ID}
                  </p>
                </div>
              </div>
            </div>
            <div className="h-[calc(100%-3rem)] sm:h-[calc(100%-5rem)] overflow-hidden">
              <AiChat accountId={HARDCODED_ACCOUNT_ID} isVisible={activeTab === 'chat'} />
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}