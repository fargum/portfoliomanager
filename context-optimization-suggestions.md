# AI Chat Context Size Optimization Suggestions

## Current Implementation Analysis
Your AI chat system currently has these context components:
- Recent conversation history (limited to 6 messages ✅)
- Memory context from `PortfolioMemoryContextProvider` 
- Portfolio tools and data context
- User preferences and conversation summaries

## Optimization Strategies

### 1. **Token-Based Message Limiting** (Instead of Count-Based)
```csharp
// Replace fixed 6-message limit with token-based limit
private const int MAX_CONTEXT_TOKENS = 2000; // ~8KB context

var recentMessages = GetRecentMessagesByTokenLimit(allStoredMessages, MAX_CONTEXT_TOKENS);
```

### 2. **Smart Context Compression**
```csharp
// Compress old assistant messages to summaries
private string CompressAssistantMessage(string content)
{
    if (content.Length > 300)
    {
        return ExtractKeyPoints(content) + " [Details omitted for brevity]";
    }
    return content;
}
```

### 3. **Selective Memory Context Loading**
```csharp
// Only load relevant memory based on query type
public async ValueTask<AIContext> InvokingAsync(InvokingContext context)
{
    var queryType = ClassifyQuery(context.RequestMessage);
    
    return queryType switch
    {
        "performance" => await LoadPerformanceMemory(),
        "holdings" => await LoadHoldingsMemory(),
        "general" => await LoadMinimalMemory(),
        _ => new AIContext { Instructions = "" }
    };
}
```

### 4. **Portfolio Context Optimization**
```csharp
// Only include essential portfolio data based on query
private async Task<string> GetOptimizedPortfolioContext(string query, int accountId)
{
    if (query.Contains("performance") || query.Contains("return"))
        return await GetPerformanceContext(accountId);
    
    if (query.Contains("holding") || query.Contains("stock"))
        return await GetHoldingsContext(accountId);
    
    return await GetBasicSummaryContext(accountId);
}
```

### 5. **Conversation Summary Strategy**
Instead of storing all messages, periodically summarize:
```csharp
// After every 10 exchanges, summarize conversation
public async Task SummarizeOldConversation(int threadId)
{
    var oldMessages = await GetMessagesOlderThan(threadId, TimeSpan.FromHours(2));
    var summary = await _aiService.SummarizeConversation(oldMessages);
    
    // Store summary and delete old detailed messages
    await StoreConversationSummary(threadId, summary);
    await DeleteDetailedMessages(oldMessages);
}
```

### 6. **Context Preprocessing Pipeline**
```csharp
public class ContextOptimizer
{
    public async Task<OptimizedContext> OptimizeForQuery(
        string query, 
        List<ChatMessage> history, 
        MemoryContext memory)
    {
        var relevanceScores = CalculateRelevance(query, history);
        var essentialMessages = SelectEssentialMessages(history, relevanceScores);
        var compressedMemory = CompressMemoryContext(memory, query);
        
        return new OptimizedContext
        {
            Messages = essentialMessages,
            Memory = compressedMemory,
            EstimatedTokens = EstimateTokenCount(essentialMessages, compressedMemory)
        };
    }
}
```

## Implementation Priority

### **High Impact (Immediate)**
1. **Token-based message limiting** instead of fixed count
2. **Query-based memory loading** - only relevant context
3. **Assistant message compression** for old responses

### **Medium Impact (Week 2)**
4. **Conversation summarization** after N exchanges
5. **Context relevance scoring** for message selection
6. **Portfolio context optimization** based on query type

### **Long Term**
7. **Embeddings-based relevant context retrieval**
8. **User-specific context preferences**
9. **Real-time context size monitoring and alerts**

## Configuration Options
```csharp
public class AiChatContextOptions
{
    public int MaxContextTokens { get; set; } = 4000;
    public int MaxRecentMessages { get; set; } = 6;
    public int MaxMemoryItems { get; set; } = 3;
    public bool EnableContextCompression { get; set; } = true;
    public bool EnableQueryBasedMemory { get; set; } = true;
}
```

This would reduce your AI response time significantly by sending only relevant, compressed context instead of full conversation history.