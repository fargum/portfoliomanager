import { ColDef } from 'ag-grid-community';
import { HoldingResponse } from '@/types/api';
import React from 'react';

// Platform icon mapping for better visual representation
const getPlatformIcon = (platformName: string): string => {
  const name = platformName.toLowerCase();
  
  // Map platform names to appropriate emoji/icons
  if (name.includes('hl') || name.includes('hargreaves')) return '🏦'; // Bank building for HL
  if (name.includes('interactive') || name.includes('ib')) return '🌐'; // Globe for Interactive Brokers
  if (name.includes('vanguard')) return '⭐'; // Star for Vanguard
  if (name.includes('fidelity')) return '💎'; // Diamond for Fidelity
  if (name.includes('schwab')) return '🔷'; // Blue diamond for Schwab
  if (name.includes('etoro')) return '📈'; // Chart for eToro
  if (name.includes('degiro')) return '🎯'; // Target for DeGiro
  if (name.includes('trading212') || name.includes('212')) return '📱'; // Phone for Trading 212
  if (name.includes('freetrade')) return '🚀'; // Rocket for Freetrade
  if (name.includes('cash')) return '💰'; // Money bag for cash
  
  // Default icons based on type
  return '💼'; // Briefcase as default
};

// Custom platform cell renderer
const PlatformCellRenderer = (params: any) => {
  const platformName = params.value || '';
  const icon = getPlatformIcon(platformName);
  
  return React.createElement('div', {
    className: 'flex items-center space-x-2 h-full',
    style: { padding: '4px 8px', display: 'flex', alignItems: 'center' }
  }, [
    React.createElement('span', {
      key: 'icon',
      className: 'text-lg',
      style: { 
        fontSize: '18px',
        filter: 'drop-shadow(0 1px 2px rgba(0,0,0,0.1))',
        minWidth: '20px'
      }
    }, icon),
    React.createElement('span', {
      key: 'name',
      className: 'text-sm font-medium text-financial-slate-700 truncate',
      style: { maxWidth: '80px' }
    }, platformName)
  ]);
};

// Utility functions for formatting
export const formatCurrency = (value: number): string => {
  return new Intl.NumberFormat('en-GB', {
    style: 'currency',
    currency: 'GBP',
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value);
};

export const formatNumber = (value: number): string => {
  return new Intl.NumberFormat('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 4,
  }).format(value);
};

export const formatDate = (dateString: string): string => {
  return new Date(dateString).toLocaleDateString('en-GB', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
};

// AG Grid column definitions
export const getHoldingsColumnDefs = (onCellValueChanged?: (params: any) => void): ColDef<HoldingResponse>[] => [
  // Checkbox column for row selection
  {
    headerName: '',
    checkboxSelection: true,
    headerCheckboxSelection: true,
    width: 50,
    minWidth: 50,
    maxWidth: 50,
    resizable: false,
    sortable: false,
    filter: false,
    pinned: 'left',
  },
  {
    field: 'platformName',
    headerName: 'Platform',
    width: 140,
    sortable: true,
    filter: true,
    pinned: 'left',
    cellRenderer: PlatformCellRenderer,
    cellStyle: {
      display: 'flex',
      alignItems: 'center',
      padding: '0',
      backgroundColor: '#fafafa',
      borderRight: '2px solid #f1f5f9'
    },
  },
  {
    field: 'instrumentName',
    headerName: 'Instrument',
    width: 500,
    flex: 0, // Override default flex to use fixed width
    sortable: true,
    filter: true,
    tooltipField: 'instrumentName',
  },
  {
    field: 'ticker',
    headerName: 'Ticker',
    width: 100,
    sortable: true,
    filter: true,
    cellStyle: { fontFamily: 'monospace', fontWeight: 'bold' },
  },
  {
    field: 'instrumentType',
    headerName: 'Type',
    width: 120,
    sortable: true,
    filter: true,
    hide: true,
  },
  {
    field: 'unitAmount',
    headerName: 'Units',
    width: 120,
    sortable: true,
    filter: 'agNumberColumnFilter',
    type: 'numericColumn',
    editable: true,
    cellEditor: 'agNumberCellEditor',
    cellEditorParams: {
      min: 0.0001,
      max: 999999999,
      precision: 4,
      step: 0.0001,
    },
    valueFormatter: (params: any) => formatNumber(params.value),
    cellStyle: (params: any) => ({
      textAlign: 'right',
      fontFamily: 'monospace',
      backgroundColor: params.node?.isRowPinned() ? 'transparent' : '#f8fafc',
      border: '1px solid #e2e8f0',
      cursor: 'pointer'
    }),
    onCellValueChanged: onCellValueChanged,
    cellClass: 'editable-cell',
  },
  {
    field: 'boughtValue',
    headerName: 'Bought Value',
    width: 130,
    sortable: true,
    filter: 'agNumberColumnFilter',
    type: 'numericColumn',
    valueFormatter: (params: any) => formatCurrency(params.value),
    cellStyle: { textAlign: 'right', fontFamily: 'monospace' },
  },
  {
    field: 'currentValue',
    headerName: 'Current Value',
    width: 140,
    sortable: true,
    filter: 'agNumberColumnFilter',
    type: 'numericColumn',
    valueFormatter: (params: any) => formatCurrency(params.value),
    cellStyle: (params: any) => ({
      textAlign: 'right',
      fontFamily: 'monospace',
      fontWeight: 'bold',
      color: params.value > 0 ? '#10b981' : '#ef4444',
    }),
  },
  {
    field: 'gainLoss',
    headerName: 'Gain/Loss',
    width: 130,
    sortable: true,
    filter: 'agNumberColumnFilter',
    type: 'numericColumn',
    valueFormatter: (params: any) => formatCurrency(params.value),
    cellStyle: (params: any) => ({
      textAlign: 'right',
      fontFamily: 'monospace',
      fontWeight: 'bold',
      color: params.value >= 0 ? '#10b981' : '#ef4444',
    }),
  },
  {
    field: 'gainLossPercentage',
    headerName: 'Gain/Loss %',
    width: 120,
    sortable: true,
    filter: 'agNumberColumnFilter',
    type: 'numericColumn',
    valueFormatter: (params: any) => `${formatNumber(params.value)}%`,
    cellStyle: (params: any) => ({
      textAlign: 'right',
      fontFamily: 'monospace',
      fontWeight: 'bold',
      color: params.value >= 0 ? '#10b981' : '#ef4444',
    }),
  },
  {
    field: 'dailyProfitLoss',
    headerName: 'Daily P/L',
    width: 120,
    sortable: true,
    filter: 'agNumberColumnFilter',
    type: 'numericColumn',
    valueFormatter: (params: any) => formatCurrency(params.value),
    cellStyle: (params: any) => ({
      textAlign: 'right',
      fontFamily: 'monospace',
      fontWeight: 'bold',
      color: params.value >= 0 ? '#10b981' : '#ef4444',
      backgroundColor: params.value >= 0 ? '#f0f9ff' : '#fef2f2',
    }),
  },
  {
    field: 'dailyProfitLossPercentage',
    headerName: 'Daily P/L %',
    width: 120,
    sortable: true,
    filter: 'agNumberColumnFilter',
    type: 'numericColumn',
    valueFormatter: (params: any) => `${formatNumber(params.value)}%`,
    cellStyle: (params: any) => ({
      textAlign: 'right',
      fontFamily: 'monospace',
      fontWeight: 'bold',
      color: params.value >= 0 ? '#10b981' : '#ef4444',
      backgroundColor: params.value >= 0 ? '#f0f9ff' : '#fef2f2',
    }),
  },
  {
    field: 'valuationDate',
    headerName: 'Valuation Date',
    width: 140,
    sortable: true,
    filter: 'agDateColumnFilter',
    valueFormatter: (params: any) => formatDate(params.value),
  },
];

// AG Grid default options
export const getGridOptions = () => ({
  defaultColDef: {
    sortable: true,
    filter: true,
    resizable: true,
    minWidth: 100,
  },
  enableRangeSelection: true,
  suppressRowClickSelection: true,
  rowSelection: 'multiple' as const,
  pagination: true,
  paginationPageSize: 50,
});

// Utility for calculating totals
export const calculateTotalValue = (holdings: HoldingResponse[]): number => {
  return holdings.reduce((sum, holding) => sum + holding.currentValue, 0);
};

export const calculateTotalBoughtValue = (holdings: HoldingResponse[]): number => {
  return holdings.reduce((sum, holding) => sum + holding.boughtValue, 0);
};

export const calculateTotalGainLoss = (holdings: HoldingResponse[]): number => {
  return holdings.reduce((sum, holding) => sum + holding.gainLoss, 0);
};

export const calculateTotalGainLossPercentage = (holdings: HoldingResponse[]): number => {
  if (holdings.length === 0) return 0;
  const totalBoughtValue = calculateTotalBoughtValue(holdings);
  const totalGainLoss = calculateTotalGainLoss(holdings);
  return totalBoughtValue !== 0 ? (totalGainLoss / totalBoughtValue) * 100 : 0;
};

export const calculateTotalDailyPnL = (holdings: HoldingResponse[]): number => {
  return holdings.reduce((sum, holding) => sum + holding.dailyProfitLoss, 0);
};

export const calculateAverageDailyPnLPercentage = (holdings: HoldingResponse[]): number => {
  if (holdings.length === 0) return 0;
  const totalDailyPnL = calculateTotalDailyPnL(holdings);
  const totalCurrentValue = calculateTotalValue(holdings);
  return totalCurrentValue !== 0 ? (totalDailyPnL / totalCurrentValue) * 100 : 0;
};

export const groupHoldingsByPortfolio = (holdings: HoldingResponse[]) => {
  return holdings.reduce((groups, holding) => {
    const portfolio = holding.portfolioName;
    if (!groups[portfolio]) {
      groups[portfolio] = [];
    }
    groups[portfolio].push(holding);
    return groups;
  }, {} as Record<string, HoldingResponse[]>);
};