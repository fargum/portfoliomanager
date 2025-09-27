// API Response Types - These match the flattened DTO from your .NET API

export interface HoldingResponse {
  holdingId: string;
  valuationDate: string;
  unitAmount: number;
  boughtValue: number;
  currentValue: number;
  gainLoss: number;
  gainLossPercentage: number;
  
  // Portfolio info
  portfolioId: string;
  portfolioName: string;
  accountId: string;
  accountName: string;
  
  // Instrument info
  instrumentId: string;
  isin: string;
  sedol: string;
  instrumentName: string;
  instrumentDescription: string;
  instrumentType: string;
  
  // Platform info
  platformId: string;
  platformName: string;
}

export interface ApiResponse<T> {
  data?: T;
  error?: string;
  message?: string;
}

export interface HoldingsListResponse {
  holdings: HoldingResponse[];
  totalCount: number;
  accountId: string;
  valuationDate: string;
}

// UI-specific types
export interface HoldingGridRow extends HoldingResponse {
  // Add any UI-specific fields here if needed
  formattedTotalValue?: string;
  formattedUnitValue?: string;
  formattedQuantity?: string;
}

export interface SearchFilters {
  accountId: string;
  valuationDate: string;
}

// AG Grid column definitions
export interface HoldingColumnDef {
  field: keyof HoldingResponse;
  headerName: string;
  width?: number;
  sortable?: boolean;
  filter?: boolean | string;
  cellRenderer?: string;
  type?: string;
  valueFormatter?: (params: any) => string;
}