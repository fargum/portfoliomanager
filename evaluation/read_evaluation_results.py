"""
Read and display Portfolio Manager evaluation results
"""

import json
import jsonlines
from datetime import datetime

def read_response_data():
    """Read the collected API responses and display summary"""
    print("=" * 60)
    print("Portfolio Manager API Response Analysis")
    print("=" * 60)
    
    try:
        with jsonlines.open('portfolio_test_dataset_with_responses.jsonl', 'r') as reader:
            responses = list(reader)
        
        print(f"📊 Total test scenarios: {len(responses)}")
        print()
        
        # Group by scenario type
        scenario_types = {}
        response_lengths = []
        
        for response in responses:
            scenario_type = response.get('scenario_type', 'unknown')
            scenario_types[scenario_type] = scenario_types.get(scenario_type, 0) + 1
            
            response_text = response.get('response', '')
            response_lengths.append(len(response_text))
        
        print("Scenario Coverage:")
        for scenario_type, count in scenario_types.items():
            print(f"  • {scenario_type.replace('_', ' ').title()}: {count} scenarios")
        
        print()
        print("Response Quality Metrics:")
        if response_lengths:
            avg_length = sum(response_lengths) / len(response_lengths)
            print(f"  • Average response length: {avg_length:.0f} characters")
            print(f"  • Shortest response: {min(response_lengths)} characters")
            print(f"  • Longest response: {max(response_lengths)} characters")
        
        # Show sample responses
        print()
        print("Sample Response Analysis:")
        print("-" * 40)
        
        for i, response in enumerate(responses[:3], 1):
            query = response.get('query', '')
            response_text = response.get('response', '')
            api_response = response.get('api_response_full', {})
            
            print(f"Sample {i}: {response.get('scenario_type', 'unknown').replace('_', ' ').title()}")
            print(f"  Query: \"{query[:60]}{'...' if len(query) > 60 else ''}\"")
            print(f"  Response length: {len(response_text)} chars")
            print(f"  Query type: {api_response.get('queryType', 'N/A')}")
            print()
            
        return responses
        
    except FileNotFoundError:
        print("❌ Response data file not found")
        return []

def read_evaluation_metrics():
    """Try to read the Azure AI evaluation metrics"""
    print("=" * 60)
    print("Azure AI Evaluation Metrics")
    print("=" * 60)
    
    try:
        # Try different encodings to read the file
        for encoding in ['utf-8', 'utf-8-sig', 'latin-1']:
            try:
                with open('evaluation_results/evaluation_results.json', 'r', encoding=encoding) as f:
                    content = f.read()
                break
            except UnicodeDecodeError:
                continue
        else:
            print("❌ Could not decode evaluation results file with any encoding")
            return
        
        print(f"📄 Evaluation file successfully read ({len(content)} characters)")
        
        # Try to parse as JSON
        try:
            import json
            results = json.loads(content)
            
            # Look for metrics in the results
            if isinstance(results, dict):
                metrics = results.get('metrics', {})
                if metrics:
                    print()
                    print("📊 **Evaluation Metrics Found:**")
                    for metric_name, metric_value in metrics.items():
                        if isinstance(metric_value, (int, float)):
                            print(f"   • {metric_name}: {metric_value:.2f}")
                        else:
                            print(f"   • {metric_name}: {metric_value}")
                
                # Look for run summaries
                run_summaries = results.get('run_summaries', {})
                if run_summaries:
                    print()
                    print("⏱️  **Run Performance:**")
                    for evaluator, summary in run_summaries.items():
                        status = summary.get('status', 'Unknown')
                        duration = summary.get('duration', 'Unknown')
                        completed = summary.get('completed_lines', 0)
                        failed = summary.get('failed_lines', 0)
                        print(f"   • {evaluator}: {status} ({completed}/{completed+failed} completed) in {duration}")
            
        except json.JSONDecodeError as e:
            print(f"⚠️  JSON parse error: {e}")
            # Try to extract any readable metrics from partial JSON
            if 'response_relevance' in content and 'gpt_relevance' in content:
                print("📊 Found response relevance evaluation data")
            if 'response_personality' in content:
                print("📊 Found response personality evaluation data")
            if 'tool_accuracy' in content:
                print("📊 Found tool accuracy evaluation data")
                
    except FileNotFoundError:
        print("❌ No evaluation results JSON file found")
    except Exception as e:
        print(f"⚠️  Could not read evaluation file: {e}")

def display_actual_results(responses):
    """Display only what we can actually measure from the data"""
    print()
    print("=" * 60)
    print("Actual Measured Results")
    print("=" * 60)
    print()
    
    if not responses:
        print("❌ No response data available")
        return
    
    # Count successful vs error responses
    successful = sum(1 for r in responses if 'error' not in r)
    errors = len(responses) - successful
    
    print(f"📊 **Response Collection:**")
    print(f"   • Total scenarios tested: {len(responses)}")
    print(f"   • Successful API responses: {successful}")
    if errors > 0:
        print(f"   • Errors encountered: {errors}")
    print()
    
    # Analyze response quality (only what we can measure)
    if responses:
        lengths = [len(r.get('response', '')) for r in responses]
        avg_length = sum(lengths) / len(lengths)
        
        print(f"📏 **Response Characteristics:**")
        print(f"   • Average response length: {avg_length:.0f} characters")
        print(f"   • Response range: {min(lengths)} - {max(lengths)} characters")
        print()
    
    # Show actual scenario distribution
    scenario_counts = {}
    for r in responses:
        scenario = r.get('scenario_type', 'unknown')
        scenario_counts[scenario] = scenario_counts.get(scenario, 0) + 1
    
    print(f"🔧 **Test Coverage:**")
    for scenario, count in scenario_counts.items():
        print(f"   • {scenario.replace('_', ' ').title()}: {count} scenarios")
    print()

if __name__ == "__main__":
    print(f"Portfolio Manager Evaluation Results")
    print(f"Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print()
    
    # Read API response data
    responses = read_response_data()
    
    # Try to read evaluation metrics
    read_evaluation_metrics()
    
    # Display actual measured results
    display_actual_results(responses)