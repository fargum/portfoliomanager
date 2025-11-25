// API Response Types - These match the flattened DTO from the .NET API

export interface HoldingResponse {
  holdingId: number;
  valuationDate: string;
  unitAmount: number;
  boughtValue: number;
  currentValue: number;
  gainLoss: number;
  gainLossPercentage: number;
  dailyProfitLoss: number;
  dailyProfitLossPercentage: number;
  
  // Portfolio info
  portfolioId: number;
  portfolioName: string;
  accountId: number;
  accountName: string;
  
  // Instrument info
  instrumentId: number;
  ticker: string;
  instrumentName: string;
  instrumentDescription: string;
  instrumentType: string;
  
  // Platform info
  platformId: number;
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
  accountId: number;
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

// Holdings CRUD operation types
export interface AddHoldingRequest {
  platformId: number;
  ticker: string;
  units: number;
  boughtValue: number;
  instrumentName?: string;
  description?: string;
  instrumentTypeId?: number;
  currencyCode?: string;
  quoteUnit?: string;
}

export interface UpdateHoldingUnitsRequest {
  units: number;
}

export interface HoldingOperationResponse {
  success: boolean;
  message: string;
  errors: string[];
}

export interface UpdateHoldingResponse extends HoldingOperationResponse {
  holdingId: number;
  previousUnits: number;
  newUnits: number;
  previousCurrentValue: number;
  newCurrentValue: number;
  ticker?: string;
}

export interface DeleteHoldingResponse extends HoldingOperationResponse {
  deletedHoldingId: number;
  deletedTicker?: string;
  portfolioId: number;
}

export interface AddHoldingResponse extends HoldingOperationResponse {
  holdingId?: number;
  instrumentCreated: boolean;
  instrument?: {
    id: number;
    ticker: string;
    name: string;
    description?: string;
    currencyCode?: string;
  };
  currentPrice: number;
  currentValue: number;
}