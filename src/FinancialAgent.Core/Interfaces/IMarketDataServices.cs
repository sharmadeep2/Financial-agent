using FinancialAgent.Core.Models;

namespace FinancialAgent.Core.Interfaces;

/// <summary>
/// Interface for NSE (National Stock Exchange) API integration
/// </summary>
public interface INseApiService
{
    /// <summary>
    /// Get real-time stock price from NSE
    /// </summary>
    Task<StockData?> GetStockPriceAsync(string symbol, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get historical stock data from NSE
    /// </summary>
    Task<HistoricalStockData?> GetHistoricalDataAsync(string symbol, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get market indices data (NIFTY, SENSEX, etc.)
    /// </summary>
    Task<List<StockData>> GetIndicesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get top gainers and losers
    /// </summary>
    Task<(List<StockData> Gainers, List<StockData> Losers)> GetTopMoversAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search for stocks by company name or symbol
    /// </summary>
    Task<List<string>> SearchSymbolsAsync(string query, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get market status (open/closed)
    /// </summary>
    Task<bool> IsMarketOpenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for BSE (Bombay Stock Exchange) API integration
/// </summary>
public interface IBseApiService
{
    /// <summary>
    /// Get real-time stock price from BSE
    /// </summary>
    Task<StockData?> GetStockPriceAsync(string symbol, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get historical stock data from BSE
    /// </summary>
    Task<HistoricalStockData?> GetHistoricalDataAsync(string symbol, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get BSE SENSEX and other indices
    /// </summary>
    Task<List<StockData>> GetIndicesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get market announcements and corporate actions
    /// </summary>
    Task<List<MarketAnnouncement>> GetAnnouncementsAsync(string? symbol = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search for BSE listed companies
    /// </summary>
    Task<List<string>> SearchSymbolsAsync(string query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for market data repository operations
/// </summary>
public interface IMarketDataRepository
{
    /// <summary>
    /// Save real-time market data
    /// </summary>
    Task SaveMarketDataAsync(StockData stockData, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Save historical data in batch
    /// </summary>
    Task SaveHistoricalDataAsync(HistoricalStockData historicalData, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get latest stock data from cache/database
    /// </summary>
    Task<StockData?> GetLatestStockDataAsync(string symbol, string exchange, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get historical data from database
    /// </summary>
    Task<HistoricalStockData?> GetHistoricalDataAsync(string symbol, string exchange, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get technical indicators for a symbol
    /// </summary>
    Task<TechnicalIndicators?> GetTechnicalIndicatorsAsync(string symbol, string exchange, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Save calculated technical indicators
    /// </summary>
    Task SaveTechnicalIndicatorsAsync(TechnicalIndicators indicators, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get market data for multiple symbols
    /// </summary>
    Task<List<StockData>> GetBulkMarketDataAsync(List<string> symbols, string exchange, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for caching service
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Get cached item
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
    
    /// <summary>
    /// Set cached item with expiration
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default) where T : class;
    
    /// <summary>
    /// Remove cached item
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if key exists in cache
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Set cached item with sliding expiration
    /// </summary>
    Task SetSlidingAsync<T>(string key, T value, TimeSpan slidingExpiration, CancellationToken cancellationToken = default) where T : class;
}

/// <summary>
/// Interface for technical analysis service
/// </summary>
public interface ITechnicalAnalysisService
{
    /// <summary>
    /// Calculate technical indicators for a symbol
    /// </summary>
    Task<TechnicalIndicators> CalculateIndicatorsAsync(string symbol, string exchange, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Calculate indicators from historical data
    /// </summary>
    Task<TechnicalIndicators> CalculateIndicatorsAsync(HistoricalStockData historicalData, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get trading signals based on technical analysis
    /// </summary>
    Task<List<TradingSignal>> GetTradingSignalsAsync(string symbol, string exchange, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Identify chart patterns
    /// </summary>
    Task<List<ChartPattern>> IdentifyPatternsAsync(string symbol, string exchange, CancellationToken cancellationToken = default);
}

/// <summary>
/// Supporting models for market data
/// </summary>
public class MarketAnnouncement
{
    public string Symbol { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime AnnouncementDate { get; set; }
    public string? AttachmentUrl { get; set; }
}

public class TradingSignal
{
    public string Symbol { get; set; } = string.Empty;
    public string Signal { get; set; } = string.Empty; // BUY, SELL, HOLD
    public string Indicator { get; set; } = string.Empty;
    public decimal Strength { get; set; } // 0-100
    public string Description { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class ChartPattern
{
    public string Symbol { get; set; } = string.Empty;
    public string PatternType { get; set; } = string.Empty; // Head and Shoulders, Triangle, etc.
    public string Description { get; set; } = string.Empty;
    public decimal Confidence { get; set; } // 0-100
    public DateTime IdentifiedAt { get; set; } = DateTime.UtcNow;
    public string Implication { get; set; } = string.Empty; // Bullish, Bearish, Neutral
}