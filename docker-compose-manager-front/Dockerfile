# syntax=docker/dockerfile:1.7
# Build stage
FROM node:20-alpine AS build

# Build arguments for version information and configuration
ARG VERSION=0.1.0-dev
ARG BUILD_DATE=unknown
ARG VCS_REF=unknown
ARG VITE_API_URL
ARG VITE_APP_VERSION
ARG VITE_BUILD_DATE
ARG VITE_GIT_COMMIT

# Set environment variables for Vite build
ENV VITE_API_URL=${VITE_API_URL}
ENV VITE_APP_VERSION=${VITE_APP_VERSION:-${VERSION}}
ENV VITE_BUILD_DATE=${VITE_BUILD_DATE:-${BUILD_DATE}}
ENV VITE_GIT_COMMIT=${VITE_GIT_COMMIT:-${VCS_REF}}

WORKDIR /app

# Copy package files
COPY package*.json ./
RUN --mount=type=cache,id=npm-cache,target=/root/.npm \
      --mount=type=cache,id=npm-modules,target=/app/node_modules \
      npm ci

# Copy source and build
COPY . .
RUN --mount=type=cache,id=npm-cache,target=/root/.npm npm run build

# Production stage
FROM nginx:alpine

# Re-declare build arguments for runtime stage
ARG VERSION=0.1.0-dev
ARG BUILD_DATE=unknown
ARG VCS_REF=unknown

# OCI image labels (https://github.com/opencontainers/image-spec/blob/main/annotations.md)
LABEL org.opencontainers.image.title="Docker Compose Manager Frontend" \
      org.opencontainers.image.description="Web UI frontend for managing Docker containers and compose files" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.created="${BUILD_DATE}" \
      org.opencontainers.image.revision="${VCS_REF}" \
      org.opencontainers.image.vendor="Docker Compose Manager Contributors" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.url="https://github.com/yourusername/docker-compose-manager" \
      org.opencontainers.image.source="https://github.com/yourusername/docker-compose-manager" \
      org.opencontainers.image.documentation="https://github.com/yourusername/docker-compose-manager/wiki"

COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
