# Portfolio Manager AI Evaluation Framework

This document describes the comprehensive evaluation framework for testing the Portfolio Manager's AI capabilities using Azure AI Evaluation SDK.

## Overview

The evaluation framework tests your .NET Portfolio Manager API by:

1. **Creating test scenarios** - Portfolio analysis, comparison, and market intelligence queries
2. **Calling your live API** - Makes HTTP requests to your running Portfolio Manager
3. **Evaluating responses** - Uses Azure AI Evaluation SDK to assess:
   - **Tool Call Accuracy**: Does the AI select the right tools with correct parameters?
   - **Response Relevance**: Are responses helpful and directly address user questions?  
   - **Response Personality**: Do responses have engaging personality vs dry technical language?

## Architecture

```
┌─────────────────┐    HTTP API Calls    ┌──────────────────┐
│   Python        │ ────────────────────▶ │  .NET Portfolio  │
│   Evaluation    │                       │  Manager API     │
│   Framework     │ ◀──────────────────── │                  │
└─────────────────┘    JSON Responses     └──────────────────┘
        │
        ▼
┌─────────────────┐
│  Azure AI       │
│  Evaluation SDK │
│  (Metrics &     │
│   Analysis)     │
└─────────────────┘
```

## Quick Start

### 1. Setup Environment

```bash
# Navigate to evaluation directory
cd evaluation

# Run the setup script
python setup.py

# Or manually install requirements
pip install -r requirements.txt
```

### 2. Configure Azure OpenAI

```bash
# Set these environment variables
export AZURE_OPENAI_ENDPOINT="https://your-openai-resource.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT="your-gpt-4-deployment"

# On Windows:
# set AZURE_OPENAI_ENDPOINT=https://your-openai-resource.openai.azure.com/
# set AZURE_OPENAI_DEPLOYMENT=your-gpt-4-deployment
```

### 3. Start Portfolio Manager API

```bash
# From your Portfolio Manager project root
dotnet run

# Or using Docker
docker-compose up

# Verify API is running at http://localhost:8080
```

### 4. Run Evaluation

```bash
# Navigate to evaluation directory
cd evaluation

# Run the complete evaluation suite
python portfolio_evaluator.py
```

## Test Scenarios

The evaluation includes these Portfolio Manager scenarios:

### Portfolio Analysis
- "How is my portfolio performing today?"
- "What was my portfolio value yesterday?"
- "Can you analyze my portfolio for November 15, 2025?"

### Portfolio Comparison  
- "How does my portfolio today compare to last week?"
- "Compare my current portfolio performance to last month"
- "Show me the difference between my portfolio on Nov 10 vs Nov 17, 2025"

### Market Intelligence
- "What's the market sentiment for Apple stock?"
- "Can you give me intel on Tesla's market situation?"
- "How does the market feel about GEN.L right now?"

### Multi-Intent Scenarios
- "What's my total portfolio value and how is MSFT doing in the market?"
- "I want to see how my investments have changed and also check sentiment for Amazon"

## Evaluation Metrics

### 1. Tool Call Accuracy ⚡
- **Purpose**: Verifies AI selects correct tools (PortfolioAnalysisTool, PortfolioComparisonTool, MarketIntelligenceTool)
- **Evaluator**: Built-in `ToolCallAccuracyEvaluator` (Azure AI SDK)
- **Scoring**: Percentage accuracy of tool selection and parameter passing

### 2. Response Relevance 🎯  
- **Purpose**: Assesses how well responses address user's portfolio questions
- **Evaluator**: Built-in `RelevanceEvaluator` (Azure AI SDK)
- **Scoring**: 1-5 scale for relevance to user query

### 3. Response Personality 🎭
- **Purpose**: Ensures responses have engaging personality vs dry technical language
- **Evaluator**: Custom prompt-based evaluator
- **Scoring**: 1-5 scale for personality and engagement level

## Output

After running evaluation, you'll get:

```
📊 Portfolio Manager AI Evaluation Results
Overall Evaluation Completed: 2025-11-18 14:30:15

🎯 Tool Call Accuracy: 85.7%
🎯 Response Relevance: 4.2/5.0  
🎭 Response Personality: 4.0/5.0

📁 Detailed results saved to evaluation_results/
```

Detailed results include:
- Individual scores for each test scenario
- Reasoning for evaluation decisions
- Full API responses for debugging
- Aggregate statistics and trends

## Configuration

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `PORTFOLIO_API_URL` | `http://localhost:8080` | Your Portfolio Manager API endpoint |
| `TEST_ACCOUNT_ID` | `1` | Account ID to use for testing |
| `AZURE_OPENAI_ENDPOINT` | None | Azure OpenAI resource endpoint |
| `AZURE_OPENAI_DEPLOYMENT` | None | GPT model deployment name |

### Customizing Test Scenarios

Edit `portfolio_evaluator.py` and modify the `test_scenarios` list in `create_test_dataset()` to add your own test cases:

```python
{
    "query": "Your custom portfolio query",
    "expected_tool": "PortfolioAnalysisTool",
    "expected_params": {"valuationDate": "today"},
    "scenario_type": "portfolio_analysis"
}
```

## Framework Components

### Core Files

| File | Purpose |
|------|---------|
| `portfolio_evaluator.py` | Main evaluation framework and orchestration |
| `performance_analyzer.py` | Performance bottleneck analysis |
| `read_evaluation_results.py` | Results parsing and display utilities |
| `response_personality.prompty` | Custom personality evaluator prompt |
| `requirements.txt` | Python dependencies |
| `setup.py` | Environment setup script |

### Generated Files

| File | Purpose |
|------|---------|
| `portfolio_test_dataset.jsonl` | Generated test scenarios |
| `portfolio_test_dataset_with_responses.jsonl` | Test scenarios with API responses |
| `evaluation_results/` | Detailed evaluation metrics and analysis |

## Performance Analysis

The framework includes performance analysis capabilities:

```bash
# Analyze request performance across different query types
python performance_analyzer.py
```

This provides:
- Time to First Byte (TTFB) analysis
- Response processing time breakdown
- Bottleneck identification
- Performance recommendations

## Troubleshooting

### API Connection Issues
```
❌ Portfolio Manager API at http://localhost:8080 is not healthy
```
**Solution**: Ensure your .NET API is running and accessible

### Azure OpenAI Configuration  
```
❌ Azure OpenAI configuration required
```
**Solution**: Set `AZURE_OPENAI_ENDPOINT` and `AZURE_OPENAI_DEPLOYMENT` environment variables

### Package Installation Issues
```
❌ Import "azure.ai.evaluation" could not be resolved
```
**Solution**: Install requirements: `pip install -r requirements.txt`

### Authentication Issues
**Solution**: Ensure Azure credentials are configured:
- Azure CLI: `az login`
- Service Principal: Set `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`, `AZURE_TENANT_ID`
- Managed Identity: Automatic in Azure environments

## Advanced Usage

### Running Specific Evaluations

You can run individual evaluators:

```python
# Only tool accuracy
evaluator.run_evaluation(
    data_file=dataset,
    evaluators={"tool_accuracy": evaluator.tool_accuracy_evaluator}
)
```

### Custom Account Testing

```bash
# Test with different account ID
TEST_ACCOUNT_ID=42 python portfolio_evaluator.py
```

### Different API Endpoints

```bash  
# Test against staging environment
PORTFOLIO_API_URL=https://staging-api.yoursite.com python portfolio_evaluator.py
```

## Integration with CI/CD

The evaluation framework can be integrated into your CI/CD pipeline:

```yaml
# Example GitHub Actions workflow
- name: Run AI Evaluation
  run: |
    cd evaluation
    pip install -r requirements.txt
    python portfolio_evaluator.py
  env:
    AZURE_OPENAI_ENDPOINT: ${{ secrets.AZURE_OPENAI_ENDPOINT }}
    AZURE_OPENAI_DEPLOYMENT: ${{ secrets.AZURE_OPENAI_DEPLOYMENT }}
    PORTFOLIO_API_URL: http://localhost:8080
```

## Custom Evaluators

You can extend the framework with custom evaluators:

```python
class CustomEvaluator:
    def __call__(self, *, query: str, response: str, **kwargs):
        # Your custom evaluation logic
        return {
            "custom_score": score,
            "reasoning": "Explanation of scoring"
        }

# Register in portfolio_evaluator.py
evaluators["custom_metric"] = CustomEvaluator()
```

## Best Practices

1. **Regular Evaluation**: Run evaluations after significant code changes
2. **Baseline Establishment**: Establish performance baselines for comparison
3. **Scenario Coverage**: Ensure test scenarios cover all major use cases
4. **Environment Consistency**: Use consistent test data and configurations
5. **Results Analysis**: Regularly review and act on evaluation insights

This comprehensive evaluation framework provides enterprise-grade testing and monitoring of your Portfolio Manager's AI capabilities, ensuring consistent quality and performance as your system evolves.