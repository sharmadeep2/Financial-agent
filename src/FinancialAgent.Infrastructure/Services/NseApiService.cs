using System.Text.Json;
using FinancialAgent.Core.Models;
using FinancialAgent.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Extensions.Http;

namespace FinancialAgent.Infrastructure.Services;

/// <summary>
/// NSE (National Stock Exchange) API service implementation
/// Following Azure best practices with retry policies, proper error handling, and logging
/// Reference: https://www.nseindia.com/
/// </summary>
public class NseApiService : INseApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NseApiService> _logger;
    private readonly ICacheService _cacheService;
    private readonly IConfiguration _configuration;
    private readonly string _baseUrl;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public NseApiService(
        HttpClient httpClient,
        ILogger<NseApiService> logger,
        ICacheService cacheService,
        IConfiguration configuration)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        
        _baseUrl = _configuration["NSE:BaseUrl"] ?? "https://www.nseindia.com";
        
        ConfigureHttpClient();
        
        _logger.LogInformation("NseApiService initialized with base URL: {BaseUrl}", _baseUrl);
    }

    /// <summary>
    /// Get real-time stock price from NSE with caching and retry logic
    /// </summary>
    public async Task<StockData?> GetStockPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));
        }

        var normalizedSymbol = symbol.ToUpperInvariant();
        var cacheKey = $"nse_stock_{normalizedSymbol}";
        
        try
        {
            // Check cache first (30-second TTL for real-time data)
            var cachedData = await _cacheService.GetAsync<StockData>(cacheKey, cancellationToken);
            if (cachedData != null)
            {
                _logger.LogDebug("Retrieved stock data for {Symbol} from cache", normalizedSymbol);
                return cachedData;
            }

            _logger.LogInformation("Fetching stock data for {Symbol} from NSE API", normalizedSymbol);

            // NSE API endpoint for individual stock quote
            var endpoint = $"/api/quote-equity?symbol={normalizedSymbol}";
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("NSE API returned {StatusCode} for symbol {Symbol}", 
                    response.StatusCode, normalizedSymbol);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var nseResponse = JsonSerializer.Deserialize<NseQuoteResponse>(jsonContent, JsonOptions);

            if (nseResponse?.Data == null)
            {
                _logger.LogWarning("No data received from NSE API for symbol {Symbol}", normalizedSymbol);
                return null;
            }

            var stockData = MapNseResponseToStockData(nseResponse.Data, normalizedSymbol);
            
            // Cache the result with 30-second expiration for real-time data
            await _cacheService.SetAsync(cacheKey, stockData, TimeSpan.FromSeconds(30), cancellationToken);
            
            _logger.LogInformation("Successfully fetched and cached stock data for {Symbol}: ₹{Price}", 
                normalizedSymbol, stockData.Price);

            return stockData;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while fetching stock data for {Symbol}", normalizedSymbol);
            throw new InvalidOperationException($"Failed to fetch stock data for {normalizedSymbol}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timed out while fetching stock data for {Symbol}", normalizedSymbol);
            throw new InvalidOperationException($"Request timed out for {normalizedSymbol}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse NSE API response for {Symbol}", normalizedSymbol);
            throw new InvalidOperationException($"Invalid response format for {normalizedSymbol}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while fetching stock data for {Symbol}", normalizedSymbol);
            throw;
        }
    }

    /// <summary>
    /// Get historical stock data from NSE with proper error handling
    /// </summary>
    public async Task<HistoricalStockData?> GetHistoricalDataAsync(string symbol, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));
        }

        if (fromDate > toDate)
        {
            throw new ArgumentException("From date cannot be after to date");
        }

        var normalizedSymbol = symbol.ToUpperInvariant();
        var cacheKey = $"nse_historical_{normalizedSymbol}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}";

        try
        {
            // Check cache first (1-hour TTL for historical data)
            var cachedData = await _cacheService.GetAsync<HistoricalStockData>(cacheKey, cancellationToken);
            if (cachedData != null)
            {
                _logger.LogDebug("Retrieved historical data for {Symbol} from cache", normalizedSymbol);
                return cachedData;
            }

            _logger.LogInformation("Fetching historical data for {Symbol} from {FromDate} to {ToDate}", 
                normalizedSymbol, fromDate.ToString("yyyy-MM-dd"), toDate.ToString("yyyy-MM-dd"));

            // NSE historical data endpoint
            var endpoint = $"/api/historical/cm/equity?symbol={normalizedSymbol}" +
                          $"&series=[%22EQ%22]" +
                          $"&from={fromDate:dd-MM-yyyy}" +
                          $"&to={toDate:dd-MM-yyyy}";

            var response = await _httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("NSE API returned {StatusCode} for historical data request: {Symbol}", 
                    response.StatusCode, normalizedSymbol);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var nseResponse = JsonSerializer.Deserialize<NseHistoricalResponse>(jsonContent, JsonOptions);

            if (nseResponse?.Data == null || !nseResponse.Data.Any())
            {
                _logger.LogWarning("No historical data received from NSE API for symbol {Symbol}", normalizedSymbol);
                return null;
            }

            var historicalData = MapNseHistoricalResponse(nseResponse.Data, normalizedSymbol, fromDate, toDate);
            
            // Cache the result with 1-hour expiration for historical data
            await _cacheService.SetAsync(cacheKey, historicalData, TimeSpan.FromHours(1), cancellationToken);
            
            _logger.LogInformation("Successfully fetched {RecordCount} historical records for {Symbol}", 
                historicalData.RecordCount, normalizedSymbol);

            return historicalData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching historical data for {Symbol}", normalizedSymbol);
            throw new InvalidOperationException($"Failed to fetch historical data for {normalizedSymbol}", ex);
        }
    }

    /// <summary>
    /// Get market indices data (NIFTY, etc.)
    /// </summary>
    public async Task<List<StockData>> GetIndicesAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "nse_indices";

        try
        {
            // Check cache first (1-minute TTL for indices)
            var cachedData = await _cacheService.GetAsync<List<StockData>>(cacheKey, cancellationToken);
            if (cachedData != null)
            {
                _logger.LogDebug("Retrieved indices data from cache");
                return cachedData;
            }

            _logger.LogInformation("Fetching indices data from NSE API");

            var endpoint = "/api/allIndices";
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("NSE API returned {StatusCode} for indices request", response.StatusCode);
                return new List<StockData>();
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var nseResponse = JsonSerializer.Deserialize<NseIndicesResponse>(jsonContent, JsonOptions);

            if (nseResponse?.Data == null)
            {
                _logger.LogWarning("No indices data received from NSE API");
                return new List<StockData>();
            }

            var indices = nseResponse.Data
                .Where(index => IsImportantIndex(index.Index))
                .Select(index => MapIndexToStockData(index))
                .ToList();

            // Cache the result with 1-minute expiration
            await _cacheService.SetAsync(cacheKey, indices, TimeSpan.FromMinutes(1), cancellationToken);

            _logger.LogInformation("Successfully fetched {IndicesCount} indices from NSE", indices.Count);
            return indices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching indices data");
            throw new InvalidOperationException("Failed to fetch indices data", ex);
        }
    }

    /// <summary>
    /// Get top gainers and losers
    /// </summary>
    public async Task<(List<StockData> Gainers, List<StockData> Losers)> GetTopMoversAsync(CancellationToken cancellationToken = default)
    {


        try
        {
            // Check cache first - Skip caching for complex tuples to avoid type constraint issues
            // In a production system, you'd create a wrapper class for the tuple

            _logger.LogInformation("Fetching top movers data from NSE API");

            // Fetch gainers and losers in parallel
            var gainersTask = FetchTopStocksAsync("gainers", cancellationToken);
            var losersTask = FetchTopStocksAsync("losers", cancellationToken);

            await Task.WhenAll(gainersTask, losersTask);

            var gainers = await gainersTask;
            var losers = await losersTask;

            var result = (gainers, losers);

            // TODO: Add caching with proper wrapper class for complex types

            _logger.LogInformation("Successfully fetched top movers: {GainersCount} gainers, {LosersCount} losers", 
                gainers.Count, losers.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching top movers data");
            throw new InvalidOperationException("Failed to fetch top movers data", ex);
        }
    }

    /// <summary>
    /// Search for stocks by company name or symbol
    /// </summary>
    public async Task<List<string>> SearchSymbolsAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<string>();
        }

        var normalizedQuery = query.ToUpperInvariant();
        var cacheKey = $"nse_search_{normalizedQuery}";

        try
        {
            // Check cache first (5-minute TTL for search results)
            var cachedData = await _cacheService.GetAsync<List<string>>(cacheKey, cancellationToken);
            if (cachedData != null)
            {
                return cachedData;
            }

            _logger.LogInformation("Searching symbols for query: {Query}", normalizedQuery);

            var endpoint = $"/api/search/autocomplete?q={Uri.EscapeDataString(normalizedQuery)}";
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("NSE API returned {StatusCode} for search query: {Query}", 
                    response.StatusCode, normalizedQuery);
                return new List<string>();
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var searchResponse = JsonSerializer.Deserialize<NseSearchResponse>(jsonContent, JsonOptions);

            var symbols = searchResponse?.Symbols?.Take(10).ToList() ?? new List<string>();

            // Cache the result
            await _cacheService.SetAsync(cacheKey, symbols, TimeSpan.FromMinutes(5), cancellationToken);

            _logger.LogInformation("Found {SymbolCount} symbols for query: {Query}", symbols.Count, normalizedQuery);
            return symbols;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while searching symbols for query: {Query}", normalizedQuery);
            return new List<string>();
        }
    }

    /// <summary>
    /// Get market status (open/closed)
    /// </summary>
    public async Task<bool> IsMarketOpenAsync(CancellationToken cancellationToken = default)
    {


        try
        {
            // TODO: Add caching for market status using a wrapper class
            
            var endpoint = "/api/marketStatus";
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("NSE API returned {StatusCode} for market status request", response.StatusCode);
                return false;
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusResponse = JsonSerializer.Deserialize<NseMarketStatusResponse>(jsonContent, JsonOptions);

            var isOpen = statusResponse?.MarketState?.EqualsIgnoreCase("Open") == true;

            _logger.LogInformation("Market status: {Status}", isOpen ? "Open" : "Closed");
            return isOpen;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while checking market status");
            return false;
        }
    }

    #region Private Methods

    /// <summary>
    /// Configure HTTP client with proper headers and timeout
    /// </summary>
    private void ConfigureHttpClient()
    {
        // NSE requires specific headers to prevent blocking
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.BaseAddress = new Uri(_baseUrl);
    }

    /// <summary>
    /// Map NSE quote response to StockData model
    /// </summary>
    private static StockData MapNseResponseToStockData(NseQuoteData data, string symbol)
    {
        return new StockData
        {
            Symbol = symbol,
            Exchange = "NSE",
            Price = data.LastPrice,
            Open = data.Open,
            High = data.DayHigh,
            Low = data.DayLow,
            Close = data.PreviousClose,
            Volume = data.TotalTradedVolume,
            Change = data.Change,
            ChangePercent = data.PChange,
            Timestamp = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
            Currency = "INR",
            Market = "IN",
            MarketCap = data.MarketCap,
            PE = data.PE,
            BookValue = data.BookValue
        };
    }

    /// <summary>
    /// Map NSE historical response to HistoricalStockData
    /// </summary>
    private static HistoricalStockData MapNseHistoricalResponse(List<NseHistoricalData> data, string symbol, DateTime fromDate, DateTime toDate)
    {
        var dailyPrices = data.Select(item => new DailyPrice
        {
            Date = item.Date,
            Open = item.Open,
            High = item.High,
            Low = item.Low,
            Close = item.Close,
            Volume = item.Volume,
            AdjustedClose = item.Close // NSE doesn't provide adjusted close
        }).OrderBy(p => p.Date).ToList();

        return new HistoricalStockData
        {
            Id = $"{symbol}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}",
            Symbol = symbol,
            Exchange = "NSE",
            DailyPrices = dailyPrices,
            FromDate = fromDate,
            ToDate = toDate
        };
    }

    /// <summary>
    /// Map NSE index data to StockData
    /// </summary>
    private static StockData MapIndexToStockData(NseIndexData index)
    {
        return new StockData
        {
            Symbol = index.Index,
            Exchange = "NSE",
            Price = index.Last,
            Open = index.Open,
            High = index.DayHigh,
            Low = index.DayLow,
            Close = index.PreviousClose,
            Change = index.Change,
            ChangePercent = index.PerChange,
            Timestamp = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
            Currency = "INR",
            Market = "IN"
        };
    }

    /// <summary>
    /// Check if index is important for tracking
    /// </summary>
    private static bool IsImportantIndex(string indexName)
    {
        var importantIndices = new[]
        {
            "NIFTY 50", "NIFTY BANK", "NIFTY IT", "NIFTY AUTO", 
            "NIFTY PHARMA", "NIFTY FMCG", "NIFTY METAL", "NIFTY ENERGY"
        };

        return importantIndices.Contains(indexName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Fetch top stocks (gainers/losers)
    /// </summary>
    private async Task<List<StockData>> FetchTopStocksAsync(string type, CancellationToken cancellationToken)
    {
        var endpoint = $"/api/live-analysis-variations?index=gainers&type={type}";
        var response = await _httpClient.GetAsync(endpoint, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new List<StockData>();
        }

        var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var topStocksResponse = JsonSerializer.Deserialize<NseTopStocksResponse>(jsonContent, JsonOptions);

        return topStocksResponse?.Data?.Take(10).Select(item => MapNseResponseToStockData(item, item.Symbol)).ToList() 
               ?? new List<StockData>();
    }

    #endregion

    #region DTOs for NSE API Response

    private record NseQuoteResponse(NseQuoteData? Data);
    
    private record NseQuoteData(
        string Symbol,
        decimal LastPrice,
        decimal Open,
        decimal DayHigh,
        decimal DayLow,
        decimal PreviousClose,
        decimal Change,
        decimal PChange,
        long TotalTradedVolume,
        decimal? MarketCap,
        decimal? PE,
        decimal? BookValue);

    private record NseHistoricalResponse(List<NseHistoricalData>? Data);
    
    private record NseHistoricalData(
        DateTime Date,
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close,
        long Volume);

    private record NseIndicesResponse(List<NseIndexData>? Data);
    
    private record NseIndexData(
        string Index,
        decimal Last,
        decimal Open,
        decimal DayHigh,
        decimal DayLow,
        decimal PreviousClose,
        decimal Change,
        decimal PerChange);

    private record NseSearchResponse(List<string>? Symbols);
    
    private record NseMarketStatusResponse(string? MarketState);
    
    private record NseTopStocksResponse(List<NseQuoteData>? Data);

    #endregion
}

/// <summary>
/// Extension methods for string operations
/// </summary>
public static class StringExtensions
{
    public static bool EqualsIgnoreCase(this string? source, string? target)
    {
        return string.Equals(source, target, StringComparison.OrdinalIgnoreCase);
    }
}