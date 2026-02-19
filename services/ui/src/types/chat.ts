// Chat API Types - These match the DTOs from the.NET API

export interface ChatRequestDto {
  query: string;
  // NOTE: accountId removed - backend retrieves from authenticated user for security
  threadId?: number; // Optional thread ID for memory context
  modelId?: string;  // Optional model deployment name (e.g. 'grok-4-fast-reasoning')
}

/// <summary>A model available for selection</summary>
export interface AiModelDto {
  id: string;
  displayName: string;
}

export interface ChatResponseDto {
  response: string;
  queryType: string;
  portfolioSummary?: PortfolioSummaryDto;
  insights?: InsightDto[];
  timestamp?: string;
  threadId?: number; // Thread ID for memory context
  threadTitle?: string; // Thread title for display
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
  statusMessage?: string;
  error?: string;
}

export interface ChatState {
  messages: ChatMessage[];
  isLoading: boolean;
  error?: string;
}

// Streaming message types (matching backend DTOs)
export interface StatusUpdateDto {
  Type: string;
  Message: string;
  Progress?: number;
  Details?: string;
}

export interface StreamingMessage {
  MessageType: string;
  Status?: StatusUpdateDto;
  Content?: string;
}