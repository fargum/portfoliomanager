"""
Portfolio Manager Configuration Diagnostic

This script helps diagnose configuration issues without exposing sensitive data.
"""

import asyncio
import aiohttp
import os
import json

async def diagnose_api():
    """Diagnose the Portfolio Manager API configuration."""
    
    print("🔍 Portfolio Manager Configuration Diagnostic")
    print("=" * 50)
    
    api_url = "http://localhost:8080"
    
    # Test basic connectivity
    try:
        async with aiohttp.ClientSession() as session:
            # Check if API is responding
            async with session.get(f"{api_url}/api/ai/chat/health") as response:
                if response.status == 200:
                    print("✅ API is reachable and responding")
                else:
                    print(f"❌ API health check failed: {response.status}")
                    return
            
            # Test a simple configuration query
            test_payload = {
                "query": "Hello, can you respond with a simple test message?",
                "accountId": 1
            }
            
            print("\n🔄 Testing AI response configuration...")
            
            try:
                async with session.post(
                    f"{api_url}/api/ai/chat/query",
                    json=test_payload,
                    timeout=aiohttp.ClientTimeout(total=10)  # Short timeout to detect config issues
                ) as response:
                    
                    if response.status == 200:
                        result = await response.json()
                        print("✅ AI configuration appears to be working")
                        print(f"   Sample response: {result.get('response', 'No response')[:50]}...")
                    else:
                        error_text = await response.text()
                        print(f"❌ AI request failed: {response.status}")
                        print(f"   Error: {error_text}")
                        
                        # Check for common configuration errors
                        if "Azure" in error_text or "OpenAI" in error_text:
                            print("\n💡 Possible Azure OpenAI configuration issues detected.")
                            print("   Please check your environment variables:")
                            print("   - Azure Foundry Endpoint")
                            print("   - Azure Foundry API Key") 
                            print("   - Model Name")
                            
            except asyncio.TimeoutError:
                print("⏰ Request timed out quickly - likely Azure OpenAI connectivity issue")
                print("\n💡 Recommendations:")
                print("   1. Verify your Azure Foundry endpoint is correct")
                print("   2. Check your API key is valid and not expired")
                print("   3. Ensure the model name matches your deployment")
                print("   4. Test your Azure endpoint directly if possible")
                
    except Exception as ex:
        print(f"❌ Connection error: {ex}")

    # Environment variable guidance (without exposing values)
    print("\n🔧 Environment Variable Check")
    print("=" * 30)
    
    # These would be loaded by your application
    required_env_vars = [
        "AZUREFOUNDRY__ENDPOINT", 
        "AZUREFOUNDRY__APIKEY",
        "AZUREFOUNDRY__MODELNAME"
    ]
    
    print("Expected environment variables for Azure Foundry:")
    for var in required_env_vars:
        value = os.getenv(var)
        if value:
            print(f"✅ {var}: Set (length: {len(value)})")
        else:
            print(f"❌ {var}: Not set")
    
    print("\n💡 Note: If environment variables are not set, check your:")
    print("   - .env file in the project root")
    print("   - Docker environment variable configuration")
    print("   - appsettings.json AzureFoundry section")


if __name__ == "__main__":
    asyncio.run(diagnose_api())