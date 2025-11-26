namespace FtoConsulting.PortfolioManager.Application.DTOs.Ai;

/// <summary>
/// Represents the type of status update during AI processing
/// </summary>
public enum StatusUpdateType
{
    /// <summary>
    /// Initial thinking/planning phase
    /// </summary>
    Thinking,
    
    /// <summary>
    /// Planning which tools to use
    /// </summary>
    ToolPlanning,
    
    /// <summary>
    /// Retrieving portfolio data
    /// </summary>
    FetchingPortfolioData,
    
    /// <summary>
    /// Fetching market news and sentiment
    /// </summary>
    FetchingMarketData,
    
    /// <summary>
    /// Analyzing portfolio performance
    /// </summary>
    AnalyzingPerformance,
    
    /// <summary>
    /// Analyzing risk metrics
    /// </summary>
    AnalyzingRisk,
    
    /// <summary>
    /// Comparing with benchmarks
    /// </summary>
    ComparingBenchmarks,
    
    /// <summary>
    /// Generating insights and recommendations
    /// </summary>
    GeneratingInsights,
    
    /// <summary>
    /// Finalizing response
    /// </summary>
    FinalizingResponse,
    
    /// <summary>
    /// Processing memory and learning
    /// </summary>
    ProcessingMemory,
    
    /// <summary>
    /// Completed successfully
    /// </summary>
    Completed,
    
    /// <summary>
    /// Error occurred
    /// </summary>
    Error
}

/// <summary>
/// Represents a status update during AI processing
/// </summary>
/// <param name="Type">The type of status update</param>
/// <param name="Message">Human-readable status message</param>
/// <param name="Progress">Optional progress percentage (0-100)</param>
/// <param name="Details">Optional additional details</param>
public record StatusUpdateDto(
    StatusUpdateType Type,
    string Message,
    int? Progress = null,
    string? Details = null
);

/// <summary>
/// Represents a streaming message that can be either status or content
/// </summary>
public abstract record StreamingMessageDto
{
    /// <summary>
    /// The type of streaming message
    /// </summary>
    public abstract string MessageType { get; }
}

/// <summary>
/// Streaming message containing status update
/// </summary>
/// <param name="Status">The status update information</param>
public record StatusStreamingMessageDto(StatusUpdateDto Status) : StreamingMessageDto
{
    public override string MessageType => "status";
}

/// <summary>
/// Streaming message containing content chunk
/// </summary>
/// <param name="Content">The content chunk</param>
public record ContentStreamingMessageDto(string Content) : StreamingMessageDto
{
    public override string MessageType => "content";
}

/// <summary>
/// Streaming message indicating completion
/// </summary>
public record CompletionStreamingMessageDto() : StreamingMessageDto
{
    public override string MessageType => "completion";
}