using FinancialAgent.Core.Models;

namespace FinancialAgent.Core.Interfaces;

/// <summary>
/// Base interface for all financial agents
/// </summary>
public interface IFinancialAgent
{
    string Name { get; }
    string Description { get; }
    
    /// <summary>
    /// Process user query and return agent response
    /// </summary>
    Task<AgentResponse> ProcessAsync(AgentContext context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if this agent can handle the given intent
    /// </summary>
    bool CanHandle(string intent);
    
    /// <summary>
    /// Get list of intents this agent can handle
    /// </summary>
    List<string> GetSupportedIntents();
}

/// <summary>
/// Interface for Semantic Kernel orchestrator
/// </summary>
public interface IAgentOrchestrator
{
    /// <summary>
    /// Process user query through appropriate agents
    /// </summary>
    Task<AgentResponse> ProcessQueryAsync(UserQuery query, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Classify user intent
    /// </summary>
    Task<string> ClassifyIntentAsync(string message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extract entities from user message
    /// </summary>
    Task<Dictionary<string, object>> ExtractEntitiesAsync(string message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get conversation history for user
    /// </summary>
    Task<List<ConversationTurn>> GetConversationHistoryAsync(string userId, string sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Save conversation turn
    /// </summary>
    Task SaveConversationTurnAsync(string userId, string sessionId, ConversationTurn turn, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for market data agent
/// </summary>
public interface IMarketDataAgent : IFinancialAgent
{
    /// <summary>
    /// Get real-time stock price
    /// </summary>
    Task<AgentResponse> GetStockPriceAsync(string symbol, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get market trends and analysis
    /// </summary>
    Task<AgentResponse> GetMarketTrendsAsync(string? timeFrame = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get trading volume analysis
    /// </summary>
    Task<AgentResponse> GetVolumeAnalysisAsync(string symbol, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Compare multiple stocks
    /// </summary>
    Task<AgentResponse> CompareStocksAsync(List<string> symbols, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for analysis agent
/// </summary>
public interface IAnalysisAgent : IFinancialAgent
{
    /// <summary>
    /// Perform technical analysis
    /// </summary>
    Task<AgentResponse> PerformTechnicalAnalysisAsync(string symbol, string? timeFrame = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Perform fundamental analysis
    /// </summary>
    Task<AgentResponse> PerformFundamentalAnalysisAsync(string symbol, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Predict stock price trends
    /// </summary>
    Task<AgentResponse> PredictPriceTrendsAsync(string symbol, string timeHorizon, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Assess investment risk
    /// </summary>
    Task<AgentResponse> AssessRiskAsync(string symbol, decimal investmentAmount, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for recommendation agent
/// </summary>
public interface IRecommendationAgent : IFinancialAgent
{
    /// <summary>
    /// Generate investment recommendations
    /// </summary>
    Task<AgentResponse> GenerateRecommendationsAsync(List<string> symbols, string riskTolerance, decimal investmentAmount, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Optimize portfolio allocation
    /// </summary>
    Task<AgentResponse> OptimizePortfolioAsync(List<HoldingAnalysis> currentHoldings, string riskTolerance, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate diversification suggestions
    /// </summary>
    Task<AgentResponse> GenerateDiversificationSuggestionsAsync(List<HoldingAnalysis> currentHoldings, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate stop-loss and target price recommendations
    /// </summary>
    Task<AgentResponse> GenerateRiskManagementAsync(string symbol, decimal currentPrice, string strategy, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for news and sentiment agent
/// </summary>
public interface INewsAndSentimentAgent : IFinancialAgent
{
    /// <summary>
    /// Get latest news for a symbol
    /// </summary>
    Task<AgentResponse> GetLatestNewsAsync(string symbol, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Analyze market sentiment
    /// </summary>
    Task<AgentResponse> AnalyzeSentimentAsync(string symbol, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get impact of news on stock price
    /// </summary>
    Task<AgentResponse> AnalyzeNewsImpactAsync(string symbol, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get sector-wise news and sentiment
    /// </summary>
    Task<AgentResponse> GetSectorSentimentAsync(string sector, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for intent classification service
/// </summary>
public interface IIntentClassificationService
{
    /// <summary>
    /// Classify user intent from message
    /// </summary>
    Task<string> ClassifyIntentAsync(string message, List<ConversationTurn>? conversationHistory = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get confidence score for intent classification
    /// </summary>
    Task<(string Intent, decimal Confidence)> ClassifyIntentWithConfidenceAsync(string message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extract named entities from message
    /// </summary>
    Task<Dictionary<string, object>> ExtractEntitiesAsync(string message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for conversation service
/// </summary>
public interface IConversationService
{
    /// <summary>
    /// Get conversation history for a user session
    /// </summary>
    Task<List<ConversationTurn>> GetConversationHistoryAsync(string userId, string sessionId, int maxTurns = 10, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Save conversation turn
    /// </summary>
    Task SaveConversationTurnAsync(string userId, string sessionId, ConversationTurn turn, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clear conversation history
    /// </summary>
    Task ClearConversationHistoryAsync(string userId, string sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get conversation summary
    /// </summary>
    Task<string> GetConversationSummaryAsync(string userId, string sessionId, CancellationToken cancellationToken = default);
}