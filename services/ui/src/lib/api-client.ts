import { HoldingResponse, ApiResponse, HoldingsListResponse, AddHoldingRequest } from '@/types/api';
import { ChatRequestDto, ChatResponseDto, AiToolDto } from '@/types/chat';

export class PortfolioApiClient {
  private readonly baseUrl: string;
  private accessToken: string | null = null;

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
   * Set the access token for authenticated requests
   */
  setAccessToken(token: string | null): void {
    this.accessToken = token;
  }

  /**
   * Get common headers for API requests
   */
  private getHeaders(): HeadersInit {
    const headers: HeadersInit = {
      'Accept': 'application/json',
      'Content-Type': 'application/json',
    };

    if (this.accessToken) {
      headers['Authorization'] = `Bearer ${this.accessToken}`;
      console.log('API request includes auth token (length:', this.accessToken.length, ')');
    } else {
      console.warn('API request made WITHOUT auth token - user may need to sign in');
    }

    return headers;
  }

  /**
   * Get common headers for streaming requests
   */
  private getStreamHeaders(): HeadersInit {
    const headers: HeadersInit = {
      'Accept': 'text/plain',
      'Content-Type': 'application/json',
    };

    if (this.accessToken) {
      headers['Authorization'] = `Bearer ${this.accessToken}`;
      console.log('Streaming API request includes auth token (length:', this.accessToken.length, ')');
    } else {
      console.warn('Streaming API request made WITHOUT auth token - user may need to sign in');
    }

    return headers;
  }

  /**
   * Fetch holdings for the authenticated user and date
   */
  async getHoldings(valuationDate: string): Promise<ApiResponse<HoldingResponse[]>> {
    try {
      const formattedDate = this.formatDate(valuationDate);
      const url = `${this.baseUrl}/api/holdings/date/${formattedDate}`;
      
      const response = await fetch(url, {
        method: 'GET',
        headers: this.getHeaders(),
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
   * Format error messages to be user-friendly
   */
  private formatErrorMessage(status: number, errorText: string): string {
    // Handle authentication/authorization errors
    if (status === 401) {
      return 'Please sign in to access the AI Assistant.';
    }
    if (status === 403) {
      return 'You do not have permission to access this feature.';
    }
    if (status === 500 && errorText.includes('AuthorizationPolicy')) {
      return 'Authentication required. Please sign in to continue.';
    }
    
    // Handle other common errors
    if (status >= 500) {
      return 'The service is currently unavailable. Please try again later.';
    }
    if (status >= 400 && status < 500) {
      return 'There was a problem with your request. Please check your input and try again.';
    }
    
    // Fallback to original error for unexpected cases
    return `HTTP ${status}: ${errorText}`;
  }

  /**
   * Test API connectivity
   */
  async testConnection(): Promise<boolean> {
    const health = await this.getHealthStatus();
    return !health.error;
  }

  /**
   * Send a chat query to the AI assistant with streaming response and status updates
   */
  async sendChatQueryStream(
    query: string, 
    accountId: number, 
    onChunk: (chunk: string) => void,
    onComplete: () => void,
    onError: (error: string) => void,
    onStatusUpdate?: (status: import('@/types/chat').StatusUpdateDto) => void,
    threadId?: number
  ): Promise<void> {
    try {
      const url = `${this.baseUrl}/api/ai/chat/stream`;
      
      console.log(`Sending streaming chat query to: ${url}`, { threadId });
      
      const requestBody: ChatRequestDto = {
        query,
        // accountId removed - backend gets from authentication for security
        threadId
      };

      const response = await fetch(url, {
        method: 'POST',
        headers: this.getStreamHeaders(),
        body: JSON.stringify(requestBody),
      });

      if (!response.ok) {
        const errorText = await response.text();
        const friendlyError = this.formatErrorMessage(response.status, errorText);
        throw new Error(friendlyError);
      }

      const reader = response.body?.getReader();
      if (!reader) {
        throw new Error('ReadableStream not supported');
      }

      const decoder = new TextDecoder();
      
      try {
        let buffer = '';
        
        while (true) {
          const { done, value } = await reader.read();
          
          if (done) {
            onComplete();
            break;
          }
          
          const chunk = decoder.decode(value, { stream: true });
          buffer += chunk;
          
          // Process complete lines (JSON messages are line-delimited)
          const lines = buffer.split('\n');
          buffer = lines.pop() || ''; // Keep incomplete line in buffer
          
          for (const line of lines) {
            if (line.trim()) {
              try {
                const message = JSON.parse(line) as import('@/types/chat').StreamingMessage;
                
                if (message.MessageType === 'status' && message.Status && onStatusUpdate) {
                  onStatusUpdate(message.Status);
                } else if (message.MessageType === 'content' && message.Content) {
                  onChunk(message.Content);
                } else if (message.MessageType === 'completion') {
                  onComplete();
                  return;
                }
              } catch (jsonError) {
                // If JSON parsing fails, treat as legacy plain text content
                onChunk(line);
              }
            }
          }
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
  async sendChatQuery(query: string, accountId: number, threadId?: number): Promise<ApiResponse<ChatResponseDto>> {
    try {
      const url = `${this.baseUrl}/api/ai/chat/query`;
      
      console.log(`Sending chat query to: ${url}`, { threadId });
      
      // NOTE: accountId removed from request body - backend retrieves from authentication for security
      const requestBody: ChatRequestDto = {
        query,
        threadId
      };

      const response = await fetch(url, {
        method: 'POST',
        headers: this.getHeaders(),
        body: JSON.stringify(requestBody),
      });

      if (!response.ok) {
        const errorText = await response.text();
        const friendlyError = this.formatErrorMessage(response.status, errorText);
        throw new Error(friendlyError);
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
   * Update holding units
   */
  async updateHoldingUnits(holdingId: number, units: number): Promise<ApiResponse<any>> {
    try {
      const url = `${this.baseUrl}/api/holdings/${holdingId}/units`;
      
      console.log(`Updating holding units: ${url}`, { holdingId, units });
      
      const requestBody = { units };

      const response = await fetch(url, {
        method: 'PUT',
        headers: this.getHeaders(),
        body: JSON.stringify(requestBody),
      });

      if (!response.ok) {
        const errorText = await response.text();
        const friendlyError = this.formatErrorMessage(response.status, errorText);
        throw new Error(friendlyError);
      }

      const updateResponse = await response.json();
      
      return {
        data: updateResponse,
        message: 'Holding units updated successfully',
      };
    } catch (error) {
      console.error('Error updating holding units:', error);
      return {
        error: error instanceof Error ? error.message : 'Failed to update holding units',
      };
    }
  }

  /**
   * Delete a holding
   */
  async deleteHolding(holdingId: number): Promise<ApiResponse<any>> {
    try {
      const url = `${this.baseUrl}/api/holdings/${holdingId}`;
      
      console.log(`Deleting holding: ${url}`, { holdingId });

      const response = await fetch(url, {
        method: 'DELETE',
        headers: this.getHeaders(),
      });

      if (!response.ok) {
        const errorText = await response.text();
        const friendlyError = this.formatErrorMessage(response.status, errorText);
        throw new Error(friendlyError);
      }

      const deleteResponse = await response.json();
      
      return {
        data: deleteResponse,
        message: 'Holding deleted successfully',
      };
    } catch (error) {
      console.error('Error deleting holding:', error);
      return {
        error: error instanceof Error ? error.message : 'Failed to delete holding',
      };
    }
  }

  /**
   * Check if an instrument exists
   */
  async checkInstrument(ticker: string): Promise<ApiResponse<{ exists: boolean; instrument?: any }>> {
    try {
      const url = `${this.baseUrl}/api/instruments/check/${encodeURIComponent(ticker.trim().toUpperCase())}`;
      
      console.log(`Checking instrument: ${url}`, { ticker });

      const response = await fetch(url, {
        method: 'GET',
        headers: this.getHeaders(),
      });

      if (response.ok) {
        const data = await response.json();
        return { data, error: undefined };
      } else {
        const errorData = await response.json().catch(() => ({ detail: 'Unknown error' }));
        return { data: undefined, error: errorData.detail || `Failed to check instrument: ${response.status}` };
      }
    } catch (error) {
      console.error('Error checking instrument:', error);
      return { data: undefined, error: error instanceof Error ? error.message : 'Unknown error occurred' };
    }
  }

  /**
   * Add a new holding to a portfolio
   */
  async addHolding(portfolioId: number, request: AddHoldingRequest): Promise<ApiResponse<any>> {
    try {
      const url = `${this.baseUrl}/api/holdings/portfolio/${portfolioId}`;
      
      console.log(`Adding new holding: ${url}`, { portfolioId, request });

      const response = await fetch(url, {
        method: 'POST',
        headers: this.getHeaders(),
        body: JSON.stringify(request),
      });

      if (!response.ok) {
        const errorText = await response.text();
        const friendlyError = this.formatErrorMessage(response.status, errorText);
        throw new Error(friendlyError);
      }

      const addResponse = await response.json();
      
      return {
        data: addResponse,
        message: 'Holding added successfully',
      };
    } catch (error) {
      console.error('Error adding holding:', error);
      return {
        error: error instanceof Error ? error.message : 'Failed to add holding',
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
        headers: this.getHeaders(),
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
        headers: this.getHeaders(),
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