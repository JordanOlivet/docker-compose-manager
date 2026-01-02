# Build stage
FROM node:20-alpine AS build

# Accept build arguments for version info
ARG VITE_API_URL=/api
ARG VITE_APP_VERSION=dev
ARG VITE_BUILD_DATE
ARG VITE_GIT_COMMIT

WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm ci

# Copy source files
COPY . .

# Set environment variables for Vite build
ENV VITE_API_URL=$VITE_API_URL
ENV VITE_APP_VERSION=$VITE_APP_VERSION
ENV VITE_BUILD_DATE=$VITE_BUILD_DATE
ENV VITE_GIT_COMMIT=$VITE_GIT_COMMIT

# Build the application
RUN npm run build

# Production stage
FROM nginx:alpine

# Copy nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy built files from build stage
COPY --from=build /app/dist /usr/share/nginx/html

# Expose port
EXPOSE 80

# Start nginx
CMD ["nginx", "-g", "daemon off;"]


