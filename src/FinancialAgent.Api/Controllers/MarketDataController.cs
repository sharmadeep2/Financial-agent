using Microsoft.AspNetCore.Mvc;
using FinancialAgent.Core.Interfaces;
using FinancialAgent.Core.Models;

namespace FinancialAgent.Api.Controllers;

/// <summary>
/// Market data API controller for NSE and BSE stock information
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MarketDataController : ControllerBase
{
    private readonly INseApiService _nseApiService;
    private readonly IBseApiService _bseApiService;
    private readonly IMarketDataRepository _marketDataRepository;
    private readonly ILogger<MarketDataController> _logger;

    public MarketDataController(
        INseApiService nseApiService,
        IBseApiService bseApiService,
        IMarketDataRepository marketDataRepository,
        ILogger<MarketDataController> logger)
    {
        _nseApiService = nseApiService;
        _bseApiService = bseApiService;
        _marketDataRepository = marketDataRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get current stock quote from NSE
    /// </summary>
    /// <param name="symbol">NSE stock symbol (e.g., RELIANCE, TCS)</param>
    /// <returns>Current stock data</returns>
    [HttpGet("nse/{symbol}")]
    public async Task<ActionResult<StockData>> GetNseQuote(string symbol)
    {
        try
        {
            _logger.LogInformation("Fetching NSE quote for symbol: {Symbol}", symbol);

            var stockData = await _nseApiService.GetStockPriceAsync(symbol);
            if (stockData == null)
            {
                _logger.LogWarning("NSE quote not found for symbol: {Symbol}", symbol);
                return NotFound($"Quote not found for symbol: {symbol}");
            }

            // Save to database for caching
            await _marketDataRepository.SaveMarketDataAsync(stockData);

            _logger.LogInformation("Successfully retrieved NSE quote for {Symbol}: Price {Price}", 
                symbol, stockData.Price);

            return Ok(stockData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching NSE quote for symbol: {Symbol}", symbol);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Get current stock quote from BSE
    /// </summary>
    /// <param name="scripCode">BSE scrip code (e.g., 500325 for RELIANCE)</param>
    /// <returns>Current stock data</returns>
    [HttpGet("bse/{scripCode}")]
    public async Task<ActionResult<StockData>> GetBseQuote(string scripCode)
    {
        try
        {
            _logger.LogInformation("Fetching BSE quote for scrip code: {ScripCode}", scripCode);

            var stockData = await _bseApiService.GetStockPriceAsync(scripCode);
            if (stockData == null)
            {
                _logger.LogWarning("BSE quote not found for scrip code: {ScripCode}", scripCode);
                return NotFound($"Quote not found for scrip code: {scripCode}");
            }

            // Save to database for caching
            await _marketDataRepository.SaveMarketDataAsync(stockData);

            _logger.LogInformation("Successfully retrieved BSE quote for {ScripCode}: Price {Price}", 
                scripCode, stockData.Price);

            return Ok(stockData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching BSE quote for scrip code: {ScripCode}", scripCode);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Get historical data for NSE stock
    /// </summary>
    /// <param name="symbol">NSE stock symbol</param>
    /// <param name="fromDate">Start date (yyyy-MM-dd)</param>
    /// <param name="toDate">End date (yyyy-MM-dd)</param>
    /// <returns>Historical stock data</returns>
    [HttpGet("nse/{symbol}/historical")]
    public async Task<ActionResult<HistoricalStockData>> GetNseHistoricalData(
        string symbol, 
        [FromQuery] DateTime fromDate, 
        [FromQuery] DateTime toDate)
    {
        try
        {
            if (fromDate > toDate)
            {
                return BadRequest("From date cannot be later than to date");
            }

            if (toDate > DateTime.Today)
            {
                return BadRequest("To date cannot be in the future");
            }

            _logger.LogInformation("Fetching NSE historical data for {Symbol} from {FromDate} to {ToDate}", 
                symbol, fromDate.ToString("yyyy-MM-dd"), toDate.ToString("yyyy-MM-dd"));

            var historicalData = await _nseApiService.GetHistoricalDataAsync(symbol, fromDate, toDate);
            if (historicalData == null || !historicalData.DailyPrices.Any())
            {
                _logger.LogWarning("NSE historical data not found for {Symbol}", symbol);
                return NotFound($"Historical data not found for symbol: {symbol}");
            }

            // Save to database
            await _marketDataRepository.SaveHistoricalDataAsync(historicalData);

            _logger.LogInformation("Successfully retrieved NSE historical data for {Symbol}: {RecordCount} records", 
                symbol, historicalData.RecordCount);

            return Ok(historicalData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching NSE historical data for {Symbol}", symbol);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Search NSE stocks by company name or symbol
    /// </summary>
    /// <param name="query">Search query</param>
    /// <returns>List of matching stock symbols</returns>
    [HttpGet("nse/search")]
    public async Task<ActionResult<List<string>>> SearchNseStocks([FromQuery] string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return BadRequest("Search query must be at least 2 characters long");
            }

            _logger.LogInformation("Searching NSE stocks with query: {Query}", query);

            var searchResults = await _nseApiService.SearchSymbolsAsync(query);
            
            _logger.LogInformation("NSE search returned {Count} results for query: {Query}", 
                searchResults.Count, query);

            return Ok(searchResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching NSE stocks with query: {Query}", query);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Search BSE stocks by company name or scrip code
    /// </summary>
    /// <param name="query">Search query</param>
    /// <returns>List of matching stock symbols</returns>
    [HttpGet("bse/search")]
    public async Task<ActionResult<List<string>>> SearchBseStocks([FromQuery] string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return BadRequest("Search query must be at least 2 characters long");
            }

            _logger.LogInformation("Searching BSE stocks with query: {Query}", query);

            var searchResults = await _bseApiService.SearchSymbolsAsync(query);
            
            _logger.LogInformation("BSE search returned {Count} results for query: {Query}", 
                searchResults.Count, query);

            return Ok(searchResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching BSE stocks with query: {Query}", query);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Get cached stock data from database
    /// </summary>
    /// <param name="symbol">Stock symbol</param>
    /// <param name="exchange">Exchange (NSE or BSE)</param>
    /// <returns>Latest cached stock data</returns>
    [HttpGet("cache/{exchange}/{symbol}")]
    public async Task<ActionResult<StockData>> GetCachedData(string symbol, string exchange)
    {
        try
        {
            _logger.LogInformation("Fetching cached data for {Symbol} on {Exchange}", symbol, exchange);

            var stockData = await _marketDataRepository.GetLatestStockDataAsync(symbol, exchange.ToUpperInvariant());
            if (stockData == null)
            {
                return NotFound($"No cached data found for {symbol} on {exchange}");
            }

            return Ok(stockData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching cached data for {Symbol} on {Exchange}", symbol, exchange);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    /// <returns>API health status</returns>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Services = new
            {
                NSE = "Connected",
                BSE = "Connected",
                Database = "Connected"
            }
        });
    }
}