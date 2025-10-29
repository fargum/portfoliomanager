'use client';

import React, { useState } from 'react';
import { HoldingsGrid } from '@/components/HoldingsGrid';
import { AiChat } from '@/components/AiChat';
import { MessageSquare, BarChart3 } from 'lucide-react';

interface PortfolioDashboardProps {
  accountId: number;
}

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
      className={`flex items-center space-x-2 px-6 py-3 rounded-lg font-medium transition-all duration-200 ${
        isActive
          ? 'bg-blue-600 text-white shadow-lg'
          : 'text-gray-600 hover:text-gray-900 hover:bg-gray-100'
      }`}
    >
      {icon}
      <span>{label}</span>
    </button>
  );
}

export function PortfolioDashboard({ accountId }: PortfolioDashboardProps) {
  const [activeTab, setActiveTab] = useState<'holdings' | 'chat'>('holdings');

  return (
    <div className="w-full h-full flex flex-col">
      {/* Tab Navigation */}
      <div className="mb-6">
        <div className="flex space-x-2 bg-gray-50 p-2 rounded-lg inline-flex">
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

      {/* Tab Content */}
      <div className="flex-1 bg-white rounded-xl shadow-lg overflow-hidden">
        {activeTab === 'holdings' && (
          <>
            <div className="bg-gray-50 border-b border-gray-200 p-6">
              <div className="flex items-center space-x-3">
                <BarChart3 className="h-6 w-6 text-blue-600" />
                <div>
                  <h2 className="text-xl font-semibold text-gray-900">Portfolio Holdings</h2>
                  <p className="text-sm text-gray-600 mt-1">
                    View and manage your portfolio holdings for Account {accountId}
                  </p>
                </div>
              </div>
            </div>
            <div className="h-[calc(100vh-12rem)]">
              <HoldingsGrid accountId={accountId} />
            </div>
          </>
        )}

        {activeTab === 'chat' && (
          <>
            <div className="bg-gradient-to-r from-blue-600 to-blue-700 text-white p-6">
              <div className="flex items-center space-x-3">
                <MessageSquare className="h-6 w-6" />
                <div>
                  <h2 className="text-xl font-semibold">AI Portfolio Assistant</h2>
                  <p className="text-blue-100 text-sm mt-1">
                    Ask questions about your portfolio and get AI-powered insights for Account {accountId}
                  </p>
                </div>
              </div>
            </div>
            <div className="h-[calc(100vh-12rem)]">
              <AiChat accountId={accountId} />
            </div>
          </>
        )}
      </div>
    </div>
  );
}