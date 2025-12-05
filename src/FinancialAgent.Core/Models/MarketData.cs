using System.ComponentModel.DataAnnotations;

namespace FinancialAgent.Core.Models;

/// <summary>
/// Represents stock market data from NSE or BSE
/// </summary>
public class StockData
{
    [Required]
    public string Symbol { get; set; } = string.Empty;
    
    [Required]
    public string Exchange { get; set; } = string.Empty; // NSE or BSE
    
    public decimal Price { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    
    public DateTime Timestamp { get; set; }
    public DateTime LastUpdated { get; set; }
    
    public string Currency { get; set; } = "INR";
    public string Market { get; set; } = "IN"; // India
    
    // Additional market data
    public decimal? MarketCap { get; set; }
    public decimal? PE { get; set; }
    public decimal? DividendYield { get; set; }
    public decimal? BookValue { get; set; }
}

/// <summary>
/// Historical stock data for analysis
/// </summary>
public class HistoricalStockData
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    
    public List<DailyPrice> DailyPrices { get; set; } = new();
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    
    public int RecordCount => DailyPrices.Count;
}

public class DailyPrice
{
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public decimal AdjustedClose { get; set; }
}

/// <summary>
/// Technical analysis indicators
/// </summary>
public class TechnicalIndicators
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; }
    
    // Moving Averages
    public decimal? SMA20 { get; set; } // 20-day Simple Moving Average
    public decimal? SMA50 { get; set; } // 50-day Simple Moving Average
    public decimal? SMA200 { get; set; } // 200-day Simple Moving Average
    public decimal? EMA12 { get; set; } // 12-day Exponential Moving Average
    public decimal? EMA26 { get; set; } // 26-day Exponential Moving Average
    
    // Momentum Indicators
    public decimal? RSI { get; set; } // Relative Strength Index
    public decimal? MACD { get; set; } // MACD Line
    public decimal? MACDSignal { get; set; } // MACD Signal Line
    public decimal? MACDHistogram { get; set; } // MACD Histogram
    
    // Volatility Indicators
    public decimal? BollingerUpper { get; set; }
    public decimal? BollingerMiddle { get; set; }
    public decimal? BollingerLower { get; set; }
    public decimal? ATR { get; set; } // Average True Range
    
    // Volume Indicators
    public decimal? VolumeMA { get; set; } // Volume Moving Average
    public decimal? OBV { get; set; } // On Balance Volume
    
    // Support and Resistance
    public List<decimal> SupportLevels { get; set; } = new();
    public List<decimal> ResistanceLevels { get; set; } = new();
}

/// <summary>
/// Market sentiment and news data
/// </summary>
public class MarketSentiment
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    
    public decimal SentimentScore { get; set; } // -1 to 1 (negative to positive)
    public string SentimentLabel { get; set; } = string.Empty; // Bearish, Neutral, Bullish
    
    public List<NewsItem> RelatedNews { get; set; } = new();
    public int TotalNewsCount { get; set; }
    public int PositiveNewsCount { get; set; }
    public int NegativeNewsCount { get; set; }
    public int NeutralNewsCount { get; set; }
}

public class NewsItem
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public decimal SentimentScore { get; set; }
    public List<string> Keywords { get; set; } = new();
}

/// <summary>
/// Stock search result from API queries
/// </summary>
public class StockSearchResult
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string? ScripCode { get; set; }
    public string? Sector { get; set; }
    public string? Industry { get; set; }
    public decimal? MarketCap { get; set; }
    public decimal? CurrentPrice { get; set; }
    public bool IsActive { get; set; } = true;
}