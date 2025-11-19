"""
Performance Analysis Tool

Analyzes where time is being spent in Portfolio Manager requests.
"""

import asyncio
import aiohttp
import time
from datetime import datetime

class PerformanceAnalyzer:
    """Analyzes performance bottlenecks in Portfolio Manager."""
    
    def __init__(self, base_url: str = "http://localhost:8080"):
        self.base_url = base_url.rstrip('/')
        self.session = None
    
    async def __aenter__(self):
        self.session = aiohttp.ClientSession()
        return self
    
    async def __aexit__(self, exc_type, exc_val, exc_tb):
        if self.session:
            await self.session.close()
    
    async def analyze_request_phases(self, query: str):
        """Break down request timing to identify bottlenecks."""
        
        print(f"\n🔍 Analyzing: {query}")
        print("-" * 60)
        
        url = f"{self.base_url}/api/ai/chat/query"
        payload = {"query": query, "accountId": 1}
        
        # Measure different phases
        total_start = time.time()
        
        try:
            async with self.session.post(
                url,
                json=payload,
                headers={"Content-Type": "application/json"}
            ) as response:
                
                # Time to first byte (TTFB)
                ttfb = time.time()
                ttfb_duration = ttfb - total_start
                
                # Read response
                result = await response.json()
                total_end = time.time()
                
                response_read_time = total_end - ttfb
                total_time = total_end - total_start
                
                print(f"📊 Timing Breakdown:")
                print(f"   Time to First Byte: {ttfb_duration:.2f}s")
                print(f"   Response Read Time: {response_read_time:.2f}s")
                print(f"   Total Time: {total_time:.2f}s")
                
                # Analyze response content
                response_text = result.get('response', '')
                print(f"   Response Length: {len(response_text)} characters")
                
                # Performance assessment
                if ttfb_duration > 60:
                    print("🐌 BOTTLENECK: Time to first byte is very high")
                    print("   This suggests Azure OpenAI processing is slow")
                    print("   Possible causes:")
                    print("   - Complex portfolio analysis taking time")
                    print("   - Azure OpenAI model/region performance")
                    print("   - Multiple tool calls being processed")
                elif response_read_time > 10:
                    print("📡 BOTTLENECK: Response reading is slow")
                    print("   This suggests network or streaming issues")
                else:
                    print("✅ Request phases look normal for AI processing")
                
                return {
                    "ttfb": ttfb_duration,
                    "response_read": response_read_time,
                    "total": total_time,
                    "response_length": len(response_text),
                    "success": response.status == 200
                }
                
        except Exception as ex:
            total_time = time.time() - total_start
            print(f"❌ Error after {total_time:.2f}s: {ex}")
            return {"error": str(ex), "total": total_time, "success": False}
    
    async def compare_query_types(self):
        """Compare performance across different query types."""
        
        print("🏃 Performance Analysis by Query Type")
        print("=" * 50)
        
        queries = [
            ("Simple Portfolio", "What's my portfolio value today?"),
            ("Market Sentiment", "What's the sentiment for Apple?"),
            ("Complex Analysis", "Analyze my portfolio and give me market intel on my top holdings"),
        ]
        
        results = {}
        
        for query_type, query in queries:
            result = await self.analyze_request_phases(query)
            results[query_type] = result
            
            # Wait between requests
            await asyncio.sleep(1)
        
        # Summary comparison
        print(f"\n📈 Performance Comparison")
        print("=" * 30)
        
        for query_type, data in results.items():
            if data.get("success"):
                print(f"{query_type:15}: {data['total']:6.1f}s (TTFB: {data['ttfb']:5.1f}s)")
            else:
                print(f"{query_type:15}: FAILED")
        
        # Recommendations
        avg_ttfb = sum(r.get("ttfb", 0) for r in results.values() if r.get("success")) / len([r for r in results.values() if r.get("success")])
        
        print(f"\n💡 Analysis:")
        if avg_ttfb > 60:
            print("   • Azure OpenAI processing is the main bottleneck")
            print("   • Consider:")
            print("     - Using a faster Azure OpenAI model")
            print("     - Optimizing your prompts")
            print("     - Checking Azure region performance")
        elif avg_ttfb > 30:
            print("   • Azure OpenAI processing is moderately slow")
            print("   • This might be normal for complex portfolio analysis")
        else:
            print("   • Azure OpenAI processing time is reasonable")
        
        return results


async def main():
    """Run performance analysis."""
    
    print(f"Portfolio Manager Performance Analysis - {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    
    async with PerformanceAnalyzer() as analyzer:
        await analyzer.compare_query_types()


if __name__ == "__main__":
    asyncio.run(main())