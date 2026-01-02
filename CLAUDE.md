# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This repository contains a Docker Compose management system with two main applications:

- **docker-compose-manager-back**: .NET 9 Web API backend that interfaces with Docker Engine
- **docker-compose-manager-front-new**: SvelteKit + TypeScript frontend with bits-ui and Tailwind CSS

The system provides a web-based interface for managing Docker containers and compose projects, with features including user authentication, role-based access control, real-time updates via SignalR, and **Docker-only project discovery** (no file system access required).

**Note**: The old React frontend has been removed. Only the Svelte frontend is now maintained.

## Recent Architecture Changes (2026-01)

### Migration to Docker-Only Discovery

The system has undergone a major architectural refactor:

**What Changed:**
- ❌ Removed database persistence for compose projects (ComposePaths, ComposeFiles tables deprecated)
- ✅ Projects now discovered directly from `docker compose ls --all`
- ✅ No file system access required for operations
- ✅ All operations use `-p projectName` flag
- ✅ Memory cache (10s) replaces database sync

**What Was Removed:**
- React frontend (replaced with Svelte)
- File editing feature (temporarily disabled via feature flags)
- Template creation (temporarily disabled)
- Background file discovery service
- ComposeFiles and ComposePaths management UI

**What Still Works:**
- All Docker operations (up, down, restart, logs, etc.)
- Project discovery and management
- User permissions (by project name)
- Real-time updates via SignalR
- Container management

**Migration Path:**
- Frontend: Fully migrated to Svelte ✅
- Backend: Implementation pending (see COMPOSE_DISCOVERY_REFACTOR.md)
- Database: ComposePaths/ComposeFiles tables will be removed in future migration

**Documentation:**
- See `COMPOSE_DISCOVERY_REFACTOR.md` for complete technical details
- See frontend `src/lib/config/features.ts` for feature flags

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

### Frontend (docker-compose-manager-front-new)

Located in `./docker-compose-manager-front-new/`

```bash
# Install dependencies
npm install

# Run development server (with hot-reload)
npm run dev

# Build for production
npm run build

# Preview production build
npm run preview

# Type-check without running
npm run check

# Type-check in watch mode
npm run check:watch
```

Frontend runs at `http://localhost:5173` (Vite default with SvelteKit)

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

- Schema includes: Users, Roles, ResourcePermissions, AppSettings, AuditLogs, Sessions
- **Deprecated tables**: ComposePaths, ComposeFiles (replaced by Docker-only discovery)
- Migrations managed via Entity Framework Core
- Default location: `Data/app.db` (development) or `/app/data/app.db` (Docker)
- Architecture supports future migration to PostgreSQL if needed

**Docker Integration:**

- Connects to Docker daemon via socket (Linux: `unix:///var/run/docker.sock`, Windows: `npipe:////./pipe/docker_engine`)
- Must have access to Docker socket to function - this grants root-level access to host
- Uses Docker.DotNet library for API communication

### Frontend Architecture

SvelteKit application using Svelte 5 with runes:

```
src/
  lib/
    api/ → API client modules (Axios-based)
    components/ → Reusable UI components (bits-ui + custom)
    config/ → Feature flags and configuration
    services/ → SignalR and other services
    stores/ → Svelte stores for state management
    types/ → TypeScript type definitions
    utils/ → Helper functions
  routes/ → File-based routing (SvelteKit)
    (protected)/ → Authenticated routes
      compose/ → Compose projects and files
      containers/ → Container management
      dashboard/ → Dashboard
      users/ → User management
```

**Key Technologies:**

- **Framework**: SvelteKit (file-based routing, SSR/SPA)
- **State Management**: Svelte 5 runes ($state, $derived, $effect) + Svelte stores
- **API Layer**: Axios + TanStack Svelte Query for data fetching/caching
- **Forms**: sveltekit-superforms + Zod validation
- **UI Components**: bits-ui (Radix-like primitives for Svelte) + Tailwind CSS
- **Code Editor**: Monaco Editor (currently disabled via feature flags)
- **Real-time**: @microsoft/signalr for WebSocket updates
- **Routing**: SvelteKit file-based routing
- **Icons**: lucide-svelte
- **Notifications**: svelte-sonner (Toast)
- **Charts**: d3-scale + LayerCake

**API Communication:**

- All API calls use Axios client with interceptors
- JWT token auto-injected in Authorization header: `Bearer {token}`
- Refresh token handling via interceptors
- TanStack Svelte Query handles caching, refetching, and optimistic updates
- SignalR for real-time events (operations, container state, compose projects)

**Feature Flags:**

Located in `src/lib/config/features.ts`:
- `COMPOSE_FILE_EDITING: false` - File editing temporarily disabled (cross-platform issues)
- `COMPOSE_TEMPLATES: false` - Template creation disabled (depends on file editing)

### Docker-Only Project Discovery (NEW ARCHITECTURE)

**Philosophy**: Docker is the single source of truth. No database persistence for projects.

**How it works:**

1. **Discovery**: Backend calls `docker compose ls --all --format json` to get all projects
2. **Caching**: Results cached in memory for 10 seconds (IMemoryCache)
3. **Permissions**: Project list filtered by user permissions (ResourcePermission table uses project name)
4. **Operations**: All operations use `-p projectName` flag - no file access required
5. **Real-time**: SignalR events invalidate cache when projects change

**Benefits:**

- ✅ No database sync required
- ✅ No cross-platform path mapping issues
- ✅ Projects auto-discovered (even those created outside the app)
- ✅ Projects with `docker compose down` still visible (status: "exited(0)")
- ✅ Stateless architecture (easy horizontal scaling)

**Project DTO Structure:**

```typescript
interface ComposeProjectDto {
  name: string;              // Docker project name
  rawStatus: string;         // e.g., "running(3)", "exited(2)"
  configFiles: string[];     // Informational only (host paths)
  status: ProjectStatus;     // Running | Stopped | Removed | Unknown
  containerCount: number;
  userPermissions: PermissionFlags;
  canStart: boolean;
  canStop: boolean;
  statusColor: string;
}
```

**Compose File Editing:**

- ⚠️ **Currently disabled** via feature flags (cross-platform issues)
- File paths shown are informational only (from Docker metadata)
- Operations don't require file access - use project name instead
- To add projects: Create `docker-compose.yml` on host → run `docker compose up` → auto-discovered

### Security Architecture

**Critical Security Considerations:**

1. **Docker Socket Access**: Backend container has root-level access to host via Docker socket mounting. This is required but dangerous. In production, consider Docker API over TCP with TLS instead.

2. **Path Traversal Prevention**: ~~All file paths validated against whitelist (configured ComposePaths)~~ **(DEPRECATED)** - File editing disabled. Docker operations use project names only.

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

2. **Frontend** (SvelteKit):
   - Navigate to `./docker-compose-manager-front-new/`
   - Create `.env` if needed (optional - defaults to localhost:5000)
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

Frontend subscribes to SignalR hub events for real-time progress.

### Real-Time Updates (SignalR)

**Hubs:**
- `/hubs/operations` - Operation updates, container state changes, compose project changes
- `/hubs/logs` - Real-time log streaming

**Frontend Integration:**
- Uses `@microsoft/signalr` package
- Service located in `src/lib/services/signalr.ts`
- Automatic reconnection with exponential backoff
- Events trigger TanStack Query cache invalidation

**Key Events:**
- `OperationUpdate` - Progress updates for long-running operations
- `ContainerStateChanged` - Container lifecycle events
- `ComposeProjectStateChanged` - Project state changes (from Docker events)

### Compose File Templates

⚠️ **Currently disabled** via feature flags (depends on file editing feature).

Pre-defined templates stored in `backend/Resources/Templates/`:
- LAMP Stack, MEAN Stack, PostgreSQL + Redis, Nginx + PHP-FPM
- WordPress, Nextcloud, GitLab CE, Traefik, Prometheus + Grafana, ELK Stack

Templates are preserved for future reactivation once file editing is re-enabled.

### Caching Strategy

**Backend:**
- Compose projects: 10 seconds (IMemoryCache)
- Container list: 5 seconds
- Manual invalidation after operations (up, down, restart)

**Frontend (TanStack Query):**
- Stale time: 60 seconds
- Automatic refetch disabled (SignalR handles updates)
- Cache invalidated on SignalR events (debounced 500ms)
- Force refresh available via API (`?refresh=true`)

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

## Versioning and Releases

The project uses **automated semantic versioning** based on PR labels. See [RELEASING.md](RELEASING.md) for complete details.

### Quick Release Guide

1. **Create PR** with your changes targeting `main`
2. **Add label** to PR:
   - `release-major` - Breaking changes (v1.0.0 → v2.0.0)
   - `release-minor` - New features (v1.0.0 → v1.1.0)
   - `release-patch` - Bug fixes (v1.0.0 → v1.0.1)
3. **Merge PR** - Release is created automatically

### What Happens Automatically

On PR merge with release label:
- Git tag created (e.g., v0.2.0)
- VERSION file updated
- CHANGELOG.md updated
- GitHub Release created with notes
- Docker images built and tagged with version
- Version embedded in backend API and frontend UI

### Version Information

**Backend API endpoint:**
```bash
curl http://localhost:5000/api/system/version
# Returns: { version, buildDate, gitCommit, environment }
```

**Frontend component:**
```tsx
import { VersionInfo } from '@/components/common'
<VersionInfo />  // Simple badge
<VersionInfo showDetails />  // Detailed info
```

**Access version programmatically:**
```typescript
import { APP_VERSION } from '@/utils/version'
```

### Current Version

See the `VERSION` file in the repository root or check the latest [GitHub Release](../../releases/latest).

## Cross-Platform Considerations

- Backend automatically detects platform and uses appropriate Docker connection method
- File paths: Use `Path.Combine()` for cross-platform compatibility
- Line endings: Git should normalize to LF
- Docker socket path differs: Unix socket (Linux) vs named pipe (Windows)
