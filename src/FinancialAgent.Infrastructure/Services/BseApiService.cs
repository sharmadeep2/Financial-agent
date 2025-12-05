using System.Net.Http.Json;
using System.Text.Json;
using FinancialAgent.Core.Models;
using FinancialAgent.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace FinancialAgent.Infrastructure.Services;

/// <summary>
/// BSE (Bombay Stock Exchange) API service implementation
/// Following Azure best practices with retry policies, proper error handling, and logging
/// Reference: https://api.bseindia.com/
/// </summary>
public class BseApiService : IBseApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BseApiService> _logger;
    private readonly ICacheService _cacheService;
    private readonly IConfiguration _configuration;
    private readonly string _baseUrl;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public BseApiService(
        HttpClient httpClient,
        ILogger<BseApiService> logger,
        ICacheService cacheService,
        IConfiguration configuration)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        
        _baseUrl = _configuration["BSE:BaseUrl"] ?? "https://api.bseindia.com";
        
        ConfigureHttpClient();
        
        _logger.LogInformation("BseApiService initialized with base URL: {BaseUrl}", _baseUrl);
    }

    /// <summary>
    /// Get real-time stock price from BSE with caching and retry logic
    /// </summary>
    public async Task<StockData?> GetStockPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));
        }

        var normalizedSymbol = symbol.ToUpperInvariant();
        var cacheKey = $"bse_stock_{normalizedSymbol}";
        
        try
        {
            // Check cache first (30-second TTL for real-time data)
            var cachedData = await _cacheService.GetAsync<StockData>(cacheKey, cancellationToken);
            if (cachedData != null)
            {
                _logger.LogDebug("Retrieved stock data for {Symbol} from cache", normalizedSymbol);
                return cachedData;
            }

            _logger.LogInformation("Fetching stock data for {Symbol} from BSE API", normalizedSymbol);

            // BSE API endpoint for stock quote - using scrip code lookup
            var scripCode = await GetScripCodeAsync(normalizedSymbol, cancellationToken);
            if (string.IsNullOrEmpty(scripCode))
            {
                _logger.LogWarning("Could not find scrip code for symbol {Symbol}", normalizedSymbol);
                return null;
            }

            var endpoint = $"/BseIndiaAPI/api/StockReachGraph/w";
            var requestData = new { scripcode = scripCode, flag = "0" }; // 0 for current data

            var response = await _httpClient.PostAsJsonAsync(endpoint, requestData, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("BSE API returned {StatusCode} for symbol {Symbol}", 
                    response.StatusCode, normalizedSymbol);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var bseResponse = JsonSerializer.Deserialize<BseStockResponse>(jsonContent, JsonOptions);

            if (bseResponse?.Data == null)
            {
                _logger.LogWarning("No data received from BSE API for symbol {Symbol}", normalizedSymbol);
                return null;
            }

            var stockData = MapBseResponseToStockData(bseResponse.Data, normalizedSymbol);
            
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
            _logger.LogError(ex, "Failed to parse BSE API response for {Symbol}", normalizedSymbol);
            throw new InvalidOperationException($"Invalid response format for {normalizedSymbol}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while fetching stock data for {Symbol}", normalizedSymbol);
            throw;
        }
    }

    /// <summary>
    /// Get historical stock data from BSE
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
        var cacheKey = $"bse_historical_{normalizedSymbol}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}";

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

            var scripCode = await GetScripCodeAsync(normalizedSymbol, cancellationToken);
            if (string.IsNullOrEmpty(scripCode))
            {
                _logger.LogWarning("Could not find scrip code for symbol {Symbol}", normalizedSymbol);
                return null;
            }

            // BSE historical data endpoint
            var endpoint = $"/BseIndiaAPI/api/StockReachGraph/w";
            var requestData = new 
            { 
                scripcode = scripCode, 
                flag = "1D", // Daily data
                frmdate = fromDate.ToString("yyyyMMdd"),
                todate = toDate.ToString("yyyyMMdd")
            };

            var response = await _httpClient.PostAsJsonAsync(endpoint, requestData, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("BSE API returned {StatusCode} for historical data request: {Symbol}", 
                    response.StatusCode, normalizedSymbol);
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var bseResponse = JsonSerializer.Deserialize<BseHistoricalResponse>(jsonContent, JsonOptions);

            if (bseResponse?.Data == null || !bseResponse.Data.Any())
            {
                _logger.LogWarning("No historical data received from BSE API for symbol {Symbol}", normalizedSymbol);
                return null;
            }

            var historicalData = MapBseHistoricalResponse(bseResponse.Data, normalizedSymbol, fromDate, toDate);
            
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
    /// Get BSE SENSEX and other indices
    /// </summary>
    public async Task<List<StockData>> GetIndicesAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "bse_indices";

        try
        {
            // Check cache first (1-minute TTL for indices)
            var cachedData = await _cacheService.GetAsync<List<StockData>>(cacheKey, cancellationToken);
            if (cachedData != null)
            {
                _logger.LogDebug("Retrieved BSE indices data from cache");
                return cachedData;
            }

            _logger.LogInformation("Fetching BSE indices data");

            var endpoint = "/BseIndiaAPI/api/Sensex/getIndices";
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("BSE API returned {StatusCode} for indices request", response.StatusCode);
                return new List<StockData>();
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var bseResponse = JsonSerializer.Deserialize<BseIndicesResponse>(jsonContent, JsonOptions);

            if (bseResponse?.Table == null)
            {
                _logger.LogWarning("No indices data received from BSE API");
                return new List<StockData>();
            }

            var indices = bseResponse.Table
                .Where(index => IsImportantBseIndex(index.IndexName))
                .Select(index => MapBseIndexToStockData(index))
                .ToList();

            // Cache the result with 1-minute expiration
            await _cacheService.SetAsync(cacheKey, indices, TimeSpan.FromMinutes(1), cancellationToken);

            _logger.LogInformation("Successfully fetched {IndicesCount} BSE indices", indices.Count);
            return indices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching BSE indices data");
            throw new InvalidOperationException("Failed to fetch BSE indices data", ex);
        }
    }

    /// <summary>
    /// Get market announcements and corporate actions
    /// </summary>
    public async Task<List<MarketAnnouncement>> GetAnnouncementsAsync(string? symbol = null, CancellationToken cancellationToken = default)
    {
        var cacheKey = string.IsNullOrEmpty(symbol) ? "bse_announcements_all" : $"bse_announcements_{symbol.ToUpperInvariant()}";

        try
        {
            // Check cache first (5-minute TTL for announcements)
            var cachedData = await _cacheService.GetAsync<List<MarketAnnouncement>>(cacheKey, cancellationToken);
            if (cachedData != null)
            {
                _logger.LogDebug("Retrieved BSE announcements from cache");
                return cachedData;
            }

            _logger.LogInformation("Fetching BSE announcements{SymbolFilter}", 
                string.IsNullOrEmpty(symbol) ? "" : $" for {symbol}");

            var endpoint = "/BseIndiaAPI/api/AnnGetData/w";
            var requestData = new 
            { 
                strCat = "-1", // All categories
                strPrevDate = DateTime.Today.AddDays(-7).ToString("yyyyMMdd"), // Last 7 days
                strScrip = symbol ?? "",
                strSearch = "P", // Pending announcements
                strToDate = DateTime.Today.ToString("yyyyMMdd"),
                strType = "C" // Corporate actions
            };

            var response = await _httpClient.PostAsJsonAsync(endpoint, requestData, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("BSE API returned {StatusCode} for announcements request", response.StatusCode);
                return new List<MarketAnnouncement>();
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var bseResponse = JsonSerializer.Deserialize<BseAnnouncementsResponse>(jsonContent, JsonOptions);

            var announcements = bseResponse?.Table?.Select(MapBseAnnouncementToMarketAnnouncement).ToList() 
                               ?? new List<MarketAnnouncement>();

            // Cache the result with 5-minute expiration
            await _cacheService.SetAsync(cacheKey, announcements, TimeSpan.FromMinutes(5), cancellationToken);

            _logger.LogInformation("Successfully fetched {AnnouncementCount} BSE announcements", announcements.Count);
            return announcements;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching BSE announcements");
            throw new InvalidOperationException("Failed to fetch BSE announcements", ex);
        }
    }

    /// <summary>
    /// Search for BSE listed companies
    /// </summary>
    public async Task<List<string>> SearchSymbolsAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<string>();
        }

        var normalizedQuery = query.ToUpperInvariant();
        var cacheKey = $"bse_search_{normalizedQuery}";

        try
        {
            // Check cache first (5-minute TTL for search results)
            var cachedData = await _cacheService.GetAsync<List<string>>(cacheKey, cancellationToken);
            if (cachedData != null)
            {
                return cachedData;
            }

            _logger.LogInformation("Searching BSE symbols for query: {Query}", normalizedQuery);

            var endpoint = "/BseIndiaAPI/api/EQPremiumSearch/w";
            var requestData = new { Flag = "1", KeyWord = normalizedQuery };

            var response = await _httpClient.PostAsJsonAsync(endpoint, requestData, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("BSE API returned {StatusCode} for search query: {Query}", 
                    response.StatusCode, normalizedQuery);
                return new List<string>();
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var searchResponse = JsonSerializer.Deserialize<BseSearchResponse>(jsonContent, JsonOptions);

            var symbols = searchResponse?.Table?.Select(item => item.ShortName)
                         .Where(name => !string.IsNullOrEmpty(name))
                         .Take(10)
                         .ToList() ?? new List<string?>();

            // Cache the result
            await _cacheService.SetAsync(cacheKey, symbols, TimeSpan.FromMinutes(5), cancellationToken);

            _logger.LogInformation("Found {SymbolCount} BSE symbols for query: {Query}", symbols.Count, normalizedQuery);
            return symbols.Where(s => s != null).Cast<string>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while searching BSE symbols for query: {Query}", normalizedQuery);
            return new List<string>();
        }
    }

    #region Private Methods

    /// <summary>
    /// Configure HTTP client with proper headers and timeout
    /// </summary>
    private void ConfigureHttpClient()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.bseindia.com/");

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.BaseAddress = new Uri(_baseUrl);
    }

    /// <summary>
    /// Get BSE scrip code for a symbol
    /// </summary>
    private async Task<string?> GetScripCodeAsync(string symbol, CancellationToken cancellationToken)
    {
        var cacheKey = $"bse_scripcode_{symbol}";
        
        var cachedCode = await _cacheService.GetAsync<string>(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedCode))
        {
            return cachedCode;
        }

        try
        {
            var endpoint = "/BseIndiaAPI/api/EQPremiumSearch/w";
            var requestData = new { Flag = "1", KeyWord = symbol };

            var response = await _httpClient.PostAsJsonAsync(endpoint, requestData, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var searchResponse = JsonSerializer.Deserialize<BseSearchResponse>(jsonContent, JsonOptions);

            var scripCode = searchResponse?.Table?.FirstOrDefault(item => 
                item.ShortName?.EqualsIgnoreCase(symbol) == true)?.ScripCode;

            if (!string.IsNullOrEmpty(scripCode))
            {
                // Cache scrip code for 1 day
                await _cacheService.SetAsync(cacheKey, scripCode, TimeSpan.FromDays(1), cancellationToken);
            }

            return scripCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scrip code for symbol {Symbol}", symbol);
            return null;
        }
    }

    /// <summary>
    /// Map BSE stock response to StockData model
    /// </summary>
    private static StockData MapBseResponseToStockData(BseStockData data, string symbol)
    {
        return new StockData
        {
            Symbol = symbol,
            Exchange = "BSE",
            Price = data.CurrentValue,
            Open = data.Open,
            High = data.High,
            Low = data.Low,
            Close = data.PrevClose,
            Volume = data.Volume,
            Change = data.CurrentValue - data.PrevClose,
            ChangePercent = ((data.CurrentValue - data.PrevClose) / data.PrevClose) * 100,
            Timestamp = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
            Currency = "INR",
            Market = "IN"
        };
    }

    /// <summary>
    /// Map BSE historical response to HistoricalStockData
    /// </summary>
    private static HistoricalStockData MapBseHistoricalResponse(List<BseHistoricalData> data, string symbol, DateTime fromDate, DateTime toDate)
    {
        var dailyPrices = data.Select(item => new DailyPrice
        {
            Date = item.Date,
            Open = item.Open,
            High = item.High,
            Low = item.Low,
            Close = item.Close,
            Volume = item.Volume,
            AdjustedClose = item.Close // BSE doesn't provide adjusted close
        }).OrderBy(p => p.Date).ToList();

        return new HistoricalStockData
        {
            Id = $"{symbol}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}",
            Symbol = symbol,
            Exchange = "BSE",
            DailyPrices = dailyPrices,
            FromDate = fromDate,
            ToDate = toDate
        };
    }

    /// <summary>
    /// Map BSE index data to StockData
    /// </summary>
    private static StockData MapBseIndexToStockData(BseIndexData index)
    {
        return new StockData
        {
            Symbol = index.IndexName,
            Exchange = "BSE",
            Price = index.CurrentValue,
            Change = index.Change,
            ChangePercent = index.PercentChange,
            Timestamp = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
            Currency = "INR",
            Market = "IN"
        };
    }

    /// <summary>
    /// Map BSE announcement to MarketAnnouncement
    /// </summary>
    private static MarketAnnouncement MapBseAnnouncementToMarketAnnouncement(BseAnnouncementData data)
    {
        return new MarketAnnouncement
        {
            Symbol = data.ScripCode ?? "",
            Title = data.ShortText ?? "",
            Description = data.FullText ?? "",
            Category = data.Category ?? "",
            AnnouncementDate = data.AttachmentDate,
            AttachmentUrl = data.AttachmentName
        };
    }

    /// <summary>
    /// Check if BSE index is important for tracking
    /// </summary>
    private static bool IsImportantBseIndex(string indexName)
    {
        var importantIndices = new[]
        {
            "SENSEX", "BSE100", "BSE200", "BSE500", "BSESMCAP", "BSEMIDCAP"
        };

        return importantIndices.Any(name => indexName?.Contains(name, StringComparison.OrdinalIgnoreCase) == true);
    }

    #endregion

    #region DTOs for BSE API Response

    private record BseStockResponse(BseStockData? Data);
    
    private record BseStockData(
        decimal CurrentValue,
        decimal Open,
        decimal High,
        decimal Low,
        decimal PrevClose,
        long Volume);

    private record BseHistoricalResponse(List<BseHistoricalData>? Data);
    
    private record BseHistoricalData(
        DateTime Date,
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close,
        long Volume);

    private record BseIndicesResponse(List<BseIndexData>? Table);
    
    private record BseIndexData(
        string IndexName,
        decimal CurrentValue,
        decimal Change,
        decimal PercentChange);

    private record BseAnnouncementsResponse(List<BseAnnouncementData>? Table);
    
    private record BseAnnouncementData(
        string? ScripCode,
        string? ShortText,
        string? FullText,
        string? Category,
        DateTime AttachmentDate,
        string? AttachmentName);

    private record BseSearchResponse(List<BseSearchData>? Table);
    
    private record BseSearchData(
        string? ScripCode,
        string? ShortName,
        string? FullName);

    #endregion
}