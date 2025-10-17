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
    <div className="w-full h-full bg-white rounded-xl shadow-financial-lg border border-financial-gray-300">
      {/* Header Section */}
      <div className="bg-gradient-to-r from-primary-600 to-primary-700 rounded-t-xl p-6 text-white">
        <div className="flex items-center justify-between mb-6">
          <div className="flex items-center space-x-3">
            <PieChart className="h-8 w-8" />
            <div>
              <h1 className="text-2xl font-bold">Portfolio Holdings</h1>
              <p className="text-primary-100">Account: {accountId}</p>
            </div>
          </div>
          <button
            onClick={fetchHoldings}
            disabled={loading}
            className="flex items-center space-x-2 bg-white/20 hover:bg-white/30 px-4 py-2 rounded-lg transition-colors duration-200"
          >
            <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
            <span>Refresh</span>
          </button>
        </div>

        {/* Stats Cards */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
          <div className="bg-white/10 rounded-lg p-4 backdrop-blur-sm">
            <div className="flex items-center space-x-3">
              <PoundSterling className="h-6 w-6 text-financial-green" />
              <div>
                <p className="text-sm text-primary-100">Total Value</p>
                <p className="text-xl font-bold">{formatCurrency(totalValue)}</p>
              </div>
            </div>
          </div>
          
          <div className="bg-white/10 rounded-lg p-4 backdrop-blur-sm">
            <div className="flex items-center space-x-3">
              <TrendingUp className="h-6 w-6 text-financial-blue" />
              <div>
                <p className="text-sm text-primary-100">Holdings Count</p>
                <p className="text-xl font-bold">{holdingsCount}</p>
              </div>
            </div>
          </div>

          <div className="bg-white/10 rounded-lg p-4 backdrop-blur-sm">
            <div className="flex items-center space-x-3">
              <Calendar className="h-6 w-6 text-financial-yellow" />
              <div>
                <p className="text-sm text-primary-100">Valuation Date</p>
                <input
                  type="date"
                  value={valuationDate}
                  onChange={(e) => setValuationDate(e.target.value)}
                  className="bg-transparent border-b border-white/30 text-white placeholder-primary-200 focus:border-white focus:outline-none text-lg font-semibold"
                />
              </div>
            </div>
          </div>
        </div>

        {/* Search Button */}
        <div className="flex justify-center">
          <button
            onClick={fetchHoldings}
            disabled={loading}
            className="flex items-center space-x-2 bg-white text-primary-600 hover:bg-primary-50 px-6 py-3 rounded-lg font-semibold transition-colors duration-200 shadow-lg"
          >
            <Search className="h-5 w-5" />
            <span>{loading ? 'Loading...' : 'Search Holdings'}</span>
          </button>
        </div>
      </div>

      {/* Content Section */}
      <div className="p-6">
        {error && (
          <div className="mb-4 p-4 bg-red-50 border border-red-200 rounded-lg">
            <div className="flex items-center space-x-2 text-red-700">
              <svg className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
              </svg>
              <span className="font-medium">Error: {error}</span>
            </div>
          </div>
        )}

        {/* Grid Container */}
        <div className="ag-theme-alpine w-full" style={{ height: '600px' }}>
          <AgGridReact
            rowData={holdings}
            columnDefs={getHoldingsColumnDefs()}
            defaultColDef={{
              sortable: true,
              filter: true,
              resizable: true,
              minWidth: 100,
            }}
            pagination={true}
            paginationPageSize={50}
            rowSelection="multiple"
          />
        </div>

        {/* Footer Summary */}
        {holdings.length > 0 && (
          <div className="mt-6 bg-financial-gray-50 rounded-lg p-4">
            <div className="flex justify-between items-center text-sm text-financial-gray-600">
              <span>Showing {holdings.length} holdings</span>
              <span className="font-semibold">
                Total Portfolio Value: <span className="text-financial-green text-lg">{formatCurrency(totalValue)}</span>
              </span>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};