import { HoldingResponse, ApiResponse, HoldingsListResponse } from '@/types/api';
import { ChatRequestDto, ChatResponseDto, AiToolDto } from '@/types/chat';

export class PortfolioApiClient {
  private readonly baseUrl: string;

  constructor(baseUrl?: string) {
    // Use Next.js public environment variable for client-side code
    const envApiUrl = process.env.NEXT_PUBLIC_API_BASE_URL;
    
    // Fallback logic for dynamic URL generation
    const defaultUrl = typeof window !== 'undefined' 
      ? (window as any).location?.origin?.replace('3000', '8080') || 'http://localhost:8080'
      : 'http://localhost:8080';
    
    this.baseUrl = (baseUrl || envApiUrl || defaultUrl).replace(/\/$/, ''); // Remove trailing slash
  }

  /**
   * Fetch holdings for a specific account and date
   */
  async getHoldings(accountId: number, valuationDate: string): Promise<ApiResponse<HoldingResponse[]>> {
    try {
      const formattedDate = this.formatDate(valuationDate);
      const url = `${this.baseUrl}/api/holdings/account/${accountId}/date/${formattedDate}`;
      
      console.log(`Fetching holdings from: ${url}`);
      
      const response = await fetch(url, {
        method: 'GET',
        headers: {
          'Accept': 'application/json',
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`HTTP ${response.status}: ${errorText}`);
      }

      const accountHoldingsResponse = await response.json();
      
      // Extract the holdings array from the AccountHoldingsResponse
      const data: HoldingResponse[] = accountHoldingsResponse.holdings || [];
      
      return {
        data,
        message: `Successfully retrieved ${data.length} holdings`,
      };
    } catch (error) {
      console.error('Error fetching holdings:', error);
      return {
        error: error instanceof Error ? error.message : 'Unknown error occurred',
      };
    }
  }

  /**
   * Get API health status
   */
  async getHealthStatus(): Promise<ApiResponse<{ status: string }>> {
    try {
      const healthUrl = `${this.baseUrl}/health`;
      console.log(`Attempting to connect to API health endpoint: ${healthUrl}`);
      console.log(`Base URL configured as: ${this.baseUrl}`);
      
      const response = await fetch(healthUrl);
      
      if (!response.ok) {
        throw new Error(`Health check failed: ${response.status}`);
      }

      const status = await response.text();
      console.log(`API health check successful: ${status}`);
      
      return {
        data: { status },
        message: 'API is healthy',
      };
    } catch (error) {
      console.error('Health check failed:', error);
      console.error('Base URL was:', this.baseUrl);
      return {
        error: error instanceof Error ? error.message : 'Health check failed',
      };
    }
  }

  /**
   * Format date for API consumption (YYYY-MM-DD)
   */
  private formatDate(date: string | Date): string {
    const d = new Date(date);
    return d.toISOString().split('T')[0];
  }

  /**
   * Test API connectivity
   */
  async testConnection(): Promise<boolean> {
    const health = await this.getHealthStatus();
    return !health.error;
  }

  /**
   * Send a chat query to the AI assistant with streaming response
   */
  async sendChatQueryStream(
    query: string, 
    accountId: number, 
    onChunk: (chunk: string) => void,
    onComplete: () => void,
    onError: (error: string) => void
  ): Promise<void> {
    try {
      const url = `${this.baseUrl}/api/ai/chat/stream`;
      
      console.log(`Sending streaming chat query to: ${url}`);
      
      const requestBody: ChatRequestDto = {
        query,
        accountId
      };

      const response = await fetch(url, {
        method: 'POST',
        headers: {
          'Accept': 'text/plain',
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestBody),
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`HTTP ${response.status}: ${errorText}`);
      }

      const reader = response.body?.getReader();
      if (!reader) {
        throw new Error('ReadableStream not supported');
      }

      const decoder = new TextDecoder();
      
      try {
        while (true) {
          const { done, value } = await reader.read();
          
          if (done) {
            onComplete();
            break;
          }
          
          const chunk = decoder.decode(value, { stream: true });
          onChunk(chunk);
        }
      } finally {
        reader.releaseLock();
      }
    } catch (error) {
      console.error('Error in streaming chat query:', error);
      onError(error instanceof Error ? error.message : 'Unknown error occurred');
    }
  }

  /**
   * Send a chat query to the AI assistant
   */
  async sendChatQuery(query: string, accountId: number): Promise<ApiResponse<ChatResponseDto>> {
    try {
      const url = `${this.baseUrl}/api/ai/chat/query`;
      
      console.log(`Sending chat query to: ${url}`);
      
      const requestBody: ChatRequestDto = {
        query,
        accountId
      };

      const response = await fetch(url, {
        method: 'POST',
        headers: {
          'Accept': 'application/json',
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(requestBody),
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`HTTP ${response.status}: ${errorText}`);
      }

      const chatResponse: ChatResponseDto = await response.json();
      
      return {
        data: chatResponse,
        message: 'Chat query processed successfully',
      };
    } catch (error) {
      console.error('Error sending chat query:', error);
      return {
        error: error instanceof Error ? error.message : 'Unknown error occurred',
      };
    }
  }

  /**
   * Get available AI tools
   */
  async getAvailableAiTools(): Promise<ApiResponse<AiToolDto[]>> {
    try {
      const url = `${this.baseUrl}/api/ai/chat/tools`;
      
      console.log(`Fetching AI tools from: ${url}`);
      
      const response = await fetch(url, {
        method: 'GET',
        headers: {
          'Accept': 'application/json',
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`HTTP ${response.status}: ${errorText}`);
      }

      const tools: AiToolDto[] = await response.json();
      
      return {
        data: tools,
        message: `Successfully retrieved ${tools.length} AI tools`,
      };
    } catch (error) {
      console.error('Error fetching AI tools:', error);
      return {
        error: error instanceof Error ? error.message : 'Unknown error occurred',
      };
    }
  }

  /**
   * Check AI chat service health
   */
  async getChatHealthStatus(): Promise<ApiResponse<{ status: string; timestamp: string }>> {
    try {
      const url = `${this.baseUrl}/api/ai/chat/health`;
      
      console.log(`Checking AI chat health: ${url}`);
      
      const response = await fetch(url, {
        method: 'GET',
        headers: {
          'Accept': 'application/json',
        },
      });

      if (!response.ok) {
        throw new Error(`AI chat health check failed: ${response.status}`);
      }

      const healthData = await response.json();
      
      return {
        data: healthData,
        message: 'AI chat service is healthy',
      };
    } catch (error) {
      console.error('AI chat health check failed:', error);
      return {
        error: error instanceof Error ? error.message : 'AI chat health check failed',
      };
    }
  }
}

// Create singleton instance
export const apiClient = new PortfolioApiClient();