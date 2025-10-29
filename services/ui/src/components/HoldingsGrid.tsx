'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { AgGridReact } from 'ag-grid-react';
import { HoldingResponse } from '@/types/api';
import { apiClient } from '@/lib/api-client';
import { getHoldingsColumnDefs, getGridOptions, calculateTotalValue, formatCurrency } from '@/lib/grid-utils';
import { Calendar, Search, TrendingUp, PoundSterling, PieChart, RefreshCw } from 'lucide-react';

// AG Grid CSS imports (we'll handle these in the layout)
import 'ag-grid-community/styles/ag-grid.css';
import 'ag-grid-community/styles/ag-theme-alpine.css';

interface HoldingsGridProps {
  accountId: number;
}

export const HoldingsGrid: React.FC<HoldingsGridProps> = ({ accountId }) => {
  const [holdings, setHoldings] = useState<HoldingResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [valuationDate, setValuationDate] = useState<string>(
    new Date().toISOString().split('T')[0]
  );

  const fetchHoldings = useCallback(async () => {
    setLoading(true);
    setError(null);
    
    try {
      const response = await apiClient.getHoldings(accountId, valuationDate);
      
      if (response.error) {
        setError(response.error);
        setHoldings([]);
      } else {
        setHoldings(response.data || []);
      }
    } catch (err) {
      setError('Failed to fetch holdings');
      setHoldings([]);
    } finally {
      setLoading(false);
    }
  }, [accountId, valuationDate]);

  useEffect(() => {
    fetchHoldings();
  }, [fetchHoldings]);

  const totalValue = calculateTotalValue(holdings);
  const holdingsCount = holdings.length;

  return (
    <div className="w-full h-full bg-white">
      {/* Header Section - Simplified */}
      <div className="bg-gradient-to-r from-primary-600 to-primary-700 p-4 text-white">
        <div className="flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <PieChart className="h-6 w-6" />
            <div>
              <h2 className="text-lg font-bold">Portfolio Holdings</h2>
              <p className="text-primary-100 text-sm">Account: {accountId}</p>
            </div>
          </div>
          
          <div className="flex items-center space-x-4">
            {/* Date Input */}
            <div className="flex items-center space-x-2">
              <Calendar className="h-4 w-4" />
              <input
                type="date"
                value={valuationDate}
                onChange={(e) => setValuationDate(e.target.value)}
                className="bg-transparent border-b border-white/30 text-white text-sm focus:border-white focus:outline-none"
              />
            </div>
            
            {/* Stats */}
            <div className="text-right">
              <p className="text-xs text-primary-100">Total Value</p>
              <p className="text-lg font-bold">{formatCurrency(totalValue)}</p>
            </div>
            
            <button
              onClick={fetchHoldings}
              disabled={loading}
              className="flex items-center space-x-2 bg-white/20 hover:bg-white/30 px-3 py-2 rounded-lg transition-colors duration-200"
            >
              <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
              <span className="text-sm">Refresh</span>
            </button>
          </div>
        </div>
      </div>

      {/* Content Section */}
      <div className="w-full h-[calc(100%-80px)] flex flex-col">
        {error && (
          <div className="mx-4 mt-4 p-3 bg-red-50 border border-red-200 rounded-lg">
            <div className="flex items-center space-x-2 text-red-700">
              <svg className="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
              </svg>
              <span className="font-medium text-sm">Error: {error}</span>
            </div>
          </div>
        )}

        {/* Grid Container - Full width with proper scrolling */}
        <div className="flex-1 w-full px-4 pb-4">
          <div className="ag-theme-alpine w-full h-full">
            <AgGridReact
              rowData={holdings}
              columnDefs={getHoldingsColumnDefs()}
              defaultColDef={{
                sortable: true,
                filter: true,
                resizable: true,
                minWidth: 120,
                flex: 1, // Allow columns to grow to fill available space
              }}
              pagination={true}
              paginationPageSize={50}
              rowSelection="multiple"
              suppressHorizontalScroll={false} // Enable horizontal scroll when needed
              alwaysShowHorizontalScroll={false} // Only show when needed
              suppressColumnVirtualisation={false} // Enable column virtualization
              maintainColumnOrder={true}
            />
          </div>
        </div>

        {/* Footer Summary */}
        {holdings.length > 0 && (
          <div className="px-4 pb-4">
            <div className="bg-gray-50 rounded-lg p-3">
              <div className="flex justify-between items-center text-sm text-gray-600">
                <span>Showing {holdings.length} holdings</span>
                <span className="font-semibold">
                  Total Portfolio Value: <span className="text-green-600 text-base">{formatCurrency(totalValue)}</span>
                </span>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};