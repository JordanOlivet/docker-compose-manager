# syntax=docker/dockerfile:1.7
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Build arguments for version information
ARG VERSION=0.1.0-dev
ARG ASSEMBLY_VERSION=0.1.0.0
ARG BUILD_DATE=unknown
ARG VCS_REF=unknown

WORKDIR /src

# Copy csproj and restore dependencies
COPY ["docker-compose-manager-back.csproj", "./"]
COPY ["nuget.config", "./"]
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages dotnet restore docker-compose-manager-back.csproj

# Copy everything else and build
COPY . .
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages dotnet build docker-compose-manager-back.csproj -c Release -o /app/build \
    -p:Version=${ASSEMBLY_VERSION} \
    -p:AssemblyVersion=${ASSEMBLY_VERSION} \
    -p:FileVersion=${ASSEMBLY_VERSION} \
    -p:InformationalVersion=${VERSION}
# Single-file publish (self-contained) for linux-x64, trimmed when possible
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages dotnet publish docker-compose-manager-back.csproj -c Release -r linux-x64 --self-contained true \
    -p:PublishSingleFile=true -p:PublishTrimmed=true -p:EnableCompressionInSingleFile=true \
    -p:InvariantGlobalization=true -p:DebugType=None -p:DebugSymbols=false \
    -p:Version=${ASSEMBLY_VERSION} -p:InformationalVersion=${VERSION} -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0 AS runtime

# Re-declare build arguments for runtime stage
ARG VERSION=0.1.0-dev
ARG BUILD_DATE=unknown
ARG VCS_REF=unknown

# OCI image labels (https://github.com/opencontainers/image-spec/blob/main/annotations.md)
LABEL org.opencontainers.image.title="Docker Compose Manager Backend" \
      org.opencontainers.image.description="REST API backend for managing Docker containers and compose files" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.created="${BUILD_DATE}" \
      org.opencontainers.image.revision="${VCS_REF}" \
      org.opencontainers.image.vendor="Docker Compose Manager Contributors" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.url="https://github.com/yourusername/docker-compose-manager" \
      org.opencontainers.image.source="https://github.com/yourusername/docker-compose-manager" \
      org.opencontainers.image.documentation="https://github.com/yourusername/docker-compose-manager/wiki"

WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish /app

# Create directories for data and logs
RUN mkdir -p /app/data /app/logs

# Expose port
EXPOSE 5000

# Set environment
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV BUILD_DATE=${BUILD_DATE}
ENV GIT_COMMIT=${VCS_REF}

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Start application
ENTRYPOINT ["/app/docker-compose-manager-back"]
