# Multi-stage Dockerfile for unified frontend + backend deployment
# Stage 1: Build .NET Backend
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS backend-build
WORKDIR /src

# Copy backend project file and restore dependencies
COPY docker-compose-manager-back/docker-compose-manager-back/*.csproj ./
RUN dotnet restore

# Copy backend source and build
COPY docker-compose-manager-back/docker-compose-manager-back/ ./
ARG VERSION=0.0.0
ARG ASSEMBLY_VERSION=0.0.0
ARG BUILD_DATE
ARG VCS_REF

RUN dotnet publish -c Release -o /app/backend \
    /p:Version=${VERSION} \
    /p:AssemblyVersion=${ASSEMBLY_VERSION} \
    /p:InformationalVersion=${VERSION}+${VCS_REF} && \
    echo "${VERSION}" > /app/backend/VERSION

# Stage 2: Build Frontend
FROM node:20-alpine AS frontend-build
WORKDIR /app

# Copy frontend package files and install dependencies
COPY docker-compose-manager-front/package*.json ./
RUN npm ci

# Copy frontend source and build
COPY docker-compose-manager-front/ ./
ARG VITE_APP_VERSION
ARG VITE_BUILD_DATE
ARG VITE_GIT_COMMIT

ENV VITE_APP_VERSION=${VITE_APP_VERSION}
ENV VITE_BUILD_DATE=${VITE_BUILD_DATE}
ENV VITE_GIT_COMMIT=${VITE_GIT_COMMIT}

RUN npm run build

# Stage 3: Runtime Base with Dependencies
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base

# Install nginx, supervisor, procps and Docker CLI
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        curl \
        ca-certificates \
        gnupg \
        lsb-release \
        nginx \
        supervisor \
        procps && \
    # Add Docker repository
    install -m 0755 -d /etc/apt/keyrings && \
    curl -fsSL https://download.docker.com/linux/debian/gpg | \
        gpg --dearmor -o /etc/apt/keyrings/docker.gpg && \
    chmod a+r /etc/apt/keyrings/docker.gpg && \
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
        https://download.docker.com/linux/debian $(lsb_release -cs) stable" > \
        /etc/apt/sources.list.d/docker.list && \
    # Install Docker CLI and Compose plugin
    apt-get update && \
    apt-get install -y --no-install-recommends \
        docker-ce-cli \
        docker-compose-plugin && \
    # Cleanup
    rm -rf /var/lib/apt/lists/*

# Stage 4: Final Assembly
FROM base AS final
WORKDIR /app

# Copy backend artifacts from build stage
COPY --from=backend-build /app/backend ./backend/

# Copy frontend artifacts to nginx root
COPY --from=frontend-build /app/dist ./wwwroot/

# Copy nginx configuration
COPY nginx-unified.conf /etc/nginx/sites-available/default

# Copy supervisor configuration
COPY supervisord.conf /etc/supervisor/conf.d/app.conf

# Create required directories
RUN mkdir -p /app/data /app/logs /app/compose-files /root/.docker && \
    chown -R www-data:www-data /app/wwwroot && \
    # Ensure nginx directories exist
    mkdir -p /var/log/nginx /var/lib/nginx && \
    # Ensure supervisor directories exist
    mkdir -p /var/log/supervisor

# Set environment variables for Docker config (used by docker login and the app)
ENV HOME=/root
ENV DOCKER_CONFIG=/root/.docker

# Expose HTTP port
EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=10s --retries=3 --start-period=40s \
    CMD curl -f http://localhost/health || exit 1

# Start supervisor to manage both processes
ENTRYPOINT ["/usr/bin/supervisord", "-c", "/etc/supervisor/supervisord.conf"]
