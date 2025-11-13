# Portfolio Manager Memory Architecture

## Overview

The Portfolio Manager implements a sophisticated memory system using the Microsoft Agent Framework to provide persistent conversation history and context-aware AI interactions. This enables continuous, intelligent conversations that remember previous interactions and provide personalized responses.

## Architecture Components

### 1. Database Schema

The memory system uses three primary entities in PostgreSQL:

#### ConversationThread
- **Purpose**: Groups related messages into conversation sessions
- **Scope**: Account-specific isolation
- **Key Fields**:
  - `account_id`: Links threads to specific user accounts
  - `thread_title`: Human-readable conversation identifier
  - `is_active`: Tracks currently active conversations
  - `last_activity`: Timestamp of most recent interaction

#### ChatMessage
- **Purpose**: Stores individual messages from users and AI
- **Features**:
  - Message content and role (User/Assistant)
  - Token count tracking for context window management
  - JSON metadata for extensibility
  - Automatic timestamping

#### MemorySummary
- **Purpose**: AI-generated conversation summaries for long-term context
- **Features**:
  - Intelligent conversation summarization using dedicated AI agent
  - Key insights and decision extraction
  - Performance optimization for large conversation histories
  - Automatic trigger when conversation exceeds length thresholds

### 2. Microsoft Agent Framework Integration

#### PostgreSqlChatMessageStore
- **Inherits**: `Microsoft.Agents.AI.ChatMessageStore`
- **Responsibility**: Persistent storage of conversation messages
- **Features**:
  - Real-time message persistence
  - Context window management (last 50 messages)
  - Thread-scoped message retrieval
  - Automatic conversation thread creation

#### PortfolioMemoryContextProvider  
- **Inherits**: `Microsoft.Agents.AI.AIContextProvider`
- **Responsibility**: Provides contextual information to AI agents
- **Features**:
  - Conversation insights extraction
  - Memory state serialization
  - Context enhancement before AI invocation
  - Integration with memory summarization agent

### 3. Memory Summarization Agent

#### MemoryExtractionService
- **Purpose**: AI-powered conversation summarization using dedicated agent
- **Features**:
  - Intelligent analysis of conversation threads
  - Extraction of key decisions, preferences, and portfolio insights
  - Structured summarization using dedicated AI agent
  - Integration with Azure OpenAI for high-quality summaries

#### Memory Summarization Flow
- **Trigger**: Automatic when conversation exceeds configured message threshold
- **Processing**: Dedicated AI agent analyzes conversation history
- **Output**: Structured memory summary with key insights
- **Storage**: Persisted to MemorySummary table for future context

### 4. Application Layer

#### ConversationThreadService
- **Purpose**: Business logic for thread management
- **Operations**:
  - Get or create active threads
  - Thread lifecycle management
  - Account-scoped thread operations
  - Memory summarization trigger management

#### AiOrchestrationService
- **Purpose**: Coordinates AI interactions with memory
- **Features**:
  - Memory-aware agent creation
  - Graceful fallback to non-memory processing
  - Factory pattern for runtime component creation
  - Integration with memory summarization workflow

#### MemoryExtractionService
- **Purpose**: AI-powered conversation analysis and summarization
- **Features**:
  - Dedicated AI agent for memory processing
  - Intelligent conversation analysis
  - Structured memory extraction
  - Asynchronous processing with error handling

## Memory Persistence Flow

### 1. Message Storage and Summarization Timeline

```mermaid
sequenceDiagram
    participant User
    participant API
    participant Agent
    participant ChatStore
    participant MemoryService
    participant SummaryAgent
    participant Database

    User->>API: Send chat message
    API->>Agent: Create memory-aware agent
    Agent->>ChatStore: Store user message
    ChatStore->>Database: INSERT user message
    Agent->>AI: Process with context
    AI-->>Agent: Generate response
    Agent->>ChatStore: Store AI response
    ChatStore->>Database: INSERT AI response
    
    Note over ChatStore,MemoryService: Check if summarization needed
    ChatStore->>MemoryService: Trigger summarization check
    MemoryService->>Database: Count messages in thread
    alt Message count exceeds threshold
        MemoryService->>SummaryAgent: Extract conversation memories
        SummaryAgent->>Database: Fetch conversation history
        SummaryAgent->>AI: Generate structured summary
        AI-->>SummaryAgent: Return memory insights
        SummaryAgent->>Database: Store MemorySummary
    end
    
    Agent-->>API: Return response
    API-->>User: Send response
```

### 2. Context Retrieval and Memory Integration

When processing a new message:
1. **Thread Resolution**: Get or create conversation thread for account
2. **Message History**: Load last 50 messages from thread
3. **Memory Summary**: Retrieve existing memory summaries for long-term context
4. **Context Enhancement**: Extract conversation insights and combine with summaries
5. **AI Processing**: Provide enriched context to AI agent
6. **Response Generation**: AI responds with full conversation context
7. **Summarization Check**: Trigger memory summarization if message threshold exceeded

### 3. Automatic Thread Management and Summarization

- **New Conversations**: Automatically create threads with descriptive titles
- **Thread Continuity**: Maintain context across multiple interactions
- **Activity Tracking**: Update `last_activity` timestamp on each message
- **Account Isolation**: Each account has separate conversation spaces
- **Automatic Summarization**: Trigger memory extraction when conversations exceed configured length
- **Memory Integration**: Incorporate previous summaries into new conversation context

## API Integration

### Memory-Enabled Endpoints

#### `/api/ai/chat/query`
- **Method**: POST
- **Features**: Synchronous chat with memory
- **Request**: 
  ```json
  {
    "query": "What's my portfolio performance?",
    "accountId": 1,
    "threadId": 123  // Optional - auto-creates if omitted
  }
  ```
- **Response**:
  ```json
  {
    "response": "Based on our previous discussion...",
    "queryType": "PortfolioAnalysis",
    "threadId": 123,
    "threadTitle": "Portfolio Performance Analysis"
  }
  ```

#### `/api/ai/chat/stream`
- **Method**: POST
- **Features**: Streaming responses with memory
- **Same request/response pattern with real-time streaming**

### Graceful Degradation

If memory components fail:
1. **Error Logging**: Log memory component failures
2. **Fallback Processing**: Continue with standard AI processing
3. **User Experience**: No interruption to user interactions
4. **Monitoring**: Track memory system health

## Configuration

### Dependency Injection Setup

```csharp
// Infrastructure Layer - Factory Registration
services.AddTransient<Func<int, int?, JsonSerializerOptions?, ChatMessageStore>>(
    serviceProvider => (accountId, threadId, jsonOptions) => 
        new PostgreSqlChatMessageStore(/* ... */));

services.AddTransient<Func<int, IChatClient, AIContextProvider>>(
    serviceProvider => (accountId, chatClient) => 
        new PortfolioMemoryContextProvider(/* ... */));

// Memory Summarization Service
services.AddScoped<MemoryExtractionService>();
services.AddScoped<IAiChatService, AzureOpenAiChatService>();
```

### Agent Creation with Memory

```csharp
var agent = chatClient.CreateAIAgent(new ChatClientAgentOptions
{
    Instructions = "You are a portfolio assistant with access to conversation history...",
    ChatOptions = new ChatOptions { Tools = portfolioTools },
    ChatMessageStoreFactory = ctx => chatMessageStoreFactory(accountId, threadId, ctx.JsonSerializerOptions),
    AIContextProviderFactory = ctx => memoryContextProviderFactory(accountId, chatClient)
});
```

### Memory Summarization Configuration

```csharp
// Memory extraction configuration
services.Configure<MemoryExtractionOptions>(options =>
{
    options.MessageThreshold = 50; // Trigger summarization after 50 messages
    options.SummaryPrompt = "Analyze this conversation and extract key portfolio insights...";
    options.MaxTokens = 2000;
});
```

## Performance Considerations

### Context Window Management
- **Message Limit**: Only loads last 50 messages per thread for immediate context
- **Memory Summaries**: Long-term context provided through AI-generated summaries
- **Token Estimation**: Tracks approximate token usage across messages and summaries
- **Automatic Cleanup**: Older messages remain in database but excluded from active context
- **Smart Summarization**: Triggers only when conversation length exceeds thresholds

### Memory Summarization Performance
- **Asynchronous Processing**: Memory extraction runs in background
- **Batched Analysis**: Processes conversation chunks efficiently
- **Caching**: Summary results cached to avoid re-processing
- **Fallback Handling**: Graceful degradation if summarization fails

### Database Optimization
- **Indexes**: Optimized queries on `account_id`, `conversation_thread_id`, and timestamps
- **Cascade Deletes**: Automatic cleanup when accounts or threads are removed
- **Connection Pooling**: Efficient database connection management

### Memory Usage
- **Factory Pattern**: Components created on-demand per request
- **Stateless Services**: No persistent in-memory state
- **Serialization**: Efficient JSON serialization for state persistence

## Monitoring & Debugging

### Logging Events
- **Thread Creation**: New conversation thread establishment
- **Memory Operations**: Message storage and retrieval
- **Summarization Events**: Memory extraction triggers and completions
- **Error Handling**: Memory component failures with fallback
- **Performance**: Context loading, summarization, and processing times

### Health Checks
- **Database Connectivity**: PostgreSQL connection health
- **Memory Components**: Factory registration validation
- **AI Service**: Azure OpenAI service availability for both chat and summarization
- **Summarization Health**: Memory extraction service status

## Migration & Deployment

### Database Migrations
- **Schema Evolution**: EF Core migrations for memory tables
- **Data Preservation**: Existing conversations maintained across updates
- **Version Compatibility**: Backward-compatible schema changes

### Container Deployment
- **Docker Support**: Fully containerized with PostgreSQL
- **Environment Configuration**: Configurable connection strings and AI endpoints
- **Scaling**: Stateless design supports horizontal scaling

## Security & Privacy

### Data Protection
- **Account Isolation**: Complete separation of conversation data by account
- **Encrypted Storage**: Database-level encryption support
- **Retention Policies**: Configurable data retention and cleanup

### Access Control
- **Authentication**: Account-based access control
- **Authorization**: Thread-level permissions
- **Audit Trail**: Complete message history with timestamps

## Future Enhancements

### Planned Features
- **Conversation Search**: Full-text search across message history and summaries
- **Advanced Memory Patterns**: Semantic clustering of related conversation topics
- **Context Prioritization**: Smart context selection based on relevance scoring
- **Multi-Modal Memory**: Support for images and documents in conversations
- **Export/Import**: Conversation backup and migration tools
- **Memory Analytics**: Insights into conversation patterns and user preferences

### Scalability Improvements
- **Distributed Caching**: Redis integration for high-traffic scenarios
- **Read Replicas**: Database read scaling for memory retrieval
- **Async Processing**: Enhanced background memory summarization and optimization
- **Parallel Summarization**: Concurrent processing of multiple conversation threads
- **Memory Compression**: Advanced techniques for long-term memory storage optimization

---

## Technical Implementation Details

This memory system represents a production-ready implementation of persistent AI conversation memory using industry-standard patterns and the Microsoft Agent Framework. The architecture ensures reliability, performance, and scalability while maintaining clean separation of concerns and comprehensive error handling.

### Key Innovations

- **AI-Powered Summarization**: Uses dedicated AI agents for intelligent memory extraction
- **Hybrid Context**: Combines immediate message history with long-term memory summaries  
- **Automatic Optimization**: Self-managing system that triggers summarization based on conversation length
- **Graceful Degradation**: Continues functioning even when summarization components fail
- **Structured Memory**: AI-generated summaries provide structured insights rather than simple text compression