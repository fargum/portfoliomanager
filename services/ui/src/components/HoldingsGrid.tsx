'use client';

import React, { useState, useEffect, useCallback, useRef } from 'react';
import { AgGridReact } from 'ag-grid-react';
import { HoldingResponse } from '@/types/api';
import { apiClient } from '@/lib/api-client';
import { getHoldingsColumnDefs, getGridOptions, calculateTotalValue, calculateTotalBoughtValue, calculateTotalGainLoss, calculateTotalGainLossPercentage, calculateTotalDailyPnL, calculateAverageDailyPnLPercentage, formatCurrency } from '@/lib/grid-utils';
import { Calendar, Search, TrendingUp, PoundSterling, PieChart, RefreshCw, Trash2, Plus, X } from 'lucide-react';

// AG Grid CSS imports (we'll handle these in the layout)
import 'ag-grid-community/styles/ag-grid.css';
import 'ag-grid-community/styles/ag-theme-alpine.css';

interface HoldingsGridProps {
  // No props needed - account is determined from authentication
}

export const HoldingsGrid: React.FC<HoldingsGridProps> = () => {
  const [holdings, setHoldings] = useState<HoldingResponse[]>([]);
  const holdingsRef = useRef<HoldingResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [editingHoldings, setEditingHoldings] = useState<Set<number>>(new Set());
  const [selectedRows, setSelectedRows] = useState<HoldingResponse[]>([]);
  const [isDeleting, setIsDeleting] = useState(false);
  const [showAddModal, setShowAddModal] = useState(false);
  const [isAdding, setIsAdding] = useState(false);
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
      const response = await apiClient.getHoldings(valuationDate);
      
      if (response.error) {
        setError(response.error);
        setHoldings([]);
        holdingsRef.current = [];
      } else {
        setHoldings(response.data || []);
        holdingsRef.current = response.data || [];
      }
    } catch (err) {
      const errorMsg = 'Failed to fetch holdings';
      setError(errorMsg);
      setHoldings([]);
      holdingsRef.current = [];
    } finally {
      setLoading(false);
    }
  }, [valuationDate]);

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

  // Handle units cell value changes
  const handleCellValueChanged = useCallback(async (params: any) => {
    const { data, newValue, oldValue, colDef } = params;
    
    // Only handle units changes
    if (colDef.field !== 'unitAmount' || newValue === oldValue) {
      return;
    }
    
    // Validate the new value
    if (newValue <= 0 || isNaN(newValue)) {
      setError('Units must be a positive number');
      // Revert the change
      data.unitAmount = oldValue;
      params.api.refreshCells({ rowNodes: [params.node], force: true });
      return;
    }
    
    const holdingId = data.holdingId;
    
    try {
      // Mark this holding as being edited
      setEditingHoldings(prev => new Set([...Array.from(prev), holdingId]));
      
      // Call the API to update units
      const response = await apiClient.updateHoldingUnits(holdingId, newValue);
      
      if (response.error) {
        setError(`Failed to update units: ${response.error}`);
        // Revert the change
        data.unitAmount = oldValue;
        params.api.refreshCells({ rowNodes: [params.node], force: true });
      } else {
        // Success - refresh the entire row to get updated calculations
        await fetchHoldings();
        
        // Show success message briefly
        const successMessage = `Successfully updated ${data.ticker} units to ${newValue.toFixed(4)}`;
        console.log(successMessage);
        
        // You might want to show a toast notification here
        setTimeout(() => {
          if (error === successMessage) setError(null);
        }, 3000);
      }
    } catch (err) {
      setError(`Error updating units: ${err instanceof Error ? err.message : 'Unknown error'}`);
      // Revert the change
      data.unitAmount = oldValue;
      params.api.refreshCells({ rowNodes: [params.node], force: true });
    } finally {
      // Remove from editing set
      setEditingHoldings(prev => {
        const newSet = new Set(Array.from(prev));
        newSet.delete(holdingId);
        return newSet;
      });
    }
  }, [fetchHoldings, error]);

  // Handle row selection changes
  const onSelectionChanged = useCallback((params: any) => {
    const selectedData = params.api.getSelectedRows() as HoldingResponse[];
    setSelectedRows(selectedData);
  }, []);

  // Handle bulk deletion
  const handleBulkDelete = useCallback(async () => {
    if (selectedRows.length === 0) return;
    
    const confirmMessage = `Are you sure you want to delete ${selectedRows.length} holding${selectedRows.length > 1 ? 's' : ''}?\n\nThis will permanently remove:\n${selectedRows.map(h => `• ${h.ticker} (${h.unitAmount.toFixed(4)} units)`).slice(0, 5).join('\n')}${selectedRows.length > 5 ? `\n• ... and ${selectedRows.length - 5} more` : ''}`;
    
    if (!window.confirm(confirmMessage)) {
      return;
    }
    
    setIsDeleting(true);
    const holdingIds = selectedRows.map(h => h.holdingId);
    const errors: string[] = [];
    let deletedCount = 0;
    
    try {
      // Delete holdings one by one to provide better error reporting
      for (const holdingId of holdingIds) {
        try {
          const response = await apiClient.deleteHolding(holdingId);
          if (response.error) {
            const holding = selectedRows.find(h => h.holdingId === holdingId);
            errors.push(`${holding?.ticker || holdingId}: ${response.error}`);
          } else {
            deletedCount++;
          }
        } catch (err) {
          const holding = selectedRows.find(h => h.holdingId === holdingId);
          errors.push(`${holding?.ticker || holdingId}: ${err instanceof Error ? err.message : 'Unknown error'}`);
        }
      }
      
      // Show results
      if (deletedCount > 0) {
        console.log(`Successfully deleted ${deletedCount} holding${deletedCount > 1 ? 's' : ''}`);
      }
      
      if (errors.length > 0) {
        setError(`Failed to delete some holdings:\n${errors.slice(0, 3).join('\n')}${errors.length > 3 ? `\n... and ${errors.length - 3} more errors` : ''}`);
      } else if (deletedCount > 0) {
        // Clear any previous errors on full success
        setError(null);
      }
      
      // Refresh the grid and clear selection
      await fetchHoldings();
      setSelectedRows([]);
      
    } finally {
      setIsDeleting(false);
    }
  }, [selectedRows, fetchHoldings]);

  // Handle adding new holding
  const handleAddHolding = useCallback(async (holdingData: {
    ticker: string;
    units: number;
    platformId: number;
    description?: string;
    currencyCode?: string;
    quoteUnit?: string;
  }) => {
    setIsAdding(true);
    
    try {
      // Clear any previous errors
      setError(null);
      
      // Get current holdings from ref (always current, not from closure)
      const currentHoldings = holdingsRef.current;
      
      if (currentHoldings.length === 0) {
        setError('No existing holdings found. Portfolio ID cannot be determined.');
        return false;
      }
      
      const portfolioId = currentHoldings[0].portfolioId;
      
      const response = await apiClient.addHolding(portfolioId, {
        platformId: holdingData.platformId,
        ticker: holdingData.ticker,
        units: holdingData.units,
        boughtValue: 0, // Default to 0 - could be enhanced to ask user for this
        instrumentName: holdingData.ticker,
        description: holdingData.description || `${holdingData.ticker} holding`,
        currencyCode: holdingData.currencyCode || 'GBP',
        quoteUnit: holdingData.quoteUnit || 'GBP'
      });
      
      if (response.error) {
        setError(`Failed to add holding: ${response.error}`);
        return false;
      } else {
        // Success - refresh the grid
        await fetchHoldings();
        setError(null);
        return true;
      }
    } catch (err) {
      setError(`Error adding holding: ${err instanceof Error ? err.message : 'Unknown error'}`);
      return false;
    } finally {
      setIsAdding(false);
    }
  }, [holdings, fetchHoldings]);

  useEffect(() => {
    fetchHoldings();
  }, [fetchHoldings]);

  const totalValue = calculateTotalValue(holdings);
  const totalBoughtValue = calculateTotalBoughtValue(holdings);
  const totalGainLoss = calculateTotalGainLoss(holdings);
  const totalGainLossPercentage = calculateTotalGainLossPercentage(holdings);
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
              <p className="text-primary-100 text-sm">Authenticated User's Holdings</p>
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
            
            {/* Compact Stats */}
            <div className="text-right">
              <div className="flex items-baseline space-x-6">
                <div>
                  <p className="text-xs text-primary-100">
                    {filteredCount !== holdings.length ? 'Filtered' : 'Total'} Value
                  </p>
                  <p className="text-xl font-bold">
                    {formatCurrency(filteredTotalValue > 0 ? filteredTotalValue : totalValue)}
                  </p>
                  {filteredCount !== holdings.length && (
                    <p className="text-xs text-primary-200">
                      Total: {formatCurrency(totalValue)}
                    </p>
                  )}
                </div>
                <div>
                  <p className="text-xs text-primary-100">Total P&L</p>
                  <p className={`text-sm font-bold ${totalGainLoss >= 0 ? 'text-green-200' : 'text-red-200'}`}>
                    {formatCurrency(totalGainLoss)}
                  </p>
                  <p className={`text-xs ${totalGainLossPercentage >= 0 ? 'text-green-200' : 'text-red-200'}`}>
                    ({totalGainLossPercentage >= 0 ? '+' : ''}{totalGainLossPercentage.toFixed(2)}%)
                  </p>
                </div>
                <div>
                  <p className="text-xs text-primary-100">Daily P&L</p>
                  <p className={`text-sm font-bold ${totalDailyPnL >= 0 ? 'text-green-200' : 'text-red-200'}`}>
                    {formatCurrency(totalDailyPnL)}
                  </p>
                  <p className={`text-xs ${avgDailyPnLPercentage >= 0 ? 'text-green-200' : 'text-red-200'}`}>
                    ({avgDailyPnLPercentage >= 0 ? '+' : ''}{avgDailyPnLPercentage.toFixed(2)}%)
                  </p>
                </div>
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
            
            <button
              onClick={() => {
                setError(null); // Clear any existing errors
                setShowAddModal(true);
              }}
              disabled={loading || editingHoldings.size > 0 || isDeleting}
              className="flex items-center space-x-2 bg-green-500/20 hover:bg-green-500/30 disabled:bg-green-500/10 px-3 py-2 rounded-lg transition-colors duration-200 disabled:cursor-not-allowed"
            >
              <Plus className="h-4 w-4" />
              <span className="text-sm">Add Holding</span>
            </button>
            
            {selectedRows.length > 0 && (
              <button
                onClick={handleBulkDelete}
                disabled={isDeleting || editingHoldings.size > 0}
                className="flex items-center space-x-2 bg-red-500/20 hover:bg-red-500/30 disabled:bg-red-500/10 px-3 py-2 rounded-lg transition-colors duration-200 disabled:cursor-not-allowed"
              >
                {isDeleting ? (
                  <>
                    <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white"></div>
                    <span className="text-sm">Deleting...</span>
                  </>
                ) : (
                  <>
                    <Trash2 className="h-4 w-4" />
                    <span className="text-sm">Delete {selectedRows.length} Selected</span>
                  </>
                )}
              </button>
            )}
            
            {editingHoldings.size > 0 && (
              <div className="flex items-center space-x-2 bg-yellow-500/20 px-3 py-2 rounded-lg">
                <div className="animate-spin rounded-full h-3 w-3 border-b-2 border-white"></div>
                <span className="text-sm">Updating {editingHoldings.size} holding{editingHoldings.size > 1 ? 's' : ''}...</span>
              </div>
            )}
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
              columnDefs={getHoldingsColumnDefs(handleCellValueChanged)}
              onGridReady={onGridReady}
              onFilterChanged={onFilterChanged}
              onSelectionChanged={onSelectionChanged}
              defaultColDef={{
                sortable: true,
                filter: true,
                resizable: true,
                minWidth: 120,
                flex: 1, // Allow columns to grow to fill available space
              }}
              // Enable cell editing
              singleClickEdit={true}
              stopEditingWhenCellsLoseFocus={true}
              // Enable row selection
              rowSelection={'multiple'}
              suppressRowClickSelection={false}
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
                  <span className="font-semibold">
                    Total P&L: 
                    <span className={`text-base ml-1 ${totalGainLoss >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                      {formatCurrency(totalGainLoss)} ({totalGainLossPercentage >= 0 ? '+' : ''}{totalGainLossPercentage.toFixed(2)}%)
                    </span>
                  </span>
                  <span className="font-semibold">
                    Daily P&L: 
                    <span className={`text-base ml-1 ${totalDailyPnL >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                      {formatCurrency(totalDailyPnL)} ({avgDailyPnLPercentage >= 0 ? '+' : ''}{avgDailyPnLPercentage.toFixed(2)}%)
                    </span>
                  </span>
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
      
      {/* Add Holding Modal */}
      {showAddModal && (
        <AddHoldingModal
          isOpen={showAddModal}
          onClose={() => setShowAddModal(false)}
          onSubmit={handleAddHolding}
          isSubmitting={isAdding}
          availablePlatforms={Array.from(
            new Map(
              holdings.map(h => [h.platformId, { id: h.platformId, name: h.platformName }])
            ).values()
          ).sort((a, b) => a.name.localeCompare(b.name))}
        />
      )}
    </div>
  );
};

// Add Holding Modal Component
interface AddHoldingModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: { ticker: string; units: number; platformId: number; description?: string; currencyCode?: string; quoteUnit?: string; }) => Promise<boolean>;
  isSubmitting: boolean;
  availablePlatforms: { id: number; name: string; }[];
}

const AddHoldingModal: React.FC<AddHoldingModalProps> = ({ isOpen, onClose, onSubmit, isSubmitting, availablePlatforms }) => {
  const [step, setStep] = useState<'ticker' | 'details'>('ticker');
  const [ticker, setTicker] = useState('');
  const [instrumentExists, setInstrumentExists] = useState<boolean | null>(null);
  const [isChecking, setIsChecking] = useState(false);
  const [formData, setFormData] = useState({
    units: '',
    platformId: availablePlatforms.length > 0 ? availablePlatforms[0].id.toString() : '',
    currencyCode: 'GBP',
    quoteUnit: 'GBP',
    description: ''
  });

  // Update platformId when availablePlatforms changes
  useEffect(() => {
    if (availablePlatforms.length > 0 && !formData.platformId) {
      setFormData(prev => ({
        ...prev,
        platformId: availablePlatforms[0].id.toString()
      }));
    }
  }, [availablePlatforms, formData.platformId]);
  const [errors, setErrors] = useState<{[key: string]: string}>({});

  const checkInstrument = async (tickerToCheck: string) => {
    if (!tickerToCheck.trim()) return;
    
    setIsChecking(true);
    setErrors({});
    
    try {
      const result = await apiClient.checkInstrument(tickerToCheck.trim().toUpperCase());
      if (result.error) {
        setErrors({ ticker: result.error });
        return;
      }
      
      setInstrumentExists(result.data?.exists || false);
      setStep('details');
      
      // Pre-fill description for new instruments
      if (!result.data?.exists) {
        setFormData(prev => ({
          ...prev,
          description: `${tickerToCheck.toUpperCase()} holdings`
        }));
      }
    } catch (error) {
      setErrors({ ticker: 'Failed to check instrument' });
    } finally {
      setIsChecking(false);
    }
  };

  const handleTickerSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!ticker.trim()) {
      setErrors({ ticker: 'Ticker is required' });
      return;
    }
    if (!/^[A-Za-z0-9.\\-_]+$/.test(ticker.trim())) {
      setErrors({ ticker: 'Invalid ticker format' });
      return;
    }
    checkInstrument(ticker);
  };

  const validateDetailsForm = () => {
    const newErrors: {[key: string]: string} = {};
    
    const units = parseFloat(formData.units);
    if (!formData.units.trim() || isNaN(units) || units <= 0) {
      newErrors.units = 'Units must be a positive number';
    } else if (units > 999999999) {
      newErrors.units = 'Units cannot exceed 999,999,999';
    }
    
    // Validate platform selection
    const platformId = parseInt(formData.platformId);
    if (!formData.platformId.trim() || isNaN(platformId) || platformId <= 0) {
      newErrors.platformId = 'Please select a platform';
    } else if (availablePlatforms.length > 0 && !availablePlatforms.find(p => p.id === platformId)) {
      newErrors.platformId = 'Selected platform is not valid';
    }
    
    // Validate new instrument fields
    if (!instrumentExists) {
      if (!formData.currencyCode.trim()) {
        newErrors.currencyCode = 'Currency is required for new instruments';
      }
      if (!formData.quoteUnit.trim()) {
        newErrors.quoteUnit = 'Quote unit is required for new instruments';
      }
    }
    
    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleDetailsSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!validateDetailsForm()) {
      return;
    }
    
    const success = await onSubmit({
      ticker: ticker.trim().toUpperCase(),
      units: parseFloat(formData.units),
      platformId: parseInt(formData.platformId),
      description: formData.description,
      currencyCode: formData.currencyCode,
      quoteUnit: formData.quoteUnit
    });
    
    if (success) {
      handleClose();
    }
  };

  const handleClose = () => {
    if (!isSubmitting) {
      setStep('ticker');
      setTicker('');
      setInstrumentExists(null);
      setFormData({ 
        units: '', 
        platformId: availablePlatforms.length > 0 ? availablePlatforms[0].id.toString() : '',
        currencyCode: 'GBP', 
        quoteUnit: 'GBP', 
        description: '' 
      });
      setErrors({});
      onClose();
    }
  };

  const goBackToTicker = () => {
    setStep('ticker');
    setInstrumentExists(null);
    setErrors({});
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-lg shadow-xl max-w-md w-full">
        <div className="p-6">
          <div className="flex justify-between items-center mb-4">
            <h2 className="text-xl font-semibold text-gray-900">Add New Holding</h2>
            <button
              onClick={handleClose}
              disabled={isSubmitting}
              className="text-gray-400 hover:text-gray-600 disabled:cursor-not-allowed"
            >
              <X className="h-6 w-6" />
            </button>
          </div>
          
          <form onSubmit={step === 'ticker' ? handleTickerSubmit : handleDetailsSubmit} className="space-y-4">
            {step === 'ticker' && (
              <>
                <div>
                  <label htmlFor="ticker" className="block text-sm font-medium text-gray-700 mb-1">
                    Ticker Symbol *
                  </label>
                  <input
                    type="text"
                    id="ticker"
                    value={ticker}
                    onChange={(e) => setTicker(e.target.value)}
                    className={`w-full px-3 py-2 border rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 ${errors.ticker ? 'border-red-500' : 'border-gray-300'}`}
                    placeholder="e.g., AAPL, MSFT, TSLA"
                    disabled={isChecking}
                    maxLength={20}
                  />
                  {errors.ticker && <p className="text-red-500 text-xs mt-1">{errors.ticker}</p>}
                  <p className="text-gray-500 text-xs mt-1">
                    Enter the ticker symbol to check if the instrument exists in our system
                  </p>
                </div>
                
                <div className="flex space-x-3 pt-4">
                  <button
                    type="button"
                    onClick={handleClose}
                    disabled={isChecking}
                    className="flex-1 px-4 py-2 border border-gray-300 rounded-md text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:cursor-not-allowed disabled:bg-gray-100"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={isChecking || !ticker.trim()}
                    className="flex-1 px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:bg-blue-400 disabled:cursor-not-allowed flex items-center justify-center"
                  >
                    {isChecking ? (
                      <>
                        <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white mr-2"></div>
                        Checking...
                      </>
                    ) : (
                      'Next'
                    )}
                  </button>
                </div>
              </>
            )}
            
            {step === 'details' && (
              <>
                <div className="bg-gray-50 p-3 rounded-md">
                  <h4 className="font-medium text-gray-900">{ticker}</h4>
                  <p className="text-sm text-gray-600">
                    {instrumentExists ? (
                      <>✅ Instrument exists in our system</>
                    ) : (
                      <>⚠️ New instrument - additional details required</>
                    )}
                  </p>
                </div>
                
                <div>
                  <label htmlFor="units" className="block text-sm font-medium text-gray-700 mb-1">
                    Number of Units *
                  </label>
                  <input
                    type="number"
                    id="units"
                    value={formData.units}
                    onChange={(e) => setFormData({ ...formData, units: e.target.value })}
                    className={`w-full px-3 py-2 border rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 ${errors.units ? 'border-red-500' : 'border-gray-300'}`}
                    placeholder="0.0000"
                    disabled={isSubmitting}
                    min="0.0001"
                    max="999999999"
                    step="0.0001"
                    autoFocus
                  />
                  {errors.units && <p className="text-red-500 text-xs mt-1">{errors.units}</p>}
                </div>
                
                <div>
                  <label htmlFor="platformId" className="block text-sm font-medium text-gray-700 mb-1">
                    Platform *
                  </label>
                  <select
                    id="platformId"
                    value={formData.platformId}
                    onChange={(e) => setFormData({ ...formData, platformId: e.target.value })}
                    className={`w-full px-3 py-2 border rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 ${errors.platformId ? 'border-red-500' : 'border-gray-300'}`}
                    disabled={isSubmitting}
                  >
                    {availablePlatforms.length === 0 ? (
                      <option value="">No platforms available</option>
                    ) : (
                      availablePlatforms.map((platform) => (
                        <option key={platform.id} value={platform.id}>
                          {platform.name}
                        </option>
                      ))
                    )}
                  </select>
                  {errors.platformId && <p className="text-red-500 text-xs mt-1">{errors.platformId}</p>}
                  <p className="text-gray-500 text-xs mt-1">
                    Choose from platforms where you already have holdings
                  </p>
                </div>
                
                {!instrumentExists && (
                  <>
                    <div>
                      <label htmlFor="currencyCode" className="block text-sm font-medium text-gray-700 mb-1">
                        Currency *
                      </label>
                      <select
                        id="currencyCode"
                        value={formData.currencyCode}
                        onChange={(e) => setFormData({ ...formData, currencyCode: e.target.value })}
                        className={`w-full px-3 py-2 border rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 ${errors.currencyCode ? 'border-red-500' : 'border-gray-300'}`}
                        disabled={isSubmitting}
                      >
                        <option value="GBP">GBP (British Pound)</option>
                        <option value="USD">USD (US Dollar)</option>
                        <option value="EUR">EUR (Euro)</option>
                        <option value="JPY">JPY (Japanese Yen)</option>
                        <option value="CAD">CAD (Canadian Dollar)</option>
                        <option value="AUD">AUD (Australian Dollar)</option>
                      </select>
                      {errors.currencyCode && <p className="text-red-500 text-xs mt-1">{errors.currencyCode}</p>}
                    </div>
                    
                    <div>
                      <label htmlFor="quoteUnit" className="block text-sm font-medium text-gray-700 mb-1">
                        Quote Unit *
                      </label>
                      <select
                        id="quoteUnit"
                        value={formData.quoteUnit}
                        onChange={(e) => setFormData({ ...formData, quoteUnit: e.target.value })}
                        className={`w-full px-3 py-2 border rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 ${errors.quoteUnit ? 'border-red-500' : 'border-gray-300'}`}
                        disabled={isSubmitting}
                      >
                        <option value="GBP">GBP (British Pound)</option>
                        <option value="USD">USD (US Dollar)</option>
                        <option value="EUR">EUR (Euro)</option>
                        <option value="JPY">JPY (Japanese Yen)</option>
                        <option value="CAD">CAD (Canadian Dollar)</option>
                        <option value="AUD">AUD (Australian Dollar)</option>
                      </select>
                      {errors.quoteUnit && <p className="text-red-500 text-xs mt-1">{errors.quoteUnit}</p>}
                    </div>
                    
                    <div>
                      <label htmlFor="description" className="block text-sm font-medium text-gray-700 mb-1">
                        Description
                      </label>
                      <input
                        type="text"
                        id="description"
                        value={formData.description}
                        onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                        className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                        placeholder="Optional description for this instrument"
                        disabled={isSubmitting}
                        maxLength={255}
                      />
                    </div>
                  </>
                )}
                
                <div className="flex space-x-3 pt-4">
                  <button
                    type="button"
                    onClick={goBackToTicker}
                    disabled={isSubmitting}
                    className="px-4 py-2 border border-gray-300 rounded-md text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:cursor-not-allowed disabled:bg-gray-100"
                  >
                    Back
                  </button>
                  <button
                    type="button"
                    onClick={handleClose}
                    disabled={isSubmitting}
                    className="px-4 py-2 border border-gray-300 rounded-md text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:cursor-not-allowed disabled:bg-gray-100"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={isSubmitting}
                    className="flex-1 px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:bg-blue-400 disabled:cursor-not-allowed flex items-center justify-center"
                  >
                    {isSubmitting ? (
                      <>
                        <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white mr-2"></div>
                        Adding...
                      </>
                    ) : (
                      'Add Holding'
                    )}
                  </button>
                </div>
              </>
            )}
          </form>
        </div>
      </div>
    </div>
  );
};

export default HoldingsGrid;