# AI Agent Architecture in Portfolio Manager

## Overview

This document describes the AI agent architecture implemented in the Portfolio Manager application, which enables intelligent portfolio analysis and market intelligence through structured tool integration.

## Architecture Diagram

<div align="center">
  <img src="../assets/agent-architecture.svg" alt="AI Agent Architecture" width="800">
</div>

*Source: Microsoft Agent Framework Documentation - Used with acknowledgment*

> **Note**: If the diagram above doesn't display correctly in GitHub, you can:
> 1. View the raw SVG file: [assets/agent-architecture.svg](../assets/agent-architecture.svg)
> 2. Download and open locally with any SVG viewer
> 3. The diagram shows the agentic loop pattern with User → Agent → LLM → Tools/MCP integration

## How This Architecture Maps to Our Implementation

The diagram above illustrates the exact pattern we've implemented in the Portfolio Manager API for AI-powered portfolio analysis and market intelligence.

### Component Mapping

1. **User** → Portfolio Manager UI/API Clients
   - Users submit queries like "Tell me about Microsoft" or "Analyze my portfolio performance"

2. **Agent** → `AiOrchestrationService`
   - Our main AI orchestration service that coordinates between the LLM and available tools
   - Handles prompt instruction initialization and context management

3. **LLM** → Azure OpenAI (GPT-5-mini)
   - The language model that processes user queries and determines which tools to call
   - Configured through `AzureFoundryOptions` with model selection

4. **Tools/MCP** → Portfolio Analysis & Market Intelligence Tools
   - `PortfolioHoldingsTool` - Retrieves and analyzes portfolio holdings
   - `MarketIntelligenceTool` - Fetches market data and news
   - `EodMarketDataTool` - Integrates with EOD Historical Data MCP server
   - Microsoft Agent Framework MCP integration

### The Agentic Loop in Practice

Our implementation follows the exact agentic loop pattern shown:

1. **Initialize with Prompt Instruction**
   ```csharp
   var agent = chatClient.CreateAIAgent(
       instructions: GetPortfolioAnalysisInstructions(),
       name: "PortfolioAnalyst",
       description: "AI agent for portfolio analysis and market intelligence"
   );
   ```

2. **Send Request + Prompt + Context**
   - User message combined with portfolio context and available tools
   - Tools are registered as AI functions for the agent to call

3. **Process & Decide Next Action**
   - The LLM analyzes the request and determines if tool calls are needed
   - Decides between portfolio analysis, market data fetching, or direct response

4. **Tool/MCP Call Required**
   - Agent calls appropriate tools (portfolio holdings, market intelligence, news search)
   - Each tool implements the MCP (Model Context Protocol) pattern

5. **Execute Tool/MCP Function**
   - Tools execute against real data sources:
     - Database queries for portfolio holdings
     - External API calls to EOD Historical Data
     - Market sentiment analysis

6. **Return Result + Send Tool Result + Updated Context**
   - Tool results are integrated back into the conversation context
   - Loop continues until the task is complete

7. **Task Complete → Return Final Response**
   - Comprehensive analysis combining portfolio data with market intelligence
   - Delivered back to the user through the API

## Key Implementation Files

- **`AiOrchestrationService.cs`** - Main agent orchestration
- **`PortfolioHoldingsTool.cs`** - Portfolio analysis tool
- **`MarketIntelligenceTool.cs`** - Market data integration
- **`EodMarketDataTool.cs`** - EOD MCP server integration
- **`McpServerService.cs`** - MCP protocol implementation

## Benefits of This Architecture

1. **Modularity** - Each tool is independent and can be enhanced separately
2. **Extensibility** - New tools can be added without changing core agent logic
3. **Context Preservation** - The agentic loop maintains conversation context throughout tool calls
4. **Real-time Data** - Tools fetch live market data and portfolio holdings
5. **Intelligent Routing** - The LLM determines the best tools for each query

## MCP Integration

The Model Context Protocol (MCP) integration allows our agent to:
- Call external services (EOD Historical Data) seamlessly
- Maintain structured communication with data providers
- Handle complex multi-step analysis tasks
- Preserve type safety and error handling across tool boundaries

## Example User Journey

1. User: "Tell me about Microsoft's performance in my portfolio"
2. Agent analyzes query and calls `PortfolioHoldingsTool` to find Microsoft holdings
3. Agent calls `MarketIntelligenceTool` to get current Microsoft market data
4. Agent calls `EodMarketDataTool` to fetch recent news and sentiment
5. Agent synthesizes all data into a comprehensive response
6. User receives analysis combining portfolio position, market performance, and recent news

---

*This architecture documentation acknowledges and references the Microsoft Agent Framework documentation as the source for the architectural diagram and conceptual framework.*