# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["docker-compose-manager-back.csproj", "./"]
COPY ["nuget.config", "./"]
RUN dotnet restore docker-compose-manager-back.csproj

# Copy everything else and build
COPY . .
RUN dotnet build docker-compose-manager-back.csproj -c Release -o /app/build
RUN dotnet publish docker-compose-manager-back.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Create directories for data and logs
RUN mkdir -p /app/data /app/logs

# Expose port
EXPOSE 5000

# Set environment
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Start application
ENTRYPOINT ["dotnet", "docker-compose-manager-back.dll"]
