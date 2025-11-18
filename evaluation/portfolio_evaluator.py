"""
Portfolio Manager AI Evaluation Suite

This module provides comprehensive evaluation of the Portfolio Manager's agentic AI features,
focusing on tool selection accuracy, response relevance, and personality engagement.
"""

import os
import json
import asyncio
import aiohttp
import jsonlines
from datetime import datetime, timedelta
from typing import Dict, List, Any, Optional
from azure.ai.evaluation import (
    evaluate, 
    ToolCallAccuracyEvaluator, 
    RelevanceEvaluator,
    AzureOpenAIModelConfiguration
)
from azure.identity import DefaultAzureCredential
from promptflow.client import load_flow


class PortfolioManagerApiClient:
    """HTTP client for communicating with the .NET Portfolio Manager API."""
    
    def __init__(self, base_url: str = "http://localhost:5000"):
        """Initialize the API client."""
        self.base_url = base_url.rstrip('/')
        self.session = None
    
    async def __aenter__(self):
        """Async context manager entry."""
        self.session = aiohttp.ClientSession()
        return self
    
    async def __aexit__(self, exc_type, exc_val, exc_tb):
        """Async context manager exit."""
        if self.session:
            await self.session.close()
    
    async def send_chat_query(self, query: str, account_id: int = 1, thread_id: Optional[int] = None) -> Dict[str, Any]:
        """Send a chat query to the Portfolio Manager API."""
        url = f"{self.base_url}/api/ai/chat/query"
        
        payload = {
            "query": query,
            "accountId": account_id,
            "threadId": thread_id
        }
        
        try:
            async with self.session.post(
                url, 
                json=payload,
                headers={"Content-Type": "application/json", "Accept": "application/json"}
            ) as response:
                if response.status != 200:
                    error_text = await response.text()
                    raise Exception(f"API Error {response.status}: {error_text}")
                
                return await response.json()
        except Exception as ex:
            print(f"Error calling Portfolio Manager API: {ex}")
            raise
    
    async def health_check(self) -> bool:
        """Check if the Portfolio Manager API is healthy."""
        try:
            url = f"{self.base_url}/api/ai/chat/health"
            async with self.session.get(url) as response:
                return response.status == 200
        except:
            return False


class ResponsePersonalityEvaluator:
    """Custom evaluator to assess personality and engagement in AI responses."""
    
    def __init__(self, model_config):
        """Initialize the personality evaluator with model configuration."""
        self._flow = load_flow(
            source="response_personality.prompty", 
            model={"configuration": model_config}
        )

    def __call__(self, *, query: str, response: str, **kwargs):
        """Evaluate response personality and engagement."""
        llm_response = self._flow(query=query, response=response)
        try:
            result = json.loads(llm_response)
            return result
        except Exception as ex:
            print(f"Warning: Failed to parse personality evaluation response: {ex}")
            return {
                "personality_score": 3,
                "reasoning": f"Failed to parse evaluation: {str(ex)}"
            }


class PortfolioManagerEvaluator:
    """Main evaluation orchestrator for Portfolio Manager AI features."""
    
    def __init__(self, azure_endpoint: str = None, azure_deployment: str = None):
        """
        Initialize the Portfolio Manager evaluator.
        
        Args:
            azure_endpoint: Azure OpenAI endpoint (optional, can use env var)
            azure_deployment: Azure OpenAI deployment name (optional, can use env var)
        """
        self.azure_endpoint = azure_endpoint or os.getenv("AZURE_OPENAI_ENDPOINT")
        self.azure_deployment = azure_deployment or os.getenv("AZURE_OPENAI_DEPLOYMENT") 
        
        if not self.azure_endpoint or not self.azure_deployment:
            raise ValueError(
                "Azure OpenAI configuration required. Set AZURE_OPENAI_ENDPOINT and "
                "AZURE_OPENAI_DEPLOYMENT environment variables or pass them explicitly."
            )
        
        # Configure Azure OpenAI model for prompt-based evaluators
        self.model_config = AzureOpenAIModelConfiguration(
            azure_deployment=self.azure_deployment,
            azure_endpoint=self.azure_endpoint,
            api_version="2025-04-01-preview"
        )
        
        # Initialize credential for authentication
        self.credential = DefaultAzureCredential()
        
        # Create evaluators
        self._setup_evaluators()
    
    def _setup_evaluators(self):
        """Initialize all evaluators."""
        # Built-in evaluators
        self.tool_accuracy_evaluator = ToolCallAccuracyEvaluator(
            model_config=self.model_config,
            credential=self.credential
        )
        
        self.relevance_evaluator = RelevanceEvaluator(
            model_config=self.model_config,
            credential=self.credential
        )
        
        # Custom evaluator for personality assessment
        self.personality_evaluator = ResponsePersonalityEvaluator(self.model_config)
    
    def create_test_dataset(self, output_file: str = "portfolio_test_dataset.jsonl"):
        """
        Create a comprehensive test dataset for Portfolio Manager evaluation.
        
        Args:
            output_file: Path to save the JSONL dataset
        """
        # Define Portfolio Manager tool definitions for evaluation
        tool_definitions = [
            {
                "name": "PortfolioAnalysisTool",
                "description": "Analyzes portfolio holdings for a specific date, calculating total value, asset allocation, and performance metrics",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "valuationDate": {
                            "type": "string",
                            "description": "Date for analysis (YYYY-MM-DD format or relative terms like 'today', 'yesterday')"
                        }
                    },
                    "required": ["valuationDate"]
                }
            },
            {
                "name": "PortfolioComparisonTool", 
                "description": "Compares portfolio performance between two different dates",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "currentDate": {
                            "type": "string",
                            "description": "Current date for comparison (YYYY-MM-DD or relative terms)"
                        },
                        "comparisonDate": {
                            "type": "string", 
                            "description": "Historical date to compare against (YYYY-MM-DD or relative terms)"
                        }
                    },
                    "required": ["currentDate", "comparisonDate"]
                }
            },
            {
                "name": "MarketIntelligenceTool",
                "description": "Provides market sentiment analysis and intelligence for specific securities",
                "parameters": {
                    "type": "object", 
                    "properties": {
                        "symbol": {
                            "type": "string",
                            "description": "Stock symbol to analyze (e.g., 'AAPL', 'MSFT', 'TSLA.L')"
                        }
                    },
                    "required": ["symbol"]
                }
            }
        ]
        
        # Create diverse test scenarios
        test_scenarios = [
            # Portfolio Analysis Scenarios
            {
                "query": "How is my portfolio performing today?",
                "expected_tool": "PortfolioAnalysisTool",
                "expected_params": {"valuationDate": "today"},
                "scenario_type": "portfolio_analysis"
            },
            {
                "query": "What was my portfolio value yesterday?", 
                "expected_tool": "PortfolioAnalysisTool",
                "expected_params": {"valuationDate": "yesterday"},
                "scenario_type": "portfolio_analysis"
            },
            {
                "query": "Can you analyze my portfolio for November 15, 2025?",
                "expected_tool": "PortfolioAnalysisTool", 
                "expected_params": {"valuationDate": "2025-11-15"},
                "scenario_type": "portfolio_analysis"
            },
            
            # Portfolio Comparison Scenarios
            {
                "query": "How does my portfolio today compare to last week?",
                "expected_tool": "PortfolioComparisonTool",
                "expected_params": {"currentDate": "today", "comparisonDate": "2025-11-11"},
                "scenario_type": "portfolio_comparison"
            },
            {
                "query": "Compare my current portfolio performance to last month",
                "expected_tool": "PortfolioComparisonTool", 
                "expected_params": {"currentDate": "today", "comparisonDate": "2025-10-18"},
                "scenario_type": "portfolio_comparison"
            },
            {
                "query": "Show me the difference between my portfolio on Nov 10 vs Nov 17, 2025",
                "expected_tool": "PortfolioComparisonTool",
                "expected_params": {"currentDate": "2025-11-17", "comparisonDate": "2025-11-10"},
                "scenario_type": "portfolio_comparison"
            },
            
            # Market Intelligence Scenarios  
            {
                "query": "What's the market sentiment for Apple stock?",
                "expected_tool": "MarketIntelligenceTool",
                "expected_params": {"symbol": "AAPL"},
                "scenario_type": "market_intelligence"
            },
            {
                "query": "Can you give me intel on Tesla's market situation?", 
                "expected_tool": "MarketIntelligenceTool",
                "expected_params": {"symbol": "TSLA"},
                "scenario_type": "market_intelligence"
            },
            {
                "query": "How does the market feel about GEN.L right now?",
                "expected_tool": "MarketIntelligenceTool",
                "expected_params": {"symbol": "GEN.L"}, 
                "scenario_type": "market_intelligence"
            },
            
            # Edge Cases and Complex Scenarios
            {
                "query": "What's my total portfolio value and how is MSFT doing in the market?",
                "expected_tool": "PortfolioAnalysisTool",  # First tool that should be called
                "expected_params": {"valuationDate": "today"},
                "scenario_type": "multi_intent",
                "note": "Should trigger both PortfolioAnalysisTool and MarketIntelligenceTool"
            },
            {
                "query": "I want to see how my investments have changed and also check sentiment for Amazon",
                "expected_tool": "PortfolioComparisonTool",  # Primary intent
                "expected_params": {"currentDate": "today", "comparisonDate": "yesterday"}, 
                "scenario_type": "multi_intent",
                "note": "Should trigger PortfolioComparisonTool and MarketIntelligenceTool"
            }
        ]
        
        # Convert to JSONL format
        with jsonlines.open(output_file, mode='w') as writer:
            for i, scenario in enumerate(test_scenarios):
                # Create the data row for evaluation
                row = {
                    "query": scenario["query"],
                    "tool_definitions": tool_definitions,
                    "scenario_id": f"test_{i+1:02d}",
                    "scenario_type": scenario["scenario_type"],
                    "expected_tool": scenario["expected_tool"],
                    "expected_params": scenario["expected_params"]
                }
                
                if "note" in scenario:
                    row["notes"] = scenario["note"]
                
                writer.write(row)
        
        print(f"Created test dataset with {len(test_scenarios)} scenarios in {output_file}")
        return output_file
    
    async def collect_ai_responses(self, test_dataset_file: str, api_base_url: str = "http://localhost:5000", account_id: int = 1):
        """Collect actual AI responses from the Portfolio Manager API."""
        
        print(f"Collecting AI responses from Portfolio Manager API at {api_base_url}...")
        
        # Load test dataset
        test_data = []
        with jsonlines.open(test_dataset_file, mode='r') as reader:
            for row in reader:
                test_data.append(row)
        
        # Collect responses from API
        enriched_data = []
        
        async with PortfolioManagerApiClient(api_base_url) as api_client:
            # Health check first
            if not await api_client.health_check():
                raise Exception(f"Portfolio Manager API at {api_base_url} is not healthy. Please ensure it's running.")
            
            print(f"✅ Portfolio Manager API is healthy")
            print(f"Collecting responses for {len(test_data)} test scenarios...")
            
            for i, scenario in enumerate(test_data, 1):
                print(f"Processing scenario {i}/{len(test_data)}: {scenario['scenario_type']}")
                
                try:
                    # Send query to Portfolio Manager API
                    api_response = await api_client.send_chat_query(
                        query=scenario["query"],
                        account_id=account_id
                    )
                    
                    # Extract tool calls from the response (if available)
                    tool_calls = []
                    # Note: We'll need to examine the actual API response structure to extract tool calls
                    # For now, we'll create a placeholder structure
                    
                    # Enrich the scenario with actual API response
                    enriched_scenario = scenario.copy()
                    enriched_scenario["response"] = api_response.get("response", "No response")
                    enriched_scenario["tool_calls"] = tool_calls  # Will be populated based on actual response structure
                    enriched_scenario["api_response_full"] = api_response  # Store full response for debugging
                    enriched_scenario["timestamp"] = datetime.utcnow().isoformat()
                    
                    enriched_data.append(enriched_scenario)
                    
                    # Small delay to avoid overwhelming the API
                    await asyncio.sleep(0.5)
                    
                except Exception as ex:
                    print(f"❌ Error processing scenario {i}: {ex}")
                    # Add scenario with error response
                    error_scenario = scenario.copy()
                    error_scenario["response"] = f"Error: {str(ex)}"
                    error_scenario["tool_calls"] = []
                    error_scenario["error"] = str(ex)
                    error_scenario["timestamp"] = datetime.utcnow().isoformat()
                    
                    enriched_data.append(error_scenario)
        
        # Save enriched dataset
        enriched_file = test_dataset_file.replace(".jsonl", "_with_responses.jsonl")
        with jsonlines.open(enriched_file, mode='w') as writer:
            for row in enriched_data:
                writer.write(row)
        
        print(f"✅ Collected {len(enriched_data)} responses")
        print(f"📁 Saved enriched dataset to: {enriched_file}")
        
        return enriched_file
    
    def run_evaluation(self, data_file: str, output_path: str = "evaluation_results"):
        """
        Run comprehensive evaluation on the Portfolio Manager AI.
        
        Args:
            data_file: Path to JSONL test dataset
            output_path: Directory to save evaluation results
        """
        print(f"Running Portfolio Manager AI evaluation...")
        print(f"Data: {data_file}")
        print(f"Output: {output_path}")
        
        # Ensure output directory exists
        os.makedirs(output_path, exist_ok=True)
        
        # Run evaluation using Azure AI Evaluation SDK
        result = evaluate(
            data=data_file,
            evaluators={
                "tool_accuracy": self.tool_accuracy_evaluator,
                "response_relevance": self.relevance_evaluator,
                "response_personality": self.personality_evaluator
            },
            evaluator_config={
                "tool_accuracy": {
                    "column_mapping": {
                        "query": "${data.query}",
                        "tool_definitions": "${data.tool_definitions}",
                        # Note: tool_calls and response will come from actual AI execution
                    }
                },
                "response_relevance": {
                    "column_mapping": {
                        "query": "${data.query}",
                        "response": "${data.response}"  # Will come from actual AI execution
                    }
                },
                "response_personality": {
                    "column_mapping": {
                        "query": "${data.query}", 
                        "response": "${data.response}"  # Will come from actual AI execution
                    }
                }
            },
            output_path=output_path
        )
        
        print("Evaluation completed!")
        return result
    
    def analyze_results(self, results):
        """Analyze and summarize evaluation results."""
        # Extract key metrics
        metrics = results.get("metrics", {})
        
        print("\n=== Portfolio Manager AI Evaluation Results ===")
        print(f"Overall Evaluation Completed: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
        print()
        
        # Tool Accuracy Analysis
        if "tool_accuracy.gpt_tool_call_accuracy" in metrics:
            tool_accuracy = metrics["tool_accuracy.gpt_tool_call_accuracy"]
            print(f"🎯 Tool Call Accuracy: {tool_accuracy:.2%}")
            
        # Response Relevance Analysis  
        if "response_relevance.gpt_relevance" in metrics:
            relevance = metrics["response_relevance.gpt_relevance"]
            print(f"🎯 Response Relevance: {relevance:.2f}/5.0")
            
        # Personality Analysis
        if "response_personality.personality_score" in metrics:
            personality = metrics["response_personality.personality_score"] 
            print(f"🎭 Response Personality: {personality:.2f}/5.0")
            
        print()
        print("Detailed results saved to evaluation output directory.")
        return metrics


async def main():
    """Main execution function for Portfolio Manager evaluation."""
    
    try:
        # Initialize evaluator
        evaluator = PortfolioManagerEvaluator()
        
        # Create test dataset
        print("Creating test dataset...")
        dataset_file = evaluator.create_test_dataset()
        
        print("\n🤖 Collecting AI responses from Portfolio Manager API...")
        print("Make sure your Portfolio Manager API is running (e.g., dotnet run or Docker)")
        
        # Get API URL from environment or use default
        api_url = os.getenv("PORTFOLIO_API_URL", "http://localhost:5000")
        account_id = int(os.getenv("TEST_ACCOUNT_ID", "1"))
        
        # Collect actual responses from the API
        enriched_dataset = await evaluator.collect_ai_responses(
            test_dataset_file=dataset_file,
            api_base_url=api_url,
            account_id=account_id
        )
        
        print("\n📊 Running evaluation...")
        
        # Run evaluation on the enriched dataset
        results = evaluator.run_evaluation(
            data_file=enriched_dataset,
            output_path="evaluation_results"
        )
        
        # Analyze and display results
        evaluator.analyze_results(results)
        
        print("\n✅ Evaluation completed successfully!")
        print(f"📁 Results saved to: evaluation_results/")
        print(f"📁 Test data with responses: {enriched_dataset}")
        
    except Exception as ex:
        print(f"❌ Error during evaluation: {ex}")
        print("\nTroubleshooting:")
        print("1. Ensure Portfolio Manager API is running")
        print("2. Check API URL and account ID settings")
        print("3. Verify Azure OpenAI configuration")
        raise


def sync_main():
    """Synchronous wrapper for the async main function."""
    asyncio.run(main())


if __name__ == "__main__":
    sync_main()