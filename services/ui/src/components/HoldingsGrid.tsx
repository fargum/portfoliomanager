'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { AgGridReact } from 'ag-grid-react';
import { HoldingResponse } from '@/types/api';
import { apiClient } from '@/lib/api-client';
import { getHoldingsColumnDefs, getGridOptions, calculateTotalValue, calculateTotalDailyPnL, calculateAverageDailyPnLPercentage, formatCurrency } from '@/lib/grid-utils';
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
  const [filteredTotalValue, setFilteredTotalValue] = useState<number>(0);
  const [filteredCount, setFilteredCount] = useState<number>(0);
  const [gridApi, setGridApi] = useState<any>(null);
  const [isGrouped, setIsGrouped] = useState(false);
  const [displayData, setDisplayData] = useState<any[]>([]);

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

  // Calculate totals from filtered/displayed data
  const updateFilteredTotals = useCallback(() => {
    if (!gridApi) return;
    
    let totalValue = 0;
    let count = 0;
    
    gridApi.forEachNodeAfterFilter((node: any) => {
      if (node.data) {
        totalValue += node.data.currentValue || 0;
        count++;
      }
    });
    
    setFilteredTotalValue(totalValue);
    setFilteredCount(count);
  }, [gridApi]);

  // Grid event handlers
  const onGridReady = useCallback((params: any) => {
    setGridApi(params.api);
    // Initial calculation
    setTimeout(() => {
      let totalValue = 0;
      let count = 0;
      
      params.api.forEachNodeAfterFilter((node: any) => {
        if (node.data) {
          totalValue += node.data.currentValue || 0;
          count++;
        }
      });
      
      setFilteredTotalValue(totalValue);
      setFilteredCount(count);
    }, 100);
  }, []);

  const onFilterChanged = useCallback(() => {
    updateFilteredTotals();
  }, [updateFilteredTotals]);

  // Update filtered totals when holdings data changes (e.g., after date change)
  useEffect(() => {
    updateFilteredTotals();
  }, [holdings, updateFilteredTotals]);

  useEffect(() => {
    fetchHoldings();
  }, [fetchHoldings]);

  const totalValue = calculateTotalValue(holdings);
  const totalDailyPnL = calculateTotalDailyPnL(holdings);
  const avgDailyPnLPercentage = calculateAverageDailyPnLPercentage(holdings);
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
            <div className="text-right space-y-1">
              <div>
                <p className="text-xs text-primary-100">
                  {filteredCount !== holdings.length ? 'Filtered' : 'Total'} Value
                </p>
                <p className="text-lg font-bold">
                  {formatCurrency(filteredTotalValue > 0 ? filteredTotalValue : totalValue)}
                </p>
                {filteredCount !== holdings.length && (
                  <p className="text-xs text-primary-200">
                    Total: {formatCurrency(totalValue)}
                  </p>
                )}
              </div>
              <div>
                <p className="text-xs text-primary-100">Daily P&L</p>
                <p className={`text-sm font-bold ${totalDailyPnL >= 0 ? 'text-green-200' : 'text-red-200'}`}>
                  {formatCurrency(totalDailyPnL)} ({avgDailyPnLPercentage >= 0 ? '+' : ''}{avgDailyPnLPercentage.toFixed(2)}%)
                </p>
              </div>
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

        {/* Grid Container - Full width with proper scrolling and space for row group panel */}
        <div className="flex-1 w-full px-4 pb-4">
          <div className="ag-theme-alpine w-full h-full" style={{ minHeight: '500px' }}>
            <AgGridReact
              rowData={holdings}
              columnDefs={getHoldingsColumnDefs()}
              onGridReady={onGridReady}
              onFilterChanged={onFilterChanged}
              defaultColDef={{
                sortable: true,
                filter: true,
                resizable: true,
                minWidth: 120,
                flex: 1, // Allow columns to grow to fill available space
              }}
              // Enable aggregation
              enableRangeSelection={true}
              aggFuncs={{
                // Keep basic aggregation functions for selection ranges
                'sum': (params: any) => {
                  const values = params.values.filter((val: any) => val != null && !isNaN(val));
                  return values.reduce((sum: number, val: number) => sum + val, 0);
                },
                'avg': (params: any) => {
                  const values = params.values.filter((val: any) => val != null && !isNaN(val));
                  if (values.length === 0) return 0;
                  return values.reduce((sum: number, val: number) => sum + val, 0) / values.length;
                },
                'count': (params: any) => {
                  return params.values.filter((val: any) => val != null).length;
                }
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

        {/* Footer Summary - Dynamic based on filters */}
        {holdings.length > 0 && (
          <div className="px-4 pb-4">
            <div className="bg-gray-50 rounded-lg p-3">
              <div className="flex justify-between items-center text-sm text-gray-600">
                <div className="flex items-center space-x-4">
                  <span>
                    Showing {filteredCount} of {holdings.length} holdings
                    {filteredCount !== holdings.length && (
                      <span className="text-blue-600 font-medium"> (filtered)</span>
                    )}
                  </span>
                </div>
                <div className="flex items-center space-x-6">
                  {filteredCount !== holdings.length && (
                    <span className="text-xs text-gray-500">
                      Total Portfolio: {formatCurrency(totalValue)}
                    </span>
                  )}
                  <span className="font-semibold">
                    {filteredCount !== holdings.length ? 'Filtered' : 'Total'} Value: 
                    <span className="text-green-600 text-base ml-1">
                      {formatCurrency(filteredTotalValue > 0 ? filteredTotalValue : totalValue)}
                    </span>
                  </span>
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};