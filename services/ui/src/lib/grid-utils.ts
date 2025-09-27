import { ColDef } from 'ag-grid-community';
import { HoldingResponse } from '@/types/api';

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
  return new Date(dateString).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
};

// AG Grid column definitions
export const getHoldingsColumnDefs = (): ColDef<HoldingResponse>[] => [
  {
    field: 'portfolioName',
    headerName: 'Portfolio',
    width: 150,
    sortable: true,
    filter: true,
    pinned: 'left',
  },
  {
    field: 'instrumentName',
    headerName: 'Instrument',
    width: 200,
    sortable: true,
    filter: true,
    tooltipField: 'instrumentName',
  },
  {
    field: 'isin',
    headerName: 'ISIN',
    width: 120,
    sortable: true,
    filter: true,
    cellStyle: { fontFamily: 'monospace', fontWeight: 'bold' },
    hide: true,
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
    valueFormatter: (params: any) => formatNumber(params.value),
    cellStyle: { textAlign: 'right', fontFamily: 'monospace' },
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
    field: 'platformName',
    headerName: 'Platform',
    width: 130,
    sortable: true,
    filter: true,
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