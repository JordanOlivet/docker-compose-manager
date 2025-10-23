# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This repository contains a Docker Compose management system with two main applications:

- **docker-compose-manager-back**: .NET 9 Web API backend that interfaces with Docker Engine
- **docker-compose-manager-front**: React 18 + TypeScript frontend with shadcn/ui

The system provides a web-based interface for managing Docker containers and compose files, with features including user authentication, role-based access control, real-time updates via WebSockets, and a compose file editor.

## Development Commands

### Backend (docker-compose-manager-back)

Located in `./docker-compose-manager-back/`

```bash
# Restore dependencies
dotnet restore

# Run in development mode with hot-reload
dotnet watch run

# Run without hot-reload
dotnet run

# Build the project
dotnet build -c Release

# Run tests
dotnet test

# Create database migration
dotnet ef migrations add MigrationName

# Apply database migrations
dotnet ef database update

# Remove last migration (if not applied)
dotnet ef migrations remove
```

Backend runs at `http://localhost:5000` with Swagger UI at `http://localhost:5000/swagger`

### Frontend (docker-compose-manager-front)

Located in `./docker-compose-manager-front/`

```bash
# Install dependencies
npm install

# Run development server (with hot-reload)
npm run dev

# Build for production
npm run build

# Preview production build
npm run preview

# Run unit tests
npm run test

# Run E2E tests
npm run test:e2e

# Run tests with coverage
npm run test:coverage

# Lint code
npm run lint
```

Frontend runs at `http://localhost:5173` (Vite default)

### Docker Deployment

From repository root:

```bash
# Build and start all services
docker compose up --build

# Run in background
docker compose up -d

# View logs
docker compose logs -f

# View backend logs only
docker compose logs -f backend

# Stop services
docker compose down

# Stop and remove volumes (fresh start)
docker compose down -v

# Rebuild specific service
docker compose build backend
```

Application accessible at `http://localhost:3000` when running via Docker Compose.

## Architecture Overview

### Backend Architecture

The backend follows a layered architecture:

```
Controllers/ → API endpoints, HTTP request/response handling
Services/ → Business logic and Docker operations
Repositories/ → Data access layer, database operations
Models/ → Domain entities
DTOs/ → Data transfer objects for API
Middleware/ → Authentication, authorization, error handling, logging
Data/ → Entity Framework Core context and migrations
```

**Key Components:**

- **AppDbContext**: EF Core database context managing all entities
- **DockerService**: Interfaces with Docker daemon using Docker.DotNet
- **ComposeService**: Handles docker-compose operations (up, down, logs)
- **JwtTokenService**: Generates and validates JWT tokens
- **FileService**: Manages compose file CRUD with path validation
- **AuditService**: Logs all user actions to AuditLogs table

**Authentication Flow:**

1. User logs in → receives short-lived access token (60 min) + long-lived refresh token (7 days)
2. Access token stored in memory, refresh token in Sessions table
3. Access token expires → frontend uses refresh token to get new access token
4. Logout or password change → refresh token deleted from database

**Database (SQLite):**

- Schema includes: Users, Roles, ComposePaths, ComposeFiles, AppSettings, AuditLogs, Sessions
- Migrations managed via Entity Framework Core
- Default location: `Data/app.db` (development) or `/app/data/app.db` (Docker)
- Architecture supports future migration to PostgreSQL if needed

**Docker Integration:**

- Connects to Docker daemon via socket (Linux: `unix:///var/run/docker.sock`, Windows: `npipe:////./pipe/docker_engine`)
- Must have access to Docker socket to function - this grants root-level access to host
- Uses Docker.DotNet library for API communication

### Frontend Architecture

React application using functional components and hooks:

```
components/ → Reusable UI components (shadcn/ui + custom)
pages/ → Page-level components (Dashboard, Containers, Compose, Users, etc.)
hooks/ → Custom React hooks
services/ → API client functions (Axios)
store/ → State management (Zustand stores)
types/ → TypeScript type definitions
utils/ → Helper functions
```

**Key Technologies:**

- **State Management**: Zustand for global state
- **API Layer**: Axios + TanStack Query (React Query) for data fetching/caching
- **Forms**: React Hook Form + Zod validation
- **UI Components**: shadcn/ui (Radix UI primitives) + Tailwind CSS
- **Code Editor**: Monaco Editor for compose file editing
- **WebSockets**: Socket.IO Client for real-time updates
- **Routing**: React Router v6

**API Communication:**

- All API calls proxied through Nginx in production (`/api/*` → `backend:5000`)
- WebSocket connections proxied at `/ws/*`
- JWT token included in Authorization header: `Bearer {token}`
- TanStack Query handles caching, refetching, and optimistic updates

### File Path Handling Strategy

**Problem**: Compose file paths contain slashes that conflict with URL routing.

**Solution**: Use numeric IDs for API operations:

- `ComposeFiles` table maps file paths to IDs
- APIs use `/api/compose/files/{id}` for operations
- Alternative query param endpoint: `/api/compose/files/by-path?path={encodedPath}`
- File discovery runs on startup and periodically (default: 5 min intervals)
- WebSocket notifications when files change

### Compose Projects vs Files

- **Compose File**: Single YAML file stored in filesystem
- **Compose Project**: Logical grouping of services, can include multiple files (base + overrides)
- Project identified by name (directory name or `-p` flag)
- API automatically detects related files (same directory, naming convention like `docker-compose.override.yml`)

### Security Architecture

**Critical Security Considerations:**

1. **Docker Socket Access**: Backend container has root-level access to host via Docker socket mounting. This is required but dangerous. In production, consider Docker API over TCP with TLS instead.

2. **Path Traversal Prevention**: All file paths validated against whitelist (configured ComposePaths). Never trust user input for file paths.

3. **Input Validation**: FluentValidation on all endpoints, sanitize all input before passing to Docker API.

4. **Rate Limiting**:
   - Auth endpoints: 5 attempts per 15 min per IP
   - General API: 100 requests per minute per user

5. **Password Security**: BCrypt hashing with cost factor 12

6. **Audit Logging**: All security-relevant actions logged to AuditLogs table with user, IP, timestamp

**Security Headers**: Set in middleware (X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, CSP)

## Development Setup

### Prerequisites

- Docker Desktop (Windows/Mac) or Docker Engine (Linux)
- .NET 9 SDK
- Node.js 20+ and npm
- Git

### Local Development (Without Docker)

1. **Backend**:
   - Configure `appsettings.Development.json` with database path, JWT secret, Docker host, CORS origins
   - Run `dotnet ef database update` to create database
   - Run `dotnet watch run` for hot-reload
   - Access Swagger UI at `http://localhost:5000/swagger`

2. **Frontend**:
   - Create `.env.development` with `VITE_API_URL=http://localhost:5000` and `VITE_WS_URL=ws://localhost:5000`
   - Run `npm install` then `npm run dev`
   - Access at `http://localhost:5173`

3. **Default Credentials**: Username `admin`, password `admin` (must be changed on first login)

### Database Migrations

When modifying entity models:

1. Create migration: `dotnet ef migrations add DescriptiveName`
2. Review generated migration in `Migrations/` folder
3. Apply: `dotnet ef database update`
4. If mistake: `dotnet ef migrations remove` (only if not applied)

Migrations run automatically on application start in `Program.cs`.

## Important Implementation Details

### Long-Running Operations

Operations like `docker-compose up` with builds return immediately with operation ID:

```json
POST /api/compose/projects/myapp/up
→ { "operationId": "op-abc123", "status": "pending" }

GET /api/operations/op-abc123
→ { "status": "running", "progress": 45, "logs": "..." }
```

Frontend subscribes to WebSocket `operation:update` events for real-time progress.

### Real-Time Updates

- WebSocket endpoint `/ws` for container status updates
- WebSocket endpoint `/ws/logs` for log streaming
- Server-Sent Events (SSE) as fallback
- SignalR library on backend, Socket.IO on frontend

### Compose File Templates

Pre-defined templates stored in `backend/Resources/Templates/`:
- LAMP Stack, MEAN Stack, PostgreSQL + Redis, Nginx + PHP-FPM
- WordPress, Nextcloud, GitLab CE, Traefik, Prometheus + Grafana, ELK Stack

Accessed via `GET /api/compose/templates`, used when creating new compose files.

### Caching Strategy

- Container list: cached 5 seconds
- Compose file list: cached 30 seconds
- ETags for compose file content
- Cache invalidation on mutations

### Logging

- Backend uses Serilog with structured JSON logging
- Log levels: Trace, Debug, Information, Warning, Error, Fatal
- Logs written to console and `/app/logs/app-.log` (rolling daily, 30 day retention)
- All user actions logged to AuditLogs table

### Environment Configuration

All settings can be overridden via environment variables using double underscore notation:
```bash
Database__ConnectionString="Data Source=/path/to/db"
Jwt__Secret="secret-key"
Docker__Host="unix:///var/run/docker.sock"
Cors__Origins__0="http://localhost:5173"
```

## Testing Strategy

### Backend Tests

Located in `docker-compose-manager-back/tests/`:
- xUnit for unit tests
- Moq for mocking dependencies
- TestContainers for integration tests with real Docker containers
- Separate test classes for Controllers, Services, Repositories

### Frontend Tests

- Vitest for unit tests (React Testing Library)
- Playwright for E2E tests
- Test real user workflows: login → view containers → perform actions → verify results

## Common Issues

1. **Backend cannot connect to Docker daemon**:
   - Linux: Check socket permissions `sudo chmod 666 /var/run/docker.sock`
   - Windows: Ensure Docker Desktop is running

2. **CORS errors**: Ensure backend `Cors:Origins` includes frontend URL (`http://localhost:5173` for dev)

3. **Database locked errors**: SQLite doesn't handle high concurrency well. Ensure only one process accesses DB. Close DB Browser tools.

4. **Hot reload not working**: Use `dotnet watch run` (not `dotnet run`) and `npm run dev` (not build)

## API Documentation

- Swagger/OpenAPI documentation available at `/swagger` in development
- All endpoints documented with XML comments
- Standard response format:
  ```json
  Success: { "data": {...}, "success": true, "message": "..." }
  Error: { "success": false, "message": "...", "errors": {...}, "errorCode": "..." }
  ```

## Cross-Platform Considerations

- Backend automatically detects platform and uses appropriate Docker connection method
- File paths: Use `Path.Combine()` for cross-platform compatibility
- Line endings: Git should normalize to LF
- Docker socket path differs: Unix socket (Linux) vs named pipe (Windows)
