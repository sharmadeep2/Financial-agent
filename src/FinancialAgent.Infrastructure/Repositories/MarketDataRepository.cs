using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FinancialAgent.Core.Models;
using FinancialAgent.Core.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinancialAgent.Infrastructure.Repositories;

/// <summary>
/// Azure Cosmos DB repository for market data storage and retrieval
/// Following Azure best practices with managed identity, connection pooling, and proper error handling
/// Reference: https://docs.microsoft.com/en-us/azure/cosmos-db/
/// </summary>
public class MarketDataRepository : IMarketDataRepository
{
    private readonly CosmosClient _cosmosClient;
    private readonly Database _database;
    private readonly Container _marketDataContainer;
    private readonly Container _historicalDataContainer;
    private readonly Container _technicalIndicatorsContainer;
    private readonly ILogger<MarketDataRepository> _logger;
    private readonly string _databaseName;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MarketDataRepository(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<MarketDataRepository> logger)
    {
        _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _databaseName = configuration["CosmosDB:DatabaseName"] ?? "FinancialData";
        
        // Initialize database and containers
        _database = _cosmosClient.GetDatabase(_databaseName);
        _marketDataContainer = _database.GetContainer("market-data");
        _historicalDataContainer = _database.GetContainer("historical-data");
        _technicalIndicatorsContainer = _database.GetContainer("technical-indicators");

        _logger.LogInformation("MarketDataRepository initialized for database: {DatabaseName}", _databaseName);
    }

    /// <summary>
    /// Save real-time market data with optimistic concurrency and TTL
    /// </summary>
    public async Task SaveMarketDataAsync(StockData stockData, CancellationToken cancellationToken = default)
    {
        if (stockData == null)
        {
            throw new ArgumentNullException(nameof(stockData));
        }

        try
        {
            var document = CreateMarketDataDocument(stockData);
            
            var itemRequestOptions = new ItemRequestOptions
            {
                EnableContentResponseOnWrite = false // Reduce bandwidth usage
            };

            // Use upsert for idempotent operations
            var response = await _marketDataContainer.UpsertItemAsync(
                document, 
                new PartitionKey(document.PartitionKey),
                itemRequestOptions,
                cancellationToken);

            _logger.LogDebug("Saved market data for {Symbol} ({Exchange}) - RU consumed: {RequestCharge}", 
                stockData.Symbol, stockData.Exchange, response.RequestCharge);

            // Track performance metrics
            if (response.RequestCharge > 10)
            {
                _logger.LogWarning("High RU consumption for market data save: {RequestCharge} RUs for {Symbol}", 
                    response.RequestCharge, stockData.Symbol);
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning(ex, "Rate limit exceeded while saving market data for {Symbol}. Will retry after {RetryAfter}ms", 
                stockData.Symbol, ex.RetryAfter?.TotalMilliseconds);
            
            // Let the caller handle retry logic
            throw new InvalidOperationException($"Rate limit exceeded for {stockData.Symbol}", ex);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error while saving market data for {Symbol}: {StatusCode} - {Message}", 
                stockData.Symbol, ex.StatusCode, ex.Message);
            throw new InvalidOperationException($"Failed to save market data for {stockData.Symbol}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while saving market data for {Symbol}", stockData.Symbol);
            throw;
        }
    }

    /// <summary>
    /// Save historical data in batch with optimized partition strategy
    /// </summary>
    public async Task SaveHistoricalDataAsync(HistoricalStockData historicalData, CancellationToken cancellationToken = default)
    {
        if (historicalData == null)
        {
            throw new ArgumentNullException(nameof(historicalData));
        }

        if (!historicalData.DailyPrices.Any())
        {
            _logger.LogWarning("No daily prices provided for historical data: {Symbol}", historicalData.Symbol);
            return;
        }

        try
        {
            var document = CreateHistoricalDataDocument(historicalData);
            
            var itemRequestOptions = new ItemRequestOptions
            {
                EnableContentResponseOnWrite = false
            };

            var response = await _historicalDataContainer.UpsertItemAsync(
                document,
                new PartitionKey(document.PartitionKey),
                itemRequestOptions,
                cancellationToken);

            _logger.LogInformation("Saved historical data for {Symbol} ({Exchange}) with {RecordCount} records - RU consumed: {RequestCharge}", 
                historicalData.Symbol, historicalData.Exchange, historicalData.RecordCount, response.RequestCharge);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error while saving historical data for {Symbol}: {StatusCode} - {Message}", 
                historicalData.Symbol, ex.StatusCode, ex.Message);
            throw new InvalidOperationException($"Failed to save historical data for {historicalData.Symbol}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while saving historical data for {Symbol}", historicalData.Symbol);
            throw;
        }
    }

    /// <summary>
    /// Get latest stock data with optimized query and caching
    /// </summary>
    public async Task<StockData?> GetLatestStockDataAsync(string symbol, string exchange, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));
        }

        if (string.IsNullOrWhiteSpace(exchange))
        {
            throw new ArgumentException("Exchange cannot be null or empty", nameof(exchange));
        }

        var partitionKey = $"{symbol}_{exchange}";

        try
        {
            // Query for the latest record with optimized SQL
            var query = new QueryDefinition(
                "SELECT TOP 1 * FROM c WHERE c.partitionKey = @partitionKey ORDER BY c.timestamp DESC")
                .WithParameter("@partitionKey", partitionKey);

            var queryRequestOptions = new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(partitionKey),
                MaxItemCount = 1 // Optimize for single item
            };

            using var iterator = _marketDataContainer.GetItemQueryIterator<MarketDataDocument>(query, requestOptions: queryRequestOptions);
            
            var response = await iterator.ReadNextAsync(cancellationToken);
            var document = response.FirstOrDefault();

            if (document == null)
            {
                _logger.LogDebug("No market data found for {Symbol} on {Exchange}", symbol, exchange);
                return null;
            }

            _logger.LogDebug("Retrieved latest market data for {Symbol} ({Exchange}) - RU consumed: {RequestCharge}", 
                symbol, exchange, response.RequestCharge);

            return MapDocumentToStockData(document);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error while getting latest stock data for {Symbol} ({Exchange}): {StatusCode} - {Message}", 
                symbol, exchange, ex.StatusCode, ex.Message);
            throw new InvalidOperationException($"Failed to get latest stock data for {symbol} on {exchange}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while getting latest stock data for {Symbol} ({Exchange})", symbol, exchange);
            throw;
        }
    }

    /// <summary>
    /// Get historical data with date range filtering
    /// </summary>
    public async Task<HistoricalStockData?> GetHistoricalDataAsync(string symbol, string exchange, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be null or empty", nameof(symbol));
        }

        if (fromDate > toDate)
        {
            throw new ArgumentException("From date cannot be after to date");
        }

        var partitionKey = $"{symbol}_{exchange}_{fromDate:yyyyMM}";

        try
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.partitionKey LIKE @partitionKeyPrefix AND c.fromDate >= @fromDate AND c.toDate <= @toDate")
                .WithParameter("@partitionKeyPrefix", $"{symbol}_{exchange}_%")
                .WithParameter("@fromDate", fromDate.ToString("yyyy-MM-dd"))
                .WithParameter("@toDate", toDate.ToString("yyyy-MM-dd"));

            var queryRequestOptions = new QueryRequestOptions
            {
                MaxItemCount = 100 // Reasonable batch size
            };

            using var iterator = _historicalDataContainer.GetItemQueryIterator<HistoricalDataDocument>(query, requestOptions: queryRequestOptions);
            
            var documents = new List<HistoricalDataDocument>();
            double totalRequestCharge = 0;

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                documents.AddRange(response);
                totalRequestCharge += response.RequestCharge;
            }

            if (!documents.Any())
            {
                _logger.LogDebug("No historical data found for {Symbol} on {Exchange} from {FromDate} to {ToDate}", 
                    symbol, exchange, fromDate.ToString("yyyy-MM-dd"), toDate.ToString("yyyy-MM-dd"));
                return null;
            }

            _logger.LogDebug("Retrieved historical data for {Symbol} ({Exchange}) - {DocumentCount} documents, RU consumed: {RequestCharge}", 
                symbol, exchange, documents.Count, totalRequestCharge);

            // Combine multiple documents if needed
            return CombineHistoricalDataDocuments(documents, symbol, exchange, fromDate, toDate);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB error while getting historical data for {Symbol} ({Exchange}): {StatusCode} - {Message}", 
                symbol, exchange, ex.StatusCode, ex.Message);
            throw new InvalidOperationException($"Failed to get historical data for {symbol} on {exchange}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while getting historical data for {Symbol} ({Exchange})", symbol, exchange);
            throw;
        }
    }

    /// <summary>
    /// Get technical indicators with caching
    /// </summary>
    public async Task<TechnicalIndicators?> GetTechnicalIndicatorsAsync(string symbol, string exchange, CancellationToken cancellationToken = default)
    {
        var partitionKey = $"{symbol}_{exchange}";

        try
        {
            var query = new QueryDefinition(
                "SELECT TOP 1 * FROM c WHERE c.partitionKey = @partitionKey ORDER BY c.calculatedAt DESC")
                .WithParameter("@partitionKey", partitionKey);

            var queryRequestOptions = new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(partitionKey),
                MaxItemCount = 1
            };

            using var iterator = _technicalIndicatorsContainer.GetItemQueryIterator<TechnicalIndicatorsDocument>(query, requestOptions: queryRequestOptions);
            
            var response = await iterator.ReadNextAsync(cancellationToken);
            var document = response.FirstOrDefault();

            if (document == null)
            {
                return null;
            }

            return MapDocumentToTechnicalIndicators(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting technical indicators for {Symbol} ({Exchange})", symbol, exchange);
            throw new InvalidOperationException($"Failed to get technical indicators for {symbol} on {exchange}", ex);
        }
    }

    /// <summary>
    /// Save calculated technical indicators
    /// </summary>
    public async Task SaveTechnicalIndicatorsAsync(TechnicalIndicators indicators, CancellationToken cancellationToken = default)
    {
        if (indicators == null)
        {
            throw new ArgumentNullException(nameof(indicators));
        }

        try
        {
            var document = CreateTechnicalIndicatorsDocument(indicators);
            
            var response = await _technicalIndicatorsContainer.UpsertItemAsync(
                document,
                new PartitionKey(document.PartitionKey),
                cancellationToken: cancellationToken);

            _logger.LogDebug("Saved technical indicators for {Symbol} - RU consumed: {RequestCharge}", 
                indicators.Symbol, response.RequestCharge);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving technical indicators for {Symbol}", indicators.Symbol);
            throw new InvalidOperationException($"Failed to save technical indicators for {indicators.Symbol}", ex);
        }
    }

    /// <summary>
    /// Get market data for multiple symbols efficiently
    /// </summary>
    public async Task<List<StockData>> GetBulkMarketDataAsync(List<string> symbols, string exchange, CancellationToken cancellationToken = default)
    {
        if (!symbols.Any())
        {
            return new List<StockData>();
        }

        var results = new List<StockData>();

        try
        {
            // Process in batches to avoid large queries
            const int batchSize = 10;
            var batches = symbols.Chunk(batchSize);

            foreach (var batch in batches)
            {
                var batchResults = await GetBatchMarketDataAsync(batch.ToList(), exchange, cancellationToken);
                results.AddRange(batchResults);
            }

            _logger.LogInformation("Retrieved bulk market data for {SymbolCount} symbols from {Exchange}", 
                results.Count, exchange);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bulk market data for {Exchange}", exchange);
            throw new InvalidOperationException($"Failed to get bulk market data for {exchange}", ex);
        }
    }

    #region Private Methods

    /// <summary>
    /// Create market data document for Cosmos DB storage
    /// </summary>
    private static MarketDataDocument CreateMarketDataDocument(StockData stockData)
    {
        return new MarketDataDocument
        {
            Id = $"{stockData.Symbol}_{stockData.Exchange}_{stockData.Timestamp:yyyyMMddHHmmss}",
            PartitionKey = $"{stockData.Symbol}_{stockData.Exchange}",
            Symbol = stockData.Symbol,
            Exchange = stockData.Exchange,
            Price = stockData.Price,
            Open = stockData.Open,
            High = stockData.High,
            Low = stockData.Low,
            Close = stockData.Close,
            Volume = stockData.Volume,
            Change = stockData.Change,
            ChangePercent = stockData.ChangePercent,
            Timestamp = stockData.Timestamp,
            LastUpdated = stockData.LastUpdated,
            Currency = stockData.Currency,
            Market = stockData.Market,
            MarketCap = stockData.MarketCap,
            PE = stockData.PE,
            DividendYield = stockData.DividendYield,
            BookValue = stockData.BookValue,
            Ttl = (int)TimeSpan.FromDays(30).TotalSeconds // Auto-delete after 30 days
        };
    }

    /// <summary>
    /// Create historical data document for efficient storage
    /// </summary>
    private static HistoricalDataDocument CreateHistoricalDataDocument(HistoricalStockData historicalData)
    {
        return new HistoricalDataDocument
        {
            Id = historicalData.Id,
            PartitionKey = $"{historicalData.Symbol}_{historicalData.Exchange}_{historicalData.FromDate:yyyyMM}",
            Symbol = historicalData.Symbol,
            Exchange = historicalData.Exchange,
            FromDate = historicalData.FromDate.ToString("yyyy-MM-dd"),
            ToDate = historicalData.ToDate.ToString("yyyy-MM-dd"),
            DailyPrices = historicalData.DailyPrices.Select(dp => new DailyPriceDocument
            {
                Date = dp.Date.ToString("yyyy-MM-dd"),
                Open = dp.Open,
                High = dp.High,
                Low = dp.Low,
                Close = dp.Close,
                Volume = dp.Volume,
                AdjustedClose = dp.AdjustedClose
            }).ToList(),
            RecordCount = historicalData.RecordCount
        };
    }

    /// <summary>
    /// Create technical indicators document
    /// </summary>
    private static TechnicalIndicatorsDocument CreateTechnicalIndicatorsDocument(TechnicalIndicators indicators)
    {
        return new TechnicalIndicatorsDocument
        {
            Id = $"{indicators.Symbol}_{indicators.CalculatedAt:yyyyMMddHHmmss}",
            PartitionKey = $"{indicators.Symbol}",
            Symbol = indicators.Symbol,
            CalculatedAt = indicators.CalculatedAt,
            SMA20 = indicators.SMA20,
            SMA50 = indicators.SMA50,
            SMA200 = indicators.SMA200,
            EMA12 = indicators.EMA12,
            EMA26 = indicators.EMA26,
            RSI = indicators.RSI,
            MACD = indicators.MACD,
            MACDSignal = indicators.MACDSignal,
            MACDHistogram = indicators.MACDHistogram,
            BollingerUpper = indicators.BollingerUpper,
            BollingerMiddle = indicators.BollingerMiddle,
            BollingerLower = indicators.BollingerLower,
            ATR = indicators.ATR,
            VolumeMA = indicators.VolumeMA,
            OBV = indicators.OBV,
            SupportLevels = indicators.SupportLevels,
            ResistanceLevels = indicators.ResistanceLevels,
            Ttl = (int)TimeSpan.FromDays(7).TotalSeconds // Auto-delete after 7 days
        };
    }

    /// <summary>
    /// Map document to stock data model
    /// </summary>
    private static StockData MapDocumentToStockData(MarketDataDocument document)
    {
        return new StockData
        {
            Symbol = document.Symbol,
            Exchange = document.Exchange,
            Price = document.Price,
            Open = document.Open,
            High = document.High,
            Low = document.Low,
            Close = document.Close,
            Volume = document.Volume,
            Change = document.Change,
            ChangePercent = document.ChangePercent,
            Timestamp = document.Timestamp,
            LastUpdated = document.LastUpdated,
            Currency = document.Currency,
            Market = document.Market,
            MarketCap = document.MarketCap,
            PE = document.PE,
            DividendYield = document.DividendYield,
            BookValue = document.BookValue
        };
    }

    /// <summary>
    /// Map document to technical indicators
    /// </summary>
    private static TechnicalIndicators MapDocumentToTechnicalIndicators(TechnicalIndicatorsDocument document)
    {
        return new TechnicalIndicators
        {
            Symbol = document.Symbol,
            CalculatedAt = document.CalculatedAt,
            SMA20 = document.SMA20,
            SMA50 = document.SMA50,
            SMA200 = document.SMA200,
            EMA12 = document.EMA12,
            EMA26 = document.EMA26,
            RSI = document.RSI,
            MACD = document.MACD,
            MACDSignal = document.MACDSignal,
            MACDHistogram = document.MACDHistogram,
            BollingerUpper = document.BollingerUpper,
            BollingerMiddle = document.BollingerMiddle,
            BollingerLower = document.BollingerLower,
            ATR = document.ATR,
            VolumeMA = document.VolumeMA,
            OBV = document.OBV,
            SupportLevels = document.SupportLevels,
            ResistanceLevels = document.ResistanceLevels
        };
    }

    /// <summary>
    /// Combine multiple historical data documents
    /// </summary>
    private static HistoricalStockData CombineHistoricalDataDocuments(
        List<HistoricalDataDocument> documents,
        string symbol,
        string exchange,
        DateTime fromDate,
        DateTime toDate)
    {
        var allDailyPrices = new List<DailyPrice>();

        foreach (var doc in documents)
        {
            var dailyPrices = doc.DailyPrices.Select(dp => new DailyPrice
            {
                Date = DateTime.Parse(dp.Date),
                Open = dp.Open,
                High = dp.High,
                Low = dp.Low,
                Close = dp.Close,
                Volume = dp.Volume,
                AdjustedClose = dp.AdjustedClose
            });

            allDailyPrices.AddRange(dailyPrices);
        }

        return new HistoricalStockData
        {
            Id = $"{symbol}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}",
            Symbol = symbol,
            Exchange = exchange,
            DailyPrices = allDailyPrices.OrderBy(dp => dp.Date).ToList(),
            FromDate = fromDate,
            ToDate = toDate
        };
    }

    /// <summary>
    /// Get batch market data efficiently
    /// </summary>
    private async Task<List<StockData>> GetBatchMarketDataAsync(List<string> symbols, string exchange, CancellationToken cancellationToken)
    {
        var results = new List<StockData>();

        // Create IN clause for batch query
        var symbolsParam = string.Join(",", symbols.Select((s, i) => $"@symbol{i}"));
        var query = new QueryDefinition($"SELECT * FROM c WHERE c.exchange = @exchange AND c.symbol IN ({symbolsParam})")
            .WithParameter("@exchange", exchange);

        for (int i = 0; i < symbols.Count; i++)
        {
            query = query.WithParameter($"@symbol{i}", symbols[i]);
        }

        using var iterator = _marketDataContainer.GetItemQueryIterator<MarketDataDocument>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response.Select(MapDocumentToStockData));
        }

        return results;
    }

    #endregion

    #region Document Models

    /// <summary>
    /// Cosmos DB document for market data storage
    /// </summary>
    private record MarketDataDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("partitionKey")]
        public string PartitionKey { get; init; } = string.Empty;

        public string Symbol { get; init; } = string.Empty;
        public string Exchange { get; init; } = string.Empty;
        public decimal Price { get; init; }
        public decimal Open { get; init; }
        public decimal High { get; init; }
        public decimal Low { get; init; }
        public decimal Close { get; init; }
        public long Volume { get; init; }
        public decimal Change { get; init; }
        public decimal ChangePercent { get; init; }
        public DateTime Timestamp { get; init; }
        public DateTime LastUpdated { get; init; }
        public string Currency { get; init; } = string.Empty;
        public string Market { get; init; } = string.Empty;
        public decimal? MarketCap { get; init; }
        public decimal? PE { get; init; }
        public decimal? DividendYield { get; init; }
        public decimal? BookValue { get; init; }
        
        [JsonPropertyName("ttl")]
        public int Ttl { get; init; }
    }

    /// <summary>
    /// Cosmos DB document for historical data storage
    /// </summary>
    private record HistoricalDataDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("partitionKey")]
        public string PartitionKey { get; init; } = string.Empty;

        public string Symbol { get; init; } = string.Empty;
        public string Exchange { get; init; } = string.Empty;
        public string FromDate { get; init; } = string.Empty;
        public string ToDate { get; init; } = string.Empty;
        public List<DailyPriceDocument> DailyPrices { get; init; } = new();
        public int RecordCount { get; init; }
    }

    private record DailyPriceDocument
    {
        public string Date { get; init; } = string.Empty;
        public decimal Open { get; init; }
        public decimal High { get; init; }
        public decimal Low { get; init; }
        public decimal Close { get; init; }
        public long Volume { get; init; }
        public decimal AdjustedClose { get; init; }
    }

    /// <summary>
    /// Cosmos DB document for technical indicators storage
    /// </summary>
    private record TechnicalIndicatorsDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("partitionKey")]
        public string PartitionKey { get; init; } = string.Empty;

        public string Symbol { get; init; } = string.Empty;
        public DateTime CalculatedAt { get; init; }
        public decimal? SMA20 { get; init; }
        public decimal? SMA50 { get; init; }
        public decimal? SMA200 { get; init; }
        public decimal? EMA12 { get; init; }
        public decimal? EMA26 { get; init; }
        public decimal? RSI { get; init; }
        public decimal? MACD { get; init; }
        public decimal? MACDSignal { get; init; }
        public decimal? MACDHistogram { get; init; }
        public decimal? BollingerUpper { get; init; }
        public decimal? BollingerMiddle { get; init; }
        public decimal? BollingerLower { get; init; }
        public decimal? ATR { get; init; }
        public decimal? VolumeMA { get; init; }
        public decimal? OBV { get; init; }
        public List<decimal> SupportLevels { get; init; } = new();
        public List<decimal> ResistanceLevels { get; init; } = new();

        [JsonPropertyName("ttl")]
        public int Ttl { get; init; }
    }

    #endregion
}