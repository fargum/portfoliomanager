// Chat API Types - These match the DTOs from the.NET API

export interface ChatRequestDto {
  query: string;
  accountId: number;
}

export interface ChatResponseDto {
  response: string;
  queryType: string;
  portfolioSummary?: PortfolioSummaryDto;
  insights?: InsightDto[];
  timestamp?: string;
}

export interface PortfolioSummaryDto {
  accountId: number;
  date: string;
  totalValue: number;
  dayChange: number;
  dayChangePercentage: number;
  holdingsCount: number;
  topHoldings: string[];
}

export interface InsightDto {
  type: string;
  title: string;
  description: string;
  impact: 'High' | 'Medium' | 'Low';
  category: string;
}

export interface AiToolDto {
  name: string;
  description: string;
  parameters: Record<string, any>;
  category: string;
}

// UI-specific chat types
export interface ChatMessage {
  id: string;
  type: 'user' | 'ai' | 'system';
  content: string;
  timestamp: Date;
  queryType?: string;
  portfolioSummary?: PortfolioSummaryDto;
  insights?: InsightDto[];
  isLoading?: boolean;
  error?: string;
}

export interface ChatState {
  messages: ChatMessage[];
  isLoading: boolean;
  error?: string;
}