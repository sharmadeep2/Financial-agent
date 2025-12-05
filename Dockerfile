# Financial Agent API Docker Configuration
# Multi-stage build for optimized production image

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file
COPY FinancialAgent.sln ./

# Copy project files
COPY src/FinancialAgent.Core/FinancialAgent.Core.csproj ./src/FinancialAgent.Core/
COPY src/FinancialAgent.Infrastructure/FinancialAgent.Infrastructure.csproj ./src/FinancialAgent.Infrastructure/
COPY src/FinancialAgent.Agents/FinancialAgent.Agents.csproj ./src/FinancialAgent.Agents/
COPY src/FinancialAgent.Api/FinancialAgent.Api.csproj ./src/FinancialAgent.Api/

# Restore NuGet packages
RUN dotnet restore

# Copy source code
COPY src/ ./src/

# Build the application
RUN dotnet build --configuration Release --no-restore

# Publish the application
RUN dotnet publish src/FinancialAgent.Api/FinancialAgent.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-build

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# Copy published application
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "FinancialAgent.Api.dll"]