using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using FinancialAgent.Core.Models;
using FinancialAgent.Core.Interfaces;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace FinancialAgent.Agents;

/// <summary>
/// Main orchestrator for coordinating multiple financial agents using Semantic Kernel
/// Follows Azure best practices with managed identity and proper error handling
/// </summary>
public class FinancialAgentOrchestrator : IAgentOrchestrator
{
    private readonly Kernel _kernel;
    private readonly ILogger<FinancialAgentOrchestrator> _logger;
    private readonly IIntentClassificationService _intentClassifier;
    private readonly IConversationService _conversationService;
    private readonly Dictionary<string, IFinancialAgent> _agents;
    private readonly Dictionary<string, string> _intentToAgentMapping;

    public FinancialAgentOrchestrator(
        Kernel kernel,
        ILogger<FinancialAgentOrchestrator> logger,
        IIntentClassificationService intentClassifier,
        IConversationService conversationService,
        IEnumerable<IFinancialAgent> agents)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _intentClassifier = intentClassifier ?? throw new ArgumentNullException(nameof(intentClassifier));
        _conversationService = conversationService ?? throw new ArgumentNullException(nameof(conversationService));
        
        // Initialize agents dictionary
        _agents = agents.ToDictionary(agent => agent.Name, agent => agent);
        _intentToAgentMapping = InitializeIntentMapping();
        
        _logger.LogInformation("FinancialAgentOrchestrator initialized with {AgentCount} agents", _agents.Count);
    }

    /// <summary>
    /// Process user query through appropriate agents with comprehensive error handling
    /// </summary>
    public async Task<AgentResponse> ProcessQueryAsync(UserQuery query, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Processing query for user {UserId}, session {SessionId}", 
                query.UserId, query.SessionId);

            // 1. Get conversation history for context
            var conversationHistory = await _conversationService.GetConversationHistoryAsync(
                query.UserId, query.SessionId, 10, cancellationToken);

            // 2. Classify intent using AI
            var intent = await ClassifyIntentAsync(query.Message, cancellationToken);
            _logger.LogDebug("Classified intent: {Intent}", intent);

            // 3. Extract entities from the message
            var entities = await ExtractEntitiesAsync(query.Message, cancellationToken);
            
            // 4. Build agent context
            var context = BuildAgentContext(query, intent, entities, conversationHistory);

            // 5. Route to appropriate agent
            var selectedAgent = SelectAgent(intent);
            if (selectedAgent == null)
            {
                _logger.LogWarning("No suitable agent found for intent: {Intent}", intent);
                return CreateFallbackResponse(query.Message, intent);
            }

            _logger.LogDebug("Selected agent: {AgentName} for intent: {Intent}", selectedAgent.Name, intent);

            // 6. Execute agent with retry logic
            var agentResponse = await ExecuteAgentWithRetryAsync(selectedAgent, context, cancellationToken);
            
            // 7. Save conversation turn
            await SaveConversationTurnAsync(query.UserId, query.SessionId, query.Message, agentResponse);

            // 8. Log performance metrics
            var processingTime = DateTime.UtcNow - startTime;
            _logger.LogInformation("Query processed successfully in {ProcessingTimeMs}ms", 
                processingTime.TotalMilliseconds);

            return agentResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query for user {UserId}", query.UserId);
            
            return new AgentResponse
            {
                AgentName = "System",
                Response = "I apologize, but I encountered an error while processing your request. Please try again later.",
                IsSuccessful = false,
                ErrorMessage = ex.Message,
                Disclaimers = GetSystemDisclaimers()
            };
        }
    }

    /// <summary>
    /// Classify user intent using Semantic Kernel with Azure OpenAI
    /// </summary>
    public async Task<string> ClassifyIntentAsync(string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var intentPrompt = """
                You are an expert financial assistant. Classify the user's intent from the following message.
                
                Available intents:
                - GET_STOCK_PRICE: User wants current stock price
                - GET_HISTORICAL_DATA: User wants historical price data
                - TECHNICAL_ANALYSIS: User wants technical analysis (RSI, MACD, etc.)
                - FUNDAMENTAL_ANALYSIS: User wants fundamental analysis
                - INVESTMENT_RECOMMENDATION: User wants investment advice
                - PORTFOLIO_ANALYSIS: User wants portfolio review
                - MARKET_TRENDS: User wants market overview or trends
                - NEWS_AND_SENTIMENT: User wants news or market sentiment
                - RISK_ASSESSMENT: User wants risk analysis
                - GENERAL_HELP: User needs general help or unclear intent
                
                User message: {{$message}}
                
                Respond with only the intent name.
                """;

            var arguments = new KernelArguments { ["message"] = message };
            var result = await _kernel.InvokePromptAsync(intentPrompt, arguments);
            
            var intent = result.GetValue<string>()?.Trim().ToUpperInvariant() ?? "GENERAL_HELP";
            
            _logger.LogDebug("Intent classified as: {Intent} for message: {Message}", intent, message);
            return intent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying intent for message: {Message}", message);
            return "GENERAL_HELP";
        }
    }

    /// <summary>
    /// Extract entities from user message using Semantic Kernel
    /// </summary>
    public async Task<Dictionary<string, object>> ExtractEntitiesAsync(string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var entityPrompt = """
                Extract financial entities from the user message. Return a JSON object with the following structure:
                {
                    "symbols": ["RELIANCE", "TCS"], // Stock symbols mentioned
                    "timeFrame": "1D", // Time frame (1D, 1W, 1M, 1Y, etc.)
                    "amount": 50000, // Investment amount if mentioned
                    "riskTolerance": "MODERATE", // LOW, MODERATE, HIGH
                    "sector": "IT" // Sector if mentioned
                }
                
                User message: {{$message}}
                
                If an entity is not found, omit it from the JSON. Return only valid JSON.
                """;

            var arguments = new KernelArguments { ["message"] = message };
            var result = await _kernel.InvokePromptAsync(entityPrompt, arguments);
            
            var jsonResult = result.GetValue<string>();
            
            // Parse JSON result (add proper JSON parsing with error handling)
            var entities = ParseEntitiesJson(jsonResult);
            
            _logger.LogDebug("Extracted entities: {@Entities}", entities);
            return entities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting entities from message: {Message}", message);
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Get conversation history for user session
    /// </summary>
    public async Task<List<ConversationTurn>> GetConversationHistoryAsync(string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _conversationService.GetConversationHistoryAsync(userId, sessionId, 10, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting conversation history for user {UserId}, session {SessionId}", userId, sessionId);
            return new List<ConversationTurn>();
        }
    }

    /// <summary>
    /// Save conversation turn with proper error handling
    /// </summary>
    public async Task SaveConversationTurnAsync(string userId, string sessionId, ConversationTurn turn, CancellationToken cancellationToken = default)
    {
        try
        {
            await _conversationService.SaveConversationTurnAsync(userId, sessionId, turn, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving conversation turn for user {UserId}, session {SessionId}", userId, sessionId);
            // Don't throw - conversation saving shouldn't fail the main request
        }
    }

    #region Private Methods

    /// <summary>
    /// Initialize intent to agent mapping
    /// </summary>
    private Dictionary<string, string> InitializeIntentMapping()
    {
        return new Dictionary<string, string>
        {
            ["GET_STOCK_PRICE"] = "MarketDataAgent",
            ["GET_HISTORICAL_DATA"] = "MarketDataAgent",
            ["MARKET_TRENDS"] = "MarketDataAgent",
            ["TECHNICAL_ANALYSIS"] = "AnalysisAgent",
            ["FUNDAMENTAL_ANALYSIS"] = "AnalysisAgent",
            ["RISK_ASSESSMENT"] = "AnalysisAgent",
            ["INVESTMENT_RECOMMENDATION"] = "RecommendationAgent",
            ["PORTFOLIO_ANALYSIS"] = "RecommendationAgent",
            ["NEWS_AND_SENTIMENT"] = "NewsAndSentimentAgent",
            ["GENERAL_HELP"] = "GeneralAgent"
        };
    }

    /// <summary>
    /// Select appropriate agent based on intent
    /// </summary>
    private IFinancialAgent? SelectAgent(string intent)
    {
        if (_intentToAgentMapping.TryGetValue(intent, out var agentName) && 
            _agents.TryGetValue(agentName, out var agent))
        {
            return agent;
        }

        // Fallback: find any agent that can handle this intent
        return _agents.Values.FirstOrDefault(agent => agent.CanHandle(intent));
    }

    /// <summary>
    /// Build agent context from user query
    /// </summary>
    private AgentContext BuildAgentContext(UserQuery query, string intent, 
        Dictionary<string, object> entities, List<ConversationTurn> conversationHistory)
    {
        var context = new AgentContext
        {
            UserId = query.UserId,
            SessionId = query.SessionId,
            Intent = intent,
            OriginalMessage = query.Message,
            ConversationHistory = conversationHistory,
            Data = entities
        };

        // Extract specific entities
        if (entities.TryGetValue("symbols", out var symbolsObj) && symbolsObj is List<string> symbols)
        {
            context.Symbols = symbols;
        }

        if (entities.TryGetValue("timeFrame", out var timeFrameObj))
        {
            context.TimeFrame = timeFrameObj.ToString();
        }

        if (entities.TryGetValue("amount", out var amountObj) && decimal.TryParse(amountObj.ToString(), out var amount))
        {
            context.InvestmentAmount = amount;
        }

        if (entities.TryGetValue("riskTolerance", out var riskObj))
        {
            context.RiskTolerance = riskObj.ToString();
        }

        return context;
    }

    /// <summary>
    /// Execute agent with exponential backoff retry logic
    /// </summary>
    private async Task<AgentResponse> ExecuteAgentWithRetryAsync(IFinancialAgent agent, AgentContext context, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        var delay = TimeSpan.FromMilliseconds(100);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await agent.ProcessAsync(context, cancellationToken);
            }
            catch (Exception ex) when (attempt < maxRetries && IsRetryableException(ex))
            {
                _logger.LogWarning(ex, "Agent execution failed (attempt {Attempt}/{MaxRetries}), retrying in {DelayMs}ms", 
                    attempt, maxRetries, delay.TotalMilliseconds);
                
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
            }
        }

        // Final attempt without catch
        return await agent.ProcessAsync(context, cancellationToken);
    }

    /// <summary>
    /// Check if exception is retryable
    /// </summary>
    private static bool IsRetryableException(Exception ex)
    {
        return ex is HttpRequestException || 
               ex is TaskCanceledException || 
               ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Create fallback response for unhandled intents
    /// </summary>
    private AgentResponse CreateFallbackResponse(string message, string intent)
    {
        return new AgentResponse
        {
            AgentName = "System",
            Response = """
                I understand you're asking about financial information, but I need more specific details to help you better. 
                
                I can help you with:
                • Stock prices and market data
                • Technical and fundamental analysis
                • Investment recommendations
                • Portfolio analysis
                • Market news and sentiment
                
                Could you please rephrase your question or be more specific about what you'd like to know?
                """,
            Suggestions = new List<string>
            {
                "What's the current price of Reliance Industries?",
                "Show me technical analysis for TCS",
                "Give me investment recommendations for IT sector",
                "Analyze my portfolio risk"
            },
            Disclaimers = GetSystemDisclaimers(),
            Metadata = new Dictionary<string, object>
            {
                ["original_intent"] = intent,
                ["fallback_reason"] = "no_suitable_agent"
            }
        };
    }

    /// <summary>
    /// Parse entities JSON with error handling
    /// </summary>
    private Dictionary<string, object> ParseEntitiesJson(string? jsonResult)
    {
        if (string.IsNullOrWhiteSpace(jsonResult))
            return new Dictionary<string, object>();

        try
        {
            // This is a simplified JSON parsing - in production, use System.Text.Json
            // For now, return empty dictionary to avoid parsing errors
            return new Dictionary<string, object>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing entities JSON: {Json}", jsonResult);
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Save conversation turn helper
    /// </summary>
    private async Task SaveConversationTurnAsync(string userId, string sessionId, string userMessage, AgentResponse agentResponse)
    {
        try
        {
            // Save user turn
            var userTurn = new ConversationTurn
            {
                Role = "User",
                Message = userMessage,
                Timestamp = DateTime.UtcNow
            };
            await _conversationService.SaveConversationTurnAsync(userId, sessionId, userTurn);

            // Save agent turn
            var agentTurn = new ConversationTurn
            {
                Role = "Agent",
                Message = agentResponse.Response,
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["agent_name"] = agentResponse.AgentName,
                    ["is_successful"] = agentResponse.IsSuccessful
                }
            };
            await _conversationService.SaveConversationTurnAsync(userId, sessionId, agentTurn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving conversation turns");
        }
    }

    /// <summary>
    /// Get standard system disclaimers
    /// </summary>
    private static List<string> GetSystemDisclaimers()
    {
        return new List<string>
        {
            "This is not financial advice and should not be considered as such.",
            "All investments involve risk and may result in loss of capital.",
            "Please consult with a qualified financial advisor before making investment decisions.",
            "Historical performance does not guarantee future results.",
            "Market data may be delayed and subject to change."
        };
    }

    #endregion
}