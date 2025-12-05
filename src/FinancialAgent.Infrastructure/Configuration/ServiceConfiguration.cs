using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using Azure.Identity;
using FinancialAgent.Core.Interfaces;
using FinancialAgent.Infrastructure.Services;
using FinancialAgent.Infrastructure.Repositories;
using Polly;
using Polly.Extensions.Http;

namespace FinancialAgent.Infrastructure.Configuration;

/// <summary>
/// Extension methods for dependency injection container configuration
/// Following Azure best practices for service registration and configuration
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configure core infrastructure services
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add HTTP clients with resilience policies
        services.AddHttpClientServices(configuration);

        // Add Azure Cosmos DB services
        services.AddCosmosDbServices(configuration);

        // Add data repositories
        services.AddRepositoryServices();

        // Add API services
        services.AddApiServices();

        return services;
    }

    /// <summary>
    /// Configure HTTP clients with Polly retry policies
    /// </summary>
    private static IServiceCollection AddHttpClientServices(this IServiceCollection services, IConfiguration configuration)
    {
        // NSE API client
        services.AddHttpClient<INseApiService, NseApiService>(client =>
        {
            client.BaseAddress = new Uri(configuration["APIs:NSE:BaseUrl"] ?? "https://www.nseindia.com/");
            client.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy());

        // BSE API client
        services.AddHttpClient<IBseApiService, BseApiService>(client =>
        {
            client.BaseAddress = new Uri(configuration["APIs:BSE:BaseUrl"] ?? "https://api.bseindia.com/");
            client.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy());

        return services;
    }

    /// <summary>
    /// Configure Azure Cosmos DB with managed identity
    /// </summary>
    private static IServiceCollection AddCosmosDbServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(serviceProvider =>
        {
            var connectionString = configuration.GetConnectionString("CosmosDB");
            var cosmosClientOptions = new CosmosClientOptions
            {
                ApplicationName = "FinancialAgent",
                ConnectionMode = ConnectionMode.Direct,
                ConsistencyLevel = ConsistencyLevel.Session,
                MaxRetryAttemptsOnRateLimitedRequests = 3,
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30),
                RequestTimeout = TimeSpan.FromSeconds(10),
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                    IgnoreNullValues = true
                }
            };

            // Use connection string if provided, otherwise use DefaultAzureCredential
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return new CosmosClient(connectionString, cosmosClientOptions);
            }
            else
            {
                var accountEndpoint = configuration["CosmosDB:AccountEndpoint"];
                if (string.IsNullOrWhiteSpace(accountEndpoint))
                {
                    throw new InvalidOperationException(
                        "Either CosmosDB connection string or CosmosDB:AccountEndpoint must be configured");
                }

                var credential = new DefaultAzureCredential();
                return new CosmosClient(accountEndpoint, credential, cosmosClientOptions);
            }
        });

        // Initialize Cosmos DB containers on startup
        services.AddSingleton<ICosmosDbInitializer, CosmosDbInitializer>();

        return services;
    }

    /// <summary>
    /// Configure data repositories
    /// </summary>
    private static IServiceCollection AddRepositoryServices(this IServiceCollection services)
    {
        services.AddScoped<IMarketDataRepository, MarketDataRepository>();
        return services;
    }

    /// <summary>
    /// Configure API services
    /// </summary>
    private static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddScoped<INseApiService, NseApiService>();
        services.AddScoped<IBseApiService, BseApiService>();
        return services;
    }

    /// <summary>
    /// HTTP retry policy with exponential backoff
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode && (int)msg.StatusCode != 404)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    /// <summary>
    /// Circuit breaker policy
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
    }
}

/// <summary>
/// Cosmos DB initialization service
/// </summary>
public interface ICosmosDbInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Cosmos DB container and database initialization
/// </summary>
public class CosmosDbInitializer : ICosmosDbInitializer
{
    private readonly CosmosClient _cosmosClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CosmosDbInitializer> _logger;

    public CosmosDbInitializer(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<CosmosDbInitializer> logger)
    {
        _cosmosClient = cosmosClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var databaseName = _configuration["CosmosDB:DatabaseName"] ?? "FinancialData";

        try
        {
            _logger.LogInformation("Initializing Cosmos DB database: {DatabaseName}", databaseName);

            // Create database if it doesn't exist
            var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(
                id: databaseName,
                throughput: 400, // Shared throughput
                cancellationToken: cancellationToken);

            var database = databaseResponse.Database;

            // Create containers
            await CreateContainersAsync(database, cancellationToken);

            _logger.LogInformation("Cosmos DB initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Cosmos DB");
            throw;
        }
    }

    private async Task CreateContainersAsync(Database database, CancellationToken cancellationToken)
    {
        var containers = new[]
        {
            new ContainerProperties
            {
                Id = "market-data",
                PartitionKeyPath = "/partitionKey",
                DefaultTimeToLive = (int)TimeSpan.FromDays(30).TotalSeconds
            },
            new ContainerProperties
            {
                Id = "historical-data",
                PartitionKeyPath = "/partitionKey"
            },
            new ContainerProperties
            {
                Id = "technical-indicators",
                PartitionKeyPath = "/partitionKey",
                DefaultTimeToLive = (int)TimeSpan.FromDays(7).TotalSeconds
            }
        };

        foreach (var containerProps in containers)
        {
            try
            {
                var response = await database.CreateContainerIfNotExistsAsync(
                    containerProperties: containerProps,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Container '{ContainerId}' ready - Status: {StatusCode}, RU consumed: {RequestCharge}",
                    containerProps.Id, response.StatusCode, response.RequestCharge);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create container: {ContainerId}", containerProps.Id);
                throw;
            }
        }
    }
}