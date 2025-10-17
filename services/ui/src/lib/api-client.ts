import { HoldingResponse, ApiResponse, HoldingsListResponse } from '@/types/api';

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
}

// Create singleton instance
export const apiClient = new PortfolioApiClient();