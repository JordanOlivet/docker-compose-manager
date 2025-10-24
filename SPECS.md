# Docker Compose Manager - Technical Specifications

## Table of Contents

1. [Overview](#1-overview)
2. [Application Specifications](#2-application-specifications)
   - [Backend (docker-compose-manager-back)](#21-backend-docker-compose-manager-back)
   - [Frontend (docker-compose-manager-front)](#22-frontend-docker-compose-manager-front)
3. [Technical Stack](#3-technical-stack)
4. [Architecture & Deployment](#4-architecture--deployment)
5. [Development Setup](#5-development-setup)
6. [Security Considerations](#6-security-considerations)
7. [API Documentation](#7-api-documentation)
8. [Monitoring & Logging](#8-monitoring--logging)
9. [Implementation Notes & Clarifications](#9-implementation-notes--clarifications)

---

## 1. Overview

This repository hosts two applications: **docker-compose-manager-back** and **docker-compose-manager-front**.

These applications work together to provide Docker and Docker Compose management capabilities through a modern web interface. The stack is primarily designed to run in Docker containers, with both applications built into Docker images and orchestrated via a docker-compose.yml file.

### Key Features
- **Cross-platform**: Executable on Windows and Linux
- **Containerized deployment**: Production-ready Docker configuration
- **Local development**: Can be run directly on the developer's OS for testing and debugging
- **Complete Docker management**: Container operations, compose file editing, and project management
- **User management**: Role-based access control with admin and user roles
- **Real-time updates**: WebSocket support for live container status and logs

---

## 2. Application Specifications

### 2.1. Backend (docker-compose-manager-back)

**Technology**: .NET 9 API service in C#

**Purpose**: Interface between the Docker Engine on the host and the web application, exposing all functionalities necessary to manage Docker and Docker Compose files via REST API routes.

#### API Endpoints

##### Authentication & Authorization
- `POST /api/auth/login` - User authentication with username/password, returns JWT token
- `POST /api/auth/logout` - Invalidate user session
- `POST /api/auth/refresh` - Refresh JWT token
- `GET /api/auth/me` - Get current user profile information
- JWT-based authentication middleware for protected routes
- Role-based authorization middleware (admin, user roles)

##### User Management API (Admin Only)
- `GET /api/users` - List all users with pagination, filtering, and sorting
- `GET /api/users/{id}` - Get specific user details
- `POST /api/users` - Create new user with username, password, and role
- `PUT /api/users/{id}` - Update user information (role, status, password)
- `DELETE /api/users/{id}` - Delete user
- `PUT /api/users/{id}/enable` - Enable user account
- `PUT /api/users/{id}/disable` - Disable user account
- `GET /api/roles` - List available roles and permissions
- `POST /api/roles` - Create custom role with specific permissions
- `PUT /api/roles/{id}` - Update role permissions

##### User Profile API
- `GET /api/profile` - Get current user's profile
- `PUT /api/profile` - Update current user's profile
- `PUT /api/profile/password` - Change current user's password

##### Docker Container Management API
- `GET /api/containers` - List all containers with filters (status, name, image)
- `GET /api/containers/{id}` - Get detailed container information (env vars, mounts, networks, ports)
- `POST /api/containers/{id}/start` - Start a container
- `POST /api/containers/{id}/stop` - Stop a container
- `POST /api/containers/{id}/restart` - Restart a container
- `DELETE /api/containers/{id}` - Remove a container
- `GET /api/containers/{id}/logs` - Stream container logs (with tail, follow, timestamps options)
- `GET /api/containers/{id}/stats` - Get real-time container statistics (CPU, memory, network, I/O)
- `POST /api/containers/bulk-action` - Perform bulk actions on multiple containers

##### Docker Compose API
- `GET /api/compose/projects` - List all compose projects from configured directories
- `GET /api/compose/projects/{projectName}` - Get compose project details and service status
- `POST /api/compose/projects/{projectName}/up` - Execute docker-compose up (with options: detached, build, force-recreate)
- `POST /api/compose/projects/{projectName}/down` - Execute docker-compose down (with options: volumes, images)
- `POST /api/compose/projects/{projectName}/start` - Start compose services
- `POST /api/compose/projects/{projectName}/stop` - Stop compose services
- `POST /api/compose/projects/{projectName}/restart` - Restart compose services
- `GET /api/compose/projects/{projectName}/logs` - Stream compose project logs
- `GET /api/compose/projects/{projectName}/ps` - Get status of all services in compose project

##### Compose File Management API
- `GET /api/compose/files` - List all compose files in configured directories
- `GET /api/compose/files/{fileId}` - Read specific compose file content by ID
- `GET /api/compose/files/by-path?path={encodedPath}` - Read specific compose file by path (query param)
- `POST /api/compose/files` - Create new compose file
- `PUT /api/compose/files/{fileId}` - Update/edit compose file content by ID
- `DELETE /api/compose/files/{fileId}` - Delete compose file by ID
- `POST /api/compose/files/{fileId}/validate` - Validate YAML syntax and docker-compose structure
- `POST /api/compose/files/{fileId}/duplicate` - Duplicate/clone compose file
- `GET /api/compose/files/{fileId}/download` - Download compose file
- `GET /api/compose/templates` - Get available compose file templates (LAMP, MEAN, Postgres+Redis, Nginx+PHP, etc.)

##### Dashboard API
- `GET /api/dashboard/stats` - Get aggregated statistics (total containers, running, stopped, compose projects)
- `GET /api/dashboard/system` - Get system resource usage (CPU, memory, disk)
- `GET /api/dashboard/activity` - Get recent activity/events log
- `GET /api/dashboard/health` - Get health status of all services

##### Configuration API (Admin Only)
- `GET /api/config/paths` - Get list of configured compose file paths
- `POST /api/config/paths` - Add new compose file path
- `PUT /api/config/paths/{id}` - Update compose file path
- `DELETE /api/config/paths/{id}` - Remove compose file path
- `GET /api/config/settings` - Get application settings
- `PUT /api/config/settings` - Update application settings (compose defaults, refresh intervals, log retention, docker connection)
- `GET /api/config/security` - Get security settings (session timeout, password policies)
- `PUT /api/config/security` - Update security settings

##### Real-Time Updates
- WebSocket endpoint `/ws` for real-time container status updates
- WebSocket endpoint `/ws/logs` for real-time log streaming
- Server-Sent Events (SSE) as fallback for real-time updates

##### Activity & Audit Logging
- Log all user actions (container operations, file modifications, config changes)
- `GET /api/audit/logs` - Retrieve audit logs with filtering (admin only)

##### Security Features
- Input validation and sanitization for all endpoints
- Secure file path handling with whitelist validation to prevent directory traversal
- Rate limiting on authentication endpoints
- CORS configuration for frontend
- Password hashing using BCrypt
- SQL injection prevention (parameterized queries)

##### Cross-Platform Support
- Support for Windows Docker Desktop (named pipe: `npipe:////./pipe/docker_engine`)
- Support for Linux Docker daemon (unix socket: `unix:///var/run/docker.sock`)
- Automatic detection of platform and Docker connection method

##### Error Handling
- Standardized error response format with error codes
- Proper HTTP status codes for different scenarios
- Detailed error messages for debugging (development mode)
- User-friendly error messages (production mode)

#### Data Storage System

The backend uses **SQLite** as the primary database for storing application data.

##### Why SQLite?
- Simple deployment - single file database, no separate container required
- Lightweight and efficient for the application's data needs
- Perfect for moderate concurrency (typical usage: <50 simultaneous users)
- Zero configuration - works out-of-the-box on Windows and Linux
- Easy backup and restore (simple file copy)
- Low resource consumption
- ACID transactions for data integrity
- Excellent .NET support via Entity Framework Core

##### Database Schema
- **Users** - User accounts, credentials (hashed passwords), roles, status, created/updated timestamps
- **Roles** - Role definitions with associated permissions
- **ComposePaths** - Configured paths where compose files are stored
- **ComposeFiles** - Discovered compose files with ID, path, last modified timestamp (for path-to-ID mapping)
- **AppSettings** - Application configuration (key-value pairs)
- **AuditLogs** - User activity tracking (user, action, timestamp, details, IP address)
- **Sessions** - Active user sessions for JWT token management (refresh tokens)

##### ORM and Database Access
- Use **Entity Framework Core** for database access
- Repository pattern for abstraction and testability
- Database migrations for version control of schema changes
- Configurable connection string in appsettings.json

##### Migration Path
- Architecture designed to support migration to PostgreSQL if needed in the future
- Database type configurable via settings
- Example scenarios requiring PostgreSQL:
  - High concurrency (>50 simultaneous users)
  - Need for database replication or high availability
  - Very large audit log volumes requiring advanced query optimization
  - Requirement for multi-instance deployment with shared database

##### Database Location
- **Development**: Local file in project directory (`Data/app.db`)
- **Docker**: Mounted volume to persist data across container restarts
- **Configuration example**:
  ```json
  {
    "Database": {
      "Type": "SQLite",
      "ConnectionString": "Data Source=/app/data/app.db"
    }
  }
  ```

---

### 2.2. Frontend (docker-compose-manager-front)

**Technology**: React web application

**Purpose**: Provide a modern, user-friendly interface for managing Docker containers and compose files.

#### Features

##### Authentication and User Management
- Login/logout functionality with session management
- User profile page (view and edit own profile)
- **Admin-only user management interface:**
  - List all users with their roles and permissions
  - Create new users with username, password, and role assignment
  - Edit existing users (change role, enable/disable, reset password)
  - Delete users
  - Define user roles (admin, user) and custom permissions

##### Dashboard (Main Page)
- Overview of all containers with status (running, stopped, exited)
- System resource usage (CPU, memory, disk)
- Quick stats: total containers, running containers, stopped containers
- Quick stats: total compose projects, active projects
- Recent activity/logs feed
- Health status indicators for services
- Search and filter capabilities

##### Container Management
- List all containers with detailed information (name, image, status, ports, created date)
- Filter containers by status, name, or image
- Container actions: start, stop, restart, delete (with confirmation)
- View container details (environment variables, mounts, networks)
- View real-time container logs with auto-refresh option
- View container statistics (CPU, memory, network usage)
- Bulk actions on multiple containers

##### Docker Compose Management
- List all compose files from configured directories
- View compose file structure and services
- **Compose project actions:**
  - Compose up (with options: detached, build, force-recreate)
  - Compose down (with options: remove volumes, remove images)
  - Compose start/stop/restart
  - View compose project status and service health
- Real-time logs for compose services

##### Compose File Editor
- Browse and search compose files in configured paths
- Create new compose files with template options
- **Edit existing compose files with:**
  - Syntax highlighting for YAML
  - Real-time validation and error detection
  - Auto-completion for common docker-compose directives
  - Preview mode before saving
- Delete compose files (with confirmation)
- Duplicate/clone compose files
- Download compose files to local machine

##### Configuration (Admin Only)
- Manage paths where compose files are stored (add, edit, remove)
- **Configure application settings:**
  - Default compose command options
  - Auto-refresh intervals for dashboard
  - Log retention settings
  - Docker engine connection settings
- User role and permission configuration
- Security settings (session timeout, password policies)

##### UI/UX Features
- Responsive design for desktop and tablet
- Dark/light theme toggle
- Real-time updates using WebSockets or polling
- Notifications for long-running operations
- Confirmation dialogs for destructive actions
- Loading states and progress indicators
- Error handling with user-friendly messages
- Breadcrumb navigation
- Keyboard shortcuts for common actions

---

## 3. Technical Stack

### 3.1. Backend (docker-compose-manager-back)

| Component | Technology |
|-----------|------------|
| **Framework** | .NET 9 (ASP.NET Core Web API) |
| **Language** | C# 12 |
| **Database ORM** | Entity Framework Core 9 |
| **Database** | SQLite 3 |
| **Docker Client** | Docker.DotNet |
| **Authentication** | JWT tokens (System.IdentityModel.Tokens.Jwt)<br>BCrypt.Net for password hashing |
| **Validation** | FluentValidation |
| **API Documentation** | Swashbuckle (Swagger/OpenAPI) |
| **WebSocket** | ASP.NET Core SignalR |
| **Logging** | Serilog with structured logging (JSON format) |
| **Configuration** | Microsoft.Extensions.Configuration |
| **YAML Processing** | YamlDotNet |
| **Testing** | xUnit, Moq, TestContainers |

### 3.2. Frontend (docker-compose-manager-front)

| Component | Technology |
|-----------|------------|
| **Framework** | React 18 |
| **Language** | TypeScript 5 |
| **Build Tool** | Vite |
| **Routing** | React Router v6 |
| **State Management** | Zustand |
| **UI Library** | shadcn/ui + Tailwind CSS |
| **Components** | Radix UI primitives<br>Lucide React (icons) |
| **Forms** | React Hook Form + Zod validation |
| **API Client** | Axios + TanStack Query (React Query) |
| **WebSocket** | Socket.IO Client |
| **Code Editor** | Monaco Editor (VS Code editor) |
| **YAML Processing** | js-yaml |
| **Date/Time** | date-fns |
| **Notifications** | React Hot Toast |
| **Testing** | Vitest, React Testing Library, Playwright |

### 3.3. DevOps & Deployment

| Component | Technology |
|-----------|------------|
| **Containerization** | Docker & Docker Compose |
| **Base Images** | Backend: `mcr.microsoft.com/dotnet/aspnet:9.0`<br>Frontend: `node:20-alpine` (build) + `nginx:alpine` (serve) |
| **Reverse Proxy** | Nginx |
| **CI/CD** | GitHub Actions (optional) |
| **Version Control** | Git |

---

## 4. Architecture & Deployment

### 4.1. Project Structure

```
docker-compose-manager/
├── docker-compose-manager-back/
│   ├── src/
│   │   ├── Controllers/
│   │   ├── Services/
│   │   ├── Repositories/
│   │   ├── Models/
│   │   ├── DTOs/
│   │   ├── Middleware/
│   │   ├── Data/
│   │   │   └── AppDbContext.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   └── Program.cs
│   ├── tests/
│   ├── Dockerfile
│   ├── .dockerignore
│   └── docker-compose-manager-back.csproj
├── docker-compose-manager-front/
│   ├── src/
│   │   ├── components/
│   │   ├── pages/
│   │   ├── hooks/
│   │   ├── services/
│   │   ├── store/
│   │   ├── types/
│   │   ├── utils/
│   │   ├── App.tsx
│   │   └── main.tsx
│   ├── public/
│   ├── nginx.conf
│   ├── Dockerfile
│   ├── .dockerignore
│   ├── package.json
│   ├── tsconfig.json
│   └── vite.config.ts
├── docker-compose.yml
├── .env.example
├── README.md
└── SPECS.md
```

### 4.2. Docker Configuration

#### Backend Dockerfile

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["docker-compose-manager-back.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

# Create data directory for SQLite
RUN mkdir -p /app/data

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "docker-compose-manager-back.dll"]
```

#### Frontend Dockerfile

```dockerfile
# Build stage
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# Production stage
FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

#### Frontend nginx.conf

```nginx
server {
    listen 80;
    server_name localhost;
    root /usr/share/nginx/html;
    index index.html;

    # Frontend routes (SPA)
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Proxy API requests to backend
    location /api/ {
        proxy_pass http://backend:5000/api/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # WebSocket proxy
    location /ws/ {
        proxy_pass http://backend:5000/ws/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "Upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

#### docker-compose.yml

```yaml
version: '3.8'

services:
  backend:
    build:
      context: ./docker-compose-manager-back
      dockerfile: Dockerfile
    container_name: docker-manager-backend
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Database__ConnectionString=Data Source=/app/data/app.db
      - Jwt__Secret=${JWT_SECRET}
      - Jwt__ExpirationMinutes=60
      - Jwt__RefreshExpirationDays=7
      - Docker__Host=${DOCKER_HOST:-unix:///var/run/docker.sock}
      - Cors__Origins=http://localhost:3000
    volumes:
      # Database persistence
      - backend-data:/app/data
      # Docker socket access (CRITICAL)
      # Linux:
      - /var/run/docker.sock:/var/run/docker.sock
      # Windows (uncomment and comment line above):
      # - //./pipe/docker_engine://./pipe/docker_engine
      # Compose files to manage
      - ${COMPOSE_FILES_PATH:-./compose-files}:/compose-files:ro
    networks:
      - docker-manager-network
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  frontend:
    build:
      context: ./docker-compose-manager-front
      dockerfile: Dockerfile
    container_name: docker-manager-frontend
    ports:
      - "3000:80"
    depends_on:
      - backend
    networks:
      - docker-manager-network
    restart: unless-stopped

volumes:
  backend-data:
    driver: local

networks:
  docker-manager-network:
    driver: bridge
```

#### .env.example

```bash
# JWT Configuration
JWT_SECRET=your-super-secret-jwt-key-change-this-in-production

# Docker Configuration
# Linux:
DOCKER_HOST=unix:///var/run/docker.sock
# Windows:
# DOCKER_HOST=npipe:////./pipe/docker_engine

# Compose Files Path
# Path on host where compose files to manage are located
COMPOSE_FILES_PATH=./compose-files

# Database (optional override)
# DATABASE_PATH=/app/data/app.db
```

### 4.3. Deployment Instructions

#### Docker Deployment (Production)

1. Clone the repository
2. Copy `.env.example` to `.env` and configure values
3. Ensure Docker is running on the host
4. Run: `docker compose up -d`
5. Access the application at `http://localhost:3000`
6. Default admin credentials: `admin` / `admin` (change immediately)

#### Important Security Notes

- **Docker Socket Access**: The backend container has access to the host Docker daemon via socket mounting. This gives the container **root-level access** to the host system.
- **Production Deployment**: Consider using Docker contexts or Docker API over TCP with TLS for remote management instead of socket mounting.
- **Compose Files Path**: Mount the directory containing compose files you want to manage. Use `:ro` (read-only) if you only want viewing capabilities.

### 4.4. Network Architecture

```
Internet/User
    ↓
[Port 3000] → Frontend Container (Nginx)
    ↓ (internal network)
    ├─→ /api/* → Backend Container :5000 (ASP.NET Core)
    │              ↓
    │              ├─→ SQLite DB (volume: backend-data)
    │              ├─→ Docker Socket (host)
    │              └─→ Compose Files (host volume)
    │
    └─→ /ws/* → Backend WebSocket :5000
```

---

## 5. Development Setup

### 5.1. Prerequisites

- **Docker Desktop** (Windows/Mac) or **Docker Engine** (Linux)
- **.NET 9 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Node.js 20+** and **npm** - [Download](https://nodejs.org/)
- **Git**
- **IDE/Editor:**
  - Visual Studio 2022 or VS Code (for backend)
  - VS Code (for frontend)

### 5.2. Local Development (Without Docker)

This setup allows you to run both applications directly on your development machine with hot-reload capabilities.

#### Backend Setup

1. **Navigate to backend directory:**
   ```bash
   cd docker-compose-manager-back
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Configure appsettings.Development.json:**
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "Microsoft.AspNetCore": "Warning"
       }
     },
     "Database": {
       "ConnectionString": "Data Source=../Data/app.db"
     },
     "Jwt": {
       "Secret": "your-dev-secret-key-at-least-32-characters-long",
       "ExpirationMinutes": 60,
       "RefreshExpirationDays": 7
     },
     "Docker": {
       "Host": "unix:///var/run/docker.sock"
     },
     "Cors": {
       "Origins": ["http://localhost:5173"]
     }
   }
   ```

4. **Apply database migrations:**
   ```bash
   dotnet ef database update
   ```

5. **Run the backend:**
   ```bash
   dotnet run
   ```

   Or with watch mode (hot-reload):
   ```bash
   dotnet watch run
   ```

   - Backend will be available at `http://localhost:5000`
   - Swagger UI at `http://localhost:5000/swagger`

#### Frontend Setup

1. **Navigate to frontend directory:**
   ```bash
   cd docker-compose-manager-front
   ```

2. **Install dependencies:**
   ```bash
   npm install
   ```

3. **Configure environment (.env.development):**
   ```bash
   VITE_API_URL=http://localhost:5000
   VITE_WS_URL=ws://localhost:5000
   ```

4. **Run the development server:**
   ```bash
   npm run dev
   ```

   - Frontend will be available at `http://localhost:5173` (Vite default)

#### Development Workflow

1. Start backend with `dotnet watch run` (auto-reloads on code changes)
2. Start frontend with `npm run dev` (hot module replacement)
3. Access frontend at `http://localhost:5173`
4. API calls go directly to `http://localhost:5000`
5. Use Swagger at `http://localhost:5000/swagger` for API testing

### 5.3. Initial Database Seeding

On first run, the backend should automatically:
1. Create the SQLite database
2. Apply all migrations
3. Seed initial data:
   - Default admin user: `admin` / `admin`
   - Default user roles (admin, user)
   - Default compose file path: `/compose-files`

This is handled in `Program.cs`:
```csharp
// Auto-apply migrations and seed data
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
    await SeedData.Initialize(scope.ServiceProvider);
}
```

### 5.4. Database Migrations

Create a new migration when you change the database schema:

```bash
# Add migration
dotnet ef migrations add MigrationName

# Apply to database
dotnet ef database update

# Remove last migration (if not applied)
dotnet ef migrations remove
```

### 5.5. Running Tests

#### Backend Tests
```bash
cd docker-compose-manager-back/tests
dotnet test
```

#### Frontend Tests
```bash
cd docker-compose-manager-front

# Unit tests
npm run test

# E2E tests
npm run test:e2e

# Test coverage
npm run test:coverage
```

### 5.6. Development with Docker Compose

For a production-like environment during development:

```bash
# Build and start all services
docker compose up --build

# Run in background
docker compose up -d

# View logs
docker compose logs -f

# Stop services
docker compose down

# Stop and remove volumes (fresh start)
docker compose down -v
```

### 5.7. Troubleshooting

#### Backend cannot connect to Docker daemon
- **Linux:** Ensure Docker socket permissions: `sudo chmod 666 /var/run/docker.sock`
- **Windows:** Ensure Docker Desktop is running and "Expose daemon on tcp://localhost:2375 without TLS" is enabled (for development only)

#### CORS errors in frontend
- Ensure backend `Cors:Origins` includes the frontend URL
- In development: `http://localhost:5173` (Vite default port)

#### Database locked errors
- SQLite doesn't handle high concurrency well
- Ensure only one process accesses the DB
- Close any DB browser tools (DB Browser for SQLite)

#### Hot reload not working
- **Backend:** Ensure `dotnet watch run` is used, not `dotnet run`
- **Frontend:** Ensure Vite dev server is running, not production build

### 5.8. Recommended VS Code Extensions

#### Backend
- C# Dev Kit
- .NET Extension Pack
- REST Client (for API testing)

#### Frontend
- ESLint
- Prettier
- Tailwind CSS IntelliSense
- TypeScript Vue Plugin (Volar)
- Error Lens

---

## 6. Security Considerations

### 6.1. Critical Security Risks

#### Docker Socket Access

The backend container requires access to the host Docker daemon, which presents significant security implications:

- **Root-level access**: Any process with Docker socket access effectively has root access to the host system
- **Container escape**: Malicious code could potentially escape the container and compromise the host
- **Risk mitigation:**
  - Run the application in a trusted, isolated network
  - Consider using Docker API over TCP with TLS instead of socket mounting
  - Implement strict input validation for all Docker commands
  - Use read-only volume mounts where possible for compose files
  - Regular security audits of the codebase

### 6.2. Authentication & Authorization

#### JWT Token Strategy

**Access Tokens:**
- Short-lived (60 minutes default)
- Stored in memory (frontend) or httpOnly cookie
- Used for API authentication
- Stateless (no DB lookup required)

**Refresh Tokens:**
- Long-lived (7 days default)
- Stored in database (Sessions table)
- Used to obtain new access tokens
- Can be revoked (logout, password change)
- One refresh token per user per device/session

**Token Flow:**
```
1. User logs in → receives access token + refresh token
2. Access token expires after 60 minutes
3. Frontend uses refresh token to get new access token
4. Refresh token expires after 7 days → user must re-login
5. Logout → refresh token is deleted from database
```

#### Password Security
- **Hashing:** BCrypt with salt rounds (cost factor 12)
- **Requirements:** Minimum 8 characters, configurable complexity rules
- **Password reset:** Token-based reset flow (if email is implemented)
- **Password change:** Requires current password verification

#### Session Management
- **Concurrent sessions:** Multiple sessions per user allowed (different devices)
- **Session tracking:** Store in Sessions table with device info, IP, last active
- **Session invalidation:**
  - Manual logout
  - Password change → all sessions invalidated
  - Refresh token expiration
  - Admin can force logout specific users

### 6.3. API Security

#### Rate Limiting

Protect against brute force and DoS attacks:
- **Authentication endpoints:**
  - `/api/auth/login`: 5 attempts per 15 minutes per IP
  - `/api/auth/refresh`: 10 attempts per 15 minutes per IP
- **General API:** 100 requests per minute per user
- Implementation: ASP.NET Core rate limiting middleware

#### CORS Configuration
- **Development:** Allow `http://localhost:5173` (Vite dev server)
- **Production:** Only allow the frontend domain
- **Credentials:** Allow credentials for cookie-based auth
- **Preflight caching:** 24 hours

#### Input Validation
- **All endpoints:** Validate all input using FluentValidation
- **File paths:** Whitelist-based validation to prevent directory traversal
  ```csharp
  // Only allow paths within configured compose directories
  var allowedPaths = configService.GetComposePaths();
  if (!IsPathWithinAllowedDirectories(requestedPath, allowedPaths))
      throw new SecurityException("Access denied");
  ```
- **Docker commands:** Sanitize all user input before passing to Docker API
- **YAML content:** Validate structure before saving compose files

#### SQL Injection Prevention
- **Entity Framework Core:** Uses parameterized queries by default
- **Raw SQL:** Avoid raw SQL queries; if necessary, use parameterized queries

### 6.4. WebSocket Security

#### Authentication

WebSocket connections must be authenticated:

```javascript
// Frontend: Include JWT in connection
const socket = io('ws://localhost:5000', {
  auth: { token: accessToken }
});
```

```csharp
// Backend: Validate JWT on connection
public override async Task OnConnectedAsync()
{
    var token = Context.GetHttpContext()?.Request.Query["access_token"];
    var principal = ValidateToken(token);
    if (principal == null)
    {
        Context.Abort();
        return;
    }
    await base.OnConnectedAsync();
}
```

### 6.5. File System Security

#### Compose File Management
- **Path traversal prevention:** Validate all file paths against whitelist
- **Read-only access:** Mount compose directories as read-only if edit capability is not needed
- **File size limits:** Enforce maximum file size (e.g., 10MB per compose file)
- **Content validation:** Validate YAML structure before saving

#### Database File
- **Location:** Store outside web root
- **Permissions:** Restrict file permissions (600 on Linux)
- **Backup:** Regular automated backups to separate location

### 6.6. Audit Logging

All security-relevant actions are logged to the AuditLogs table:
- User login/logout (with IP address)
- Failed login attempts
- Password changes
- User management actions (create, delete, role changes)
- Container operations (start, stop, delete)
- Compose file modifications
- Configuration changes

**Log Retention:**
- Default: 90 days
- Configurable via admin settings
- Automatic cleanup of old logs

### 6.7. Production Deployment Recommendations

1. **Use HTTPS:** Deploy behind reverse proxy with TLS (Nginx, Traefik)
2. **Environment variables:** Never commit secrets to repository
3. **JWT secret:** Generate strong random secret (at least 256 bits)
4. **Change default credentials:** Force admin password change on first login
5. **Regular updates:** Keep dependencies updated for security patches
6. **Network isolation:** Run in isolated Docker network, expose only necessary ports
7. **Monitoring:** Set up alerts for suspicious activity (multiple failed logins, etc.)
8. **Backup strategy:** Regular database backups with encryption
9. **Docker socket alternative:** Consider Docker API over TCP with TLS for remote deployments
10. **Principle of least privilege:** Create dedicated Docker group/user with minimal permissions

### 6.8. Security Headers

The backend should set security headers:
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add("Content-Security-Policy", "default-src 'self'");
    await next();
});
```

### 6.9. Vulnerability Disclosure

- Report security issues privately to repository maintainers
- Do not publicly disclose vulnerabilities until patched
- Security patches released with high priority

---

## 7. API Documentation

### 7.1. Swagger/OpenAPI

The backend API is fully documented using Swagger/OpenAPI specification.

#### Accessing Swagger UI

- **Development:** `http://localhost:5000/swagger`
- **Production:** Disable Swagger in production or protect with authentication

#### Configuration

In `Program.cs`:
```csharp
// Add Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Docker Compose Manager API v1");
        c.RoutePrefix = "swagger";
    });
}
```

#### Swagger Annotations

All controllers and endpoints should be annotated:
```csharp
/// <summary>
/// Manages Docker containers
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContainersController : ControllerBase
{
    /// <summary>
    /// Get all containers
    /// </summary>
    /// <param name="status">Filter by status (running, stopped, all)</param>
    /// <returns>List of containers</returns>
    /// <response code="200">Returns the list of containers</response>
    /// <response code="401">Unauthorized - invalid or missing JWT token</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<ContainerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<ContainerDto>>> GetContainers(
        [FromQuery] string? status = "all")
    {
        // Implementation
    }
}
```

### 7.2. API Response Format

#### Success Response
```json
{
  "data": { ... },
  "success": true,
  "message": "Operation completed successfully"
}
```

#### Error Response
```json
{
  "success": false,
  "message": "Error description",
  "errors": {
    "field": ["Validation error message"]
  },
  "errorCode": "ERROR_CODE",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

#### Pagination Response
```json
{
  "data": [ ... ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalPages": 5,
    "totalItems": 100,
    "hasNext": true,
    "hasPrevious": false
  },
  "success": true
}
```

### 7.3. Error Codes

| Code | Description |
|------|-------------|
| `AUTH_INVALID_CREDENTIALS` | Invalid username or password |
| `AUTH_TOKEN_EXPIRED` | JWT token has expired |
| `AUTH_TOKEN_INVALID` | JWT token is malformed or invalid |
| `AUTH_INSUFFICIENT_PERMISSIONS` | User lacks required permissions |
| `VALIDATION_ERROR` | Input validation failed |
| `RESOURCE_NOT_FOUND` | Requested resource not found |
| `DOCKER_CONNECTION_ERROR` | Cannot connect to Docker daemon |
| `DOCKER_OPERATION_FAILED` | Docker operation failed |
| `FILE_NOT_FOUND` | Compose file not found |
| `FILE_INVALID` | Compose file syntax invalid |
| `PATH_TRAVERSAL_DETECTED` | Attempted directory traversal |
| `RATE_LIMIT_EXCEEDED` | Too many requests |
| `INTERNAL_SERVER_ERROR` | Unexpected server error |

### 7.4. Common HTTP Status Codes

- **200 OK:** Request succeeded
- **201 Created:** Resource created successfully
- **204 No Content:** Request succeeded with no response body
- **400 Bad Request:** Invalid input or validation error
- **401 Unauthorized:** Missing or invalid authentication
- **403 Forbidden:** Authenticated but insufficient permissions
- **404 Not Found:** Resource not found
- **409 Conflict:** Resource conflict (e.g., username already exists)
- **422 Unprocessable Entity:** Semantic validation error
- **429 Too Many Requests:** Rate limit exceeded
- **500 Internal Server Error:** Unexpected server error
- **503 Service Unavailable:** Docker daemon unavailable

### 7.5. Authentication in Swagger

To test authenticated endpoints in Swagger:
1. Call `/api/auth/login` endpoint
2. Copy the `accessToken` from response
3. Click "Authorize" button in Swagger UI
4. Enter: `Bearer {accessToken}`
5. All subsequent requests will include the token

### 7.6. API Versioning

Future versioning strategy:
- URL versioning: `/api/v2/containers`
- Header versioning: `X-API-Version: 2.0`
- Currently: v1 (no version in URL)

---

## 8. Monitoring & Logging

### 8.1. Application Logging

#### Structured Logging with Serilog

The backend uses Serilog for structured, machine-readable logging in JSON format.

**Configuration in appsettings.json:**
```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "formatter": "Serilog.Formatting.Json.JsonFormatter"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "/app/logs/app-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "formatter": "Serilog.Formatting.Json.JsonFormatter"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

#### Log Levels

- **Verbose/Trace:** Detailed diagnostic information (rarely used)
- **Debug:** Internal system events for debugging
- **Information:** General application flow (startup, shutdown, requests)
- **Warning:** Unexpected events that don't prevent operation
- **Error:** Errors and exceptions that affect specific operations
- **Fatal:** Critical errors that crash the application

#### Logging Best Practices

```csharp
// Log with structured data
_logger.LogInformation(
    "Container {ContainerId} started by user {UserId}",
    containerId,
    userId
);

// Log exceptions
try
{
    await dockerService.StartContainer(id);
}
catch (Exception ex)
{
    _logger.LogError(ex,
        "Failed to start container {ContainerId}",
        id
    );
    throw;
}

// Log performance
using (_logger.BeginTimedOperation("Docker Compose Up"))
{
    await composeService.ComposeUp(projectName);
}
```

#### Log Output Example
```json
{
  "@t": "2024-01-15T10:30:00.1234567Z",
  "@l": "Information",
  "@mt": "Container {ContainerId} started by user {UserId}",
  "ContainerId": "abc123",
  "UserId": "42",
  "SourceContext": "ContainerService",
  "MachineName": "docker-manager-backend",
  "ThreadId": 12
}
```

### 8.2. Health Checks

#### Backend Health Endpoint

**GET /health**

Returns application health status:
```json
{
  "status": "Healthy",
  "checks": {
    "database": {
      "status": "Healthy",
      "description": "SQLite database is accessible"
    },
    "docker": {
      "status": "Healthy",
      "description": "Docker daemon is reachable"
    }
  },
  "totalDuration": "00:00:00.1234567"
}
```

**Implementation:**
```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database")
    .AddCheck<DockerHealthCheck>("docker");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

#### Docker Healthcheck (in Dockerfile)
```dockerfile
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1
```

### 8.3. Metrics & Monitoring

#### Key Metrics to Monitor

1. **Application Metrics:**
   - Request count and duration (per endpoint)
   - Error rate (4xx, 5xx responses)
   - Active WebSocket connections
   - Queue depth for long-running operations

2. **Docker Metrics:**
   - Number of containers (total, running, stopped)
   - Container start/stop operations per minute
   - Docker API response times

3. **Database Metrics:**
   - Query execution time
   - Database file size
   - Connection pool usage

4. **System Metrics:**
   - CPU usage (container and host)
   - Memory usage (container and host)
   - Disk I/O
   - Network I/O

#### Prometheus Integration (Optional)

For production monitoring, integrate Prometheus:

```csharp
// Install: prometheus-net.AspNetCore
builder.Services.AddSingleton<IMetricsCollector, PrometheusMetricsCollector>();

app.UseHttpMetrics(); // Collect HTTP metrics
app.MapMetrics();     // Expose /metrics endpoint
```

**Prometheus scrape config:**
```yaml
scrape_configs:
  - job_name: 'docker-compose-manager'
    static_configs:
      - targets: ['backend:5000']
    metrics_path: '/metrics'
```

### 8.4. Alerting

#### Recommended Alerts

1. **Critical:**
   - Docker daemon unreachable for > 5 minutes
   - Backend health check failing
   - Database corruption detected
   - Disk space < 10%

2. **Warning:**
   - Error rate > 5% over 5 minutes
   - Response time > 2 seconds (p95)
   - Multiple failed login attempts from same IP
   - Memory usage > 80%

3. **Info:**
   - New user created
   - Configuration changed
   - Container deleted

### 8.5. Log Aggregation (Production)

For production deployments, consider log aggregation:

#### Option 1: ELK Stack (Elasticsearch, Logstash, Kibana)
```yaml
# docker-compose.yml addition
  logstash:
    image: docker.elastic.co/logstash/logstash:8.11.0
    volumes:
      - ./logstash.conf:/usr/share/logstash/pipeline/logstash.conf
      - backend-logs:/logs

  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:8.11.0

  kibana:
    image: docker.elastic.co/kibana/kibana:8.11.0
    ports:
      - "5601:5601"
```

#### Option 2: Loki + Grafana

Lightweight alternative to ELK:
```yaml
  loki:
    image: grafana/loki:latest
    ports:
      - "3100:3100"

  grafana:
    image: grafana/grafana:latest
    ports:
      - "3001:3000"
```

### 8.6. Audit Trail

All security-relevant actions are logged to the `AuditLogs` table:

| Field | Description |
|-------|-------------|
| Id | Unique identifier |
| UserId | User who performed the action |
| Action | Action performed (enum) |
| EntityType | Type of entity affected |
| EntityId | ID of entity affected |
| Changes | JSON object with before/after state |
| IpAddress | IP address of user |
| UserAgent | Browser/client info |
| Timestamp | When action occurred |
| Result | Success or failure |

**Audit Log Actions:**
- `UserLogin`, `UserLogout`, `UserCreated`, `UserUpdated`, `UserDeleted`
- `ContainerStarted`, `ContainerStopped`, `ContainerDeleted`
- `ComposeFileCreated`, `ComposeFileUpdated`, `ComposeFileDeleted`
- `ComposeProjectUp`, `ComposeProjectDown`
- `ConfigurationChanged`, `RoleChanged`

**Querying Audit Logs:**
```http
GET /api/audit/logs?userId=42&action=ContainerDeleted&from=2024-01-01&to=2024-01-31
```

### 8.7. Debugging Tips

#### View Backend Logs (Docker)
```bash
# Follow logs
docker logs -f docker-manager-backend

# Last 100 lines
docker logs --tail 100 docker-manager-backend

# Since 1 hour ago
docker logs --since 1h docker-manager-backend
```

#### View Structured Logs (Development)
```bash
# Navigate to logs directory
cd docker-compose-manager-back/logs

# Parse JSON logs with jq
cat app-20240115.log | jq 'select(.UserId == "42")'
cat app-20240115.log | jq 'select(."@l" == "Error")'
```

#### Enable Debug Logging

Set environment variable:
```bash
Serilog__MinimumLevel__Default=Debug
```

Or in appsettings.Development.json:
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"
    }
  }
}
```

---

## 9. Implementation Notes & Clarifications

### 9.1. Compose File Path Handling

#### Problem

File paths contain slashes which conflict with URL routing (e.g., `/home/user/docker/app.yml`)

#### Solution

Use numeric IDs for API operations instead of file paths:

1. **Database schema includes ComposeFiles table:**
   ```csharp
   public class ComposeFile
   {
       public int Id { get; set; }
       public string FileName { get; set; }
       public string FullPath { get; set; }
       public int ComposePathId { get; set; } // FK to ComposePaths
       public DateTime LastModified { get; set; }
   }
   ```

2. **API usage:**
   ```http
   GET /api/compose/files
   Returns: [
     { "id": 1, "fileName": "app.yml", "fullPath": "/compose-files/app.yml" },
     { "id": 2, "fileName": "db.yml", "fullPath": "/compose-files/db.yml" }
   ]

   GET /api/compose/files/1
   Returns file content for ID 1

   # Alternative: query param for direct path access
   GET /api/compose/files/by-path?path=%2Fcompose-files%2Fapp.yml
   ```

3. **File discovery on startup:**
   - Scan configured directories on application start
   - Populate ComposeFiles table with discovered files
   - Periodically rescan (configurable interval, default 5 minutes)
   - WebSocket notification when files change

### 9.2. Compose Projects vs Files

#### Definitions

**Compose File:**
- Single YAML file (e.g., `docker-compose.yml`)
- Stored in filesystem
- Can be edited, created, deleted via API

**Compose Project:**
- Logical grouping of services defined in compose file(s)
- Can include multiple files:
  - `docker-compose.yml` (base)
  - `docker-compose.override.yml` (local overrides)
  - `docker-compose.prod.yml` (environment-specific)
- Identified by project name (directory name or `-p` flag)
- Has runtime state (services running/stopped)

#### Implementation

1. **Project detection:**
   ```
   /compose-files/
   ├── myapp/
   │   ├── docker-compose.yml          → Project: "myapp"
   │   └── docker-compose.override.yml → Merged with above
   ├── webapp/
   │   └── docker-compose.yml          → Project: "webapp"
   └── standalone.yml                  → Project: "standalone"
   ```

2. **API behavior:**
   ```http
   GET /api/compose/projects
   Returns: [
     {
       "name": "myapp",
       "files": ["docker-compose.yml", "docker-compose.override.yml"],
       "status": "running",
       "services": [...]
     },
     {
       "name": "webapp",
       "files": ["docker-compose.yml"],
       "status": "stopped",
       "services": [...]
     }
   ]

   POST /api/compose/projects/myapp/up
   Executes: docker-compose -f docker-compose.yml -f docker-compose.override.yml up -d
   ```

3. **File association:**
   - Automatically detect related files (same directory, naming convention)
   - Allow manual association via UI
   - Store relationships in ComposeFiles table

### 9.3. Compose File Templates

Pre-defined templates for common stacks, stored in backend resources:

#### Available Templates

1. **LAMP Stack** (Linux, Apache, MySQL, PHP)
   ```yaml
   services:
     web:
       image: php:8.2-apache
       ports: ["80:80"]
       volumes: ["./www:/var/www/html"]
     db:
       image: mysql:8.0
       environment:
         MYSQL_ROOT_PASSWORD: example
   ```

2. **MEAN Stack** (MongoDB, Express, Angular, Node.js)
3. **PostgreSQL + Redis**
4. **Nginx + PHP-FPM**
5. **WordPress**
6. **Nextcloud**
7. **GitLab CE**
8. **Traefik Reverse Proxy**
9. **Prometheus + Grafana**
10. **ELK Stack** (Elasticsearch, Logstash, Kibana)

#### Template Management

- Templates stored in `backend/Resources/Templates/` directory
- Loaded on application start
- `GET /api/compose/templates` returns list of templates
- `GET /api/compose/templates/{templateId}` returns template content
- `POST /api/compose/files` can specify `templateId` to create from template
- Admin can upload custom templates via UI (stored in database)

### 9.4. Long-Running Operations

Some operations take significant time (e.g., `docker-compose up` with image builds):

#### Implementation Strategy

1. **Async operation pattern:**
   ```http
   POST /api/compose/projects/myapp/up
   Returns immediately: {
     "operationId": "op-abc123",
     "status": "pending",
     "message": "Operation started"
   }

   GET /api/operations/op-abc123
   Returns: {
     "operationId": "op-abc123",
     "status": "running",
     "progress": 45,
     "logs": "Pulling image...\nBuilding service web...",
     "startedAt": "2024-01-15T10:30:00Z"
   }
   ```

2. **WebSocket updates:**
   ```javascript
   // Frontend subscribes to operation updates
   socket.on('operation:update', (data) => {
     console.log(`Operation ${data.operationId}: ${data.status}`);
   });
   ```

3. **Operation timeout:**
   - Default: 10 minutes for compose up/down
   - Configurable per operation type
   - User can cancel in-progress operations

### 9.5. Initial Setup & Seeding

On first application start:

1. **Database initialization:**
   - Create database file if not exists
   - Run all migrations
   - Seed default data

2. **Default data seeded:**
   ```csharp
   // Default admin user
   new User
   {
       Username = "admin",
       PasswordHash = BCrypt.HashPassword("admin"),
       Role = "admin",
       MustChangePassword = true // Force change on first login
   }

   // Default roles
   new Role { Name = "admin", Permissions = [...all] }
   new Role { Name = "user", Permissions = [...limited] }

   // Default compose path
   new ComposePath
   {
       Path = "/compose-files",
       IsReadOnly = false,
       IsEnabled = true
   }
   ```

3. **First-run experience (Frontend):**
   - Login screen with note about default credentials
   - Force password change modal after first login
   - Setup wizard (optional):
     - Configure compose file paths
     - Test Docker connection
     - Create additional users

### 9.6. Environment Variable Override

All configuration can be overridden via environment variables:

```bash
# Database
Database__ConnectionString="Data Source=/custom/path/app.db"

# JWT
Jwt__Secret="your-secret-key"
Jwt__ExpirationMinutes=60
Jwt__RefreshExpirationDays=7

# Docker
Docker__Host="unix:///var/run/docker.sock"
Docker__Timeout=60000

# CORS
Cors__Origins__0="http://localhost:5173"
Cors__Origins__1="http://example.com"

# Logging
Serilog__MinimumLevel__Default="Information"
Serilog__WriteTo__0__Args__path="/custom/logs/app-.log"

# Compose files
ComposePaths__0__Path="/path1"
ComposePaths__0__IsReadOnly=false
ComposePaths__1__Path="/path2"
ComposePaths__1__IsReadOnly=true
```

### 9.7. Performance Considerations

1. **Caching:**
   - Cache container list for 5 seconds (configurable)
   - Cache compose file list for 30 seconds
   - Use ETags for compose file content
   - Invalidate cache on mutations

2. **Pagination:**
   - Default page size: 20 items
   - Maximum page size: 100 items
   - Cursor-based pagination for large datasets

3. **Connection pooling:**
   - SQLite: Single connection (file lock limitation)
   - Docker API: Connection pool of 10 (configurable)

4. **Rate limiting:**
   - Per-user limits stored in memory cache
   - Sliding window algorithm
   - Redis optional for multi-instance deployments
