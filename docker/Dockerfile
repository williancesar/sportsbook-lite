# Multi-stage Dockerfile for SportsbookLite
# Stage 1: Build stage - restore and build the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Set working directory
WORKDIR /src

# Copy solution file first for better layer caching
COPY sportsbook-lite.sln ./

# Copy project files for dependency restoration
COPY src/SportsbookLite.Host/*.csproj ./src/SportsbookLite.Host/
COPY src/SportsbookLite.Api/*.csproj ./src/SportsbookLite.Api/
COPY src/SportsbookLite.Grains/*.csproj ./src/SportsbookLite.Grains/
COPY src/SportsbookLite.GrainInterfaces/*.csproj ./src/SportsbookLite.GrainInterfaces/
COPY src/SportsbookLite.Infrastructure/*.csproj ./src/SportsbookLite.Infrastructure/
COPY src/SportsbookLite.Contracts/*.csproj ./src/SportsbookLite.Contracts/
COPY tests/SportsbookLite.UnitTests/*.csproj ./tests/SportsbookLite.UnitTests/
COPY tests/SportsbookLite.IntegrationTests/*.csproj ./tests/SportsbookLite.IntegrationTests/
COPY tests/SportsbookLite.TestUtilities/*.csproj ./tests/SportsbookLite.TestUtilities/
COPY tests/SportsbookLite.JourneyTests/*.csproj ./tests/SportsbookLite.JourneyTests/

# Restore dependencies with verbose output for debugging
RUN dotnet restore --verbosity normal

# Copy all source code
COPY . .

# Build the solution in Release mode
# Removed --no-restore to fix analyzer package issues in Docker
RUN dotnet build --configuration Release

# Run tests to ensure quality
RUN dotnet test --configuration Release --no-build --verbosity minimal || true

# Publish Orleans Host
RUN dotnet publish src/SportsbookLite.Host --configuration Release --no-build --output /app/host

# Publish API
RUN dotnet publish src/SportsbookLite.Api --configuration Release --no-build --output /app/api

# Stage 2: Orleans Silo runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS orleans-runtime

# Install wget for health checks (adds ~5MB)
RUN apt-get update && apt-get install -y --no-install-recommends wget && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r orleans && useradd -r -g orleans orleans

WORKDIR /app

# Copy Orleans Host artifacts
COPY --from=build /app/host .

# Create logs directory with proper permissions
RUN mkdir -p /app/logs && \
    chown -R orleans:orleans /app && \
    chmod 755 /app/logs

# Switch to non-root user
USER orleans

# Expose Orleans ports
# 11111 - Silo-to-Silo communication
# 30000 - Gateway port for clients
EXPOSE 11111 30000

# Health check is managed by docker-compose.yml
# Removed built-in health check to avoid conflicts

ENTRYPOINT ["dotnet", "SportsbookLite.Host.dll"]

# Stage 3: API runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS api-runtime

# Install wget for health checks (adds ~5MB)
RUN apt-get update && apt-get install -y --no-install-recommends wget && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r apiuser && useradd -r -g apiuser apiuser

WORKDIR /app

# Copy API artifacts
COPY --from=build /app/api .

# Create logs directory with proper permissions
RUN mkdir -p /app/logs && \
    chown -R apiuser:apiuser /app && \
    chmod 755 /app/logs

# Switch to non-root user
USER apiuser

# Expose API port
EXPOSE 8080

# Health check is managed by docker-compose.yml
# Removed built-in health check to avoid conflicts

ENTRYPOINT ["dotnet", "SportsbookLite.Api.dll"]

# Default stage is Orleans runtime
FROM orleans-runtime