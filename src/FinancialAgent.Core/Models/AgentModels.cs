namespace FinancialAgent.Core.Models;

/// <summary>
/// User query context for agent processing
/// </summary>
public class UserQuery
{
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    // Context parameters
    public Dictionary<string, object> Parameters { get; set; } = new();
    public List<string> Symbols { get; set; } = new();
    public string? TimeFrame { get; set; }
    public decimal? InvestmentAmount { get; set; }
    public string? RiskTolerance { get; set; }
    
    // Conversation history
    public List<ConversationTurn> ConversationHistory { get; set; } = new();
}

/// <summary>
/// Agent response model
/// </summary>
public class AgentResponse
{
    public string AgentName { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public object? Data { get; set; }
    public List<string> Suggestions { get; set; } = new();
    public List<string> Disclaimers { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsSuccessful { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Agent execution context
/// </summary>
public class AgentContext
{
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public string OriginalMessage { get; set; } = string.Empty;
    
    // Extracted parameters
    public List<string> Symbols { get; set; } = new();
    public string? Symbol => Symbols.FirstOrDefault();
    public string? TimeFrame { get; set; }
    public decimal? InvestmentAmount { get; set; }
    public string? InvestmentHorizon { get; set; }
    public string? RiskTolerance { get; set; }
    
    // Context data
    public Dictionary<string, object> Data { get; set; } = new();
    public List<ConversationTurn> ConversationHistory { get; set; } = new();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Conversation turn for maintaining context
/// </summary>
public class ConversationTurn
{
    public string Role { get; set; } = string.Empty; // User or Agent
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Intent { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Investment recommendation model
/// </summary>
public class InvestmentRecommendation
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    
    public string Recommendation { get; set; } = string.Empty; // BUY, SELL, HOLD
    public decimal TargetPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal StopLoss { get; set; }
    
    public string Rationale { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; } // 0-100
    public string TimeHorizon { get; set; } = string.Empty; // Short, Medium, Long term
    
    public List<string> RiskFactors { get; set; } = new();
    public List<string> Catalysts { get; set; } = new();
    
    public DateTime RecommendationDate { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewDate { get; set; }
    
    public Dictionary<string, decimal> Metrics { get; set; } = new();
}

/// <summary>
/// Portfolio analysis result
/// </summary>
public class PortfolioAnalysis
{
    public string UserId { get; set; } = string.Empty;
    public string PortfolioId { get; set; } = string.Empty;
    
    public decimal TotalValue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalGainLoss { get; set; }
    public decimal TotalGainLossPercent { get; set; }
    
    public decimal DayGainLoss { get; set; }
    public decimal DayGainLossPercent { get; set; }
    
    public List<HoldingAnalysis> Holdings { get; set; } = new();
    public Dictionary<string, decimal> SectorAllocation { get; set; } = new();
    public Dictionary<string, decimal> AssetAllocation { get; set; } = new();
    
    public RiskMetrics Risk { get; set; } = new();
    public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
}

public class HoldingAnalysis
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal GainLoss { get; set; }
    public decimal GainLossPercent { get; set; }
    public decimal PortfolioWeight { get; set; }
    public string Sector { get; set; } = string.Empty;
}

public class RiskMetrics
{
    public string RiskLevel { get; set; } = string.Empty; // Low, Medium, High
    public decimal Beta { get; set; }
    public decimal Volatility { get; set; }
    public decimal Sharpe { get; set; }
    public decimal MaxDrawdown { get; set; }
    public Dictionary<string, decimal> DiversificationScores { get; set; } = new();
}