# Docker Compose Manager - Technical Specifications

## 1. Overview

Docker Compose Manager is a web-based application for managing Docker containers and Docker Compose projects. It provides a unified interface for viewing, controlling, and monitoring your Docker infrastructure.

### Architecture

The application is deployed as a **single unified container** containing:
- **Backend**: .NET 9 Web API
- **Frontend**: SvelteKit static files
- **Reverse Proxy**: Nginx
- **Process Manager**: Supervisor

### Key Features

- Cross-platform support (Linux, Windows)
- Automatic compose file discovery
- Real-time updates via SignalR WebSockets
- Role-based access control
- Comprehensive audit logging

---

## 2. Application Architecture

### Unified Deployment

The production deployment uses a single Docker image managed by Supervisor:

```
┌─────────────────────────────────────────────┐
│           Unified Container                 │
│  ┌───────────────────────────────────────┐  │
│  │            Supervisor                 │  │
│  │  ┌─────────────┐  ┌────────────────┐  │  │
│  │  │   Nginx     │  │   .NET API     │  │  │
│  │  │   (:80)     │  │   (:5000)      │  │  │
│  │  └──────┬──────┘  └───────┬────────┘  │  │
│  │         │                 │           │  │
│  │         │    /api/*       │           │  │
│  │         └────────────────►│           │  │
│  └───────────────────────────┼───────────┘  │
│                              │              │
│                              ▼              │
│                    ┌─────────────────┐      │
│                    │  SQLite + Docker│      │
│                    │     Socket      │      │
│                    └─────────────────┘      │
└─────────────────────────────────────────────┘
```

### Backend (.NET 9)

Layered architecture with clear separation of concerns:

```
Controllers/     → API endpoints, HTTP request/response handling
Services/        → Business logic and Docker operations
Repositories/    → Data access layer
Models/          → Domain entities
DTOs/            → Data transfer objects
Middleware/      → Authentication, authorization, error handling
Data/            → Entity Framework Core context and migrations
```

**Key Services:**

| Service | Responsibility |
|---------|---------------|
| `DockerService` | Docker daemon communication via Docker.DotNet |
| `ComposeService` | Docker Compose operations (up, down, logs) |
| `JwtTokenService` | JWT token generation and validation |
| `AuditService` | Activity logging |
| `ComposeFileScanner` | Filesystem discovery of compose files |
| `ComposeFileCacheService` | In-memory caching of discovered files |
| `ConflictResolutionService` | Project name conflict detection |
| `ProjectMatchingService` | Match Docker projects with compose files |

**Database Schema (SQLite):**

| Table | Description |
|-------|-------------|
| `Users` | User accounts with hashed passwords |
| `Roles` | Role definitions and permissions |
| `AppSettings` | Application configuration (key-value) |
| `AuditLogs` | User activity tracking |
| `Sessions` | Refresh token storage |

### Frontend (SvelteKit 2)

SvelteKit application with Svelte 5 and modern tooling:

```
src/
├── routes/          → SvelteKit file-based routing
├── lib/
│   ├── components/  → Reusable UI components (bits-ui based)
│   ├── stores/      → Svelte stores for state management
│   ├── api/         → API client functions (Axios)
│   └── types/       → TypeScript definitions
└── app.html         → HTML template
```

**Key Technologies:**

| Component | Technology |
|-----------|------------|
| Framework | SvelteKit 2.48.5 |
| UI Library | Svelte 5.43+ with runes |
| Components | bits-ui 2.14+ |
| Styling | Tailwind CSS 4 |
| Data Fetching | TanStack Svelte Query 6 |
| Real-time | @microsoft/signalr 10 |
| Forms | sveltekit-superforms + Zod |
| Code Editor | Monaco Editor |
| Internationalization | i18next |

### Compose Discovery System

**Automatic file discovery** replaces manual path configuration:

1. Scans a root directory (`/app/compose-files` by default) recursively
2. Discovers all `.yml` and `.yaml` files containing `services:` key
3. Extracts project names (from `name` field, directory, or filename)
4. Caches results in-memory with configurable TTL

**Configuration:**

```json
{
  "ComposeDiscovery": {
    "RootPath": "/app/compose-files",
    "ScanDepthLimit": 5,
    "CacheDurationSeconds": 10,
    "MaxFileSizeKB": 1024
  }
}
```

**Disabling Files:**

Add `x-disabled: true` to temporarily hide a compose file:

```yaml
x-disabled: true
name: my-project
services:
  web:
    image: nginx
```

**Conflict Resolution:**

When multiple files share a project name:
- 1 active file → Project uses that file
- 0 active files → Project hidden
- 2+ active files → Conflict error displayed

**Command Classification:**

| Requires File | Works Without File |
|---------------|-------------------|
| `up`, `build`, `pull`, `push`, `config` | `start`, `stop`, `restart`, `pause`, `unpause` |
| | `logs`, `ps`, `top`, `down`, `rm`, `kill` |

---

## 3. Technical Stack

### Backend

| Component | Technology |
|-----------|------------|
| Framework | .NET 9 (ASP.NET Core) |
| Language | C# 12 |
| ORM | Entity Framework Core 9 |
| Database | SQLite 3 |
| Docker Client | Docker.DotNet |
| Authentication | JWT (BCrypt for passwords) |
| Validation | FluentValidation |
| WebSocket | ASP.NET Core SignalR |
| Logging | Serilog (structured JSON) |
| YAML | YamlDotNet |
| Testing | xUnit, Moq, FluentAssertions |

### Frontend

| Component | Technology |
|-----------|------------|
| Framework | SvelteKit 2.48.5 |
| Language | TypeScript 5.9+ |
| UI Framework | Svelte 5.43+ |
| Build Tool | Vite 7 |
| Components | bits-ui 2.14+ |
| Styling | Tailwind CSS 4, tailwind-variants |
| Icons | lucide-svelte |
| Data Fetching | TanStack Svelte Query 6 |
| API Client | Axios |
| Real-time | @microsoft/signalr 10 |
| Forms | sveltekit-superforms 2, Zod 4 |
| Code Editor | Monaco Editor |
| Notifications | svelte-sonner |
| i18n | i18next |

### Deployment

| Component | Technology |
|-----------|------------|
| Container | Docker |
| Process Manager | Supervisor |
| Reverse Proxy | Nginx |
| Base Image | .NET 9 ASP.NET + Node.js |

---

## 4. Security

### Docker Socket Access

The application requires Docker socket access, granting **root-level access** to the host. Security measures:

- Path traversal prevention with `PathValidator`
- All file paths validated against configured `RootPath`
- Input sanitization before Docker API calls
- Audit logging of all operations

### Authentication

**JWT Token Strategy:**

| Token Type | Lifetime | Storage |
|------------|----------|---------|
| Access Token | 60 minutes | Frontend memory |
| Refresh Token | 7 days | Database (Sessions table) |

**Token Flow:**
1. Login → Access + Refresh tokens issued
2. Access token expires → Use refresh token
3. Refresh token expires → Re-authentication required
4. Logout/Password change → Refresh token revoked

**Password Security:**
- BCrypt hashing with work factor 12
- Minimum 8 characters (configurable)
- Force password change on first login

### Rate Limiting

| Endpoint | Limit |
|----------|-------|
| Authentication | 5 attempts / 15 min / IP |
| General API | 100 requests / min / user |

### Security Headers

```
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
X-XSS-Protection: 1; mode=block
Referrer-Policy: strict-origin-when-cross-origin
Content-Security-Policy: default-src 'self'
```

---

## 5. API Overview

### Endpoint Categories

| Category | Base Path | Description |
|----------|-----------|-------------|
| Authentication | `/api/auth` | Login, logout, token refresh |
| Users | `/api/users` | User management (admin) |
| Profile | `/api/profile` | Current user profile |
| Containers | `/api/containers` | Container operations |
| Compose | `/api/compose` | Compose projects and files |
| Dashboard | `/api/dashboard` | Statistics and health |
| System | `/api/system` | Version info |

### Response Format

**Success:**
```json
{
  "data": { ... },
  "success": true,
  "message": "Operation completed"
}
```

**Error:**
```json
{
  "success": false,
  "message": "Error description",
  "errorCode": "ERROR_CODE",
  "errors": { "field": ["validation error"] }
}
```

### Error Codes

| Code | Description |
|------|-------------|
| `AUTH_INVALID_CREDENTIALS` | Invalid username/password |
| `AUTH_TOKEN_EXPIRED` | JWT token expired |
| `AUTH_INSUFFICIENT_PERMISSIONS` | Missing permissions |
| `DOCKER_CONNECTION_ERROR` | Cannot connect to Docker |
| `FILE_NOT_FOUND` | Compose file not found |
| `PATH_TRAVERSAL_DETECTED` | Security violation |
| `RATE_LIMIT_EXCEEDED` | Too many requests |

### Real-Time Endpoints

| Endpoint | Protocol | Purpose |
|----------|----------|---------|
| `/hub/containers` | SignalR | Container status updates |
| `/hub/logs` | SignalR | Log streaming |

---

## 6. Development & Testing

### Backend Commands

```bash
cd docker-compose-manager-back

dotnet restore           # Restore dependencies
dotnet watch run         # Run with hot-reload
dotnet build -c Release  # Build for production
dotnet test              # Run tests

# Database migrations
dotnet ef migrations add MigrationName
dotnet ef database update
```

### Frontend Commands

```bash
cd docker-compose-manager-front

npm install      # Install dependencies
npm run dev      # Development server
npm run build    # Production build
npm run check    # Type checking
```

### Docker Commands

```bash
docker compose up --build   # Build and run
docker compose up -d        # Background
docker compose logs -f      # Follow logs
docker compose down         # Stop
```

### Testing

**Backend (xUnit):**
- Unit tests for services and controllers
- Integration tests with TestContainers
- 100+ tests covering compose discovery

**Frontend:**
- Component tests with Vitest
- E2E tests with Playwright (planned)

---

## 7. Configuration

### Environment Variables

All settings support environment variable override with double-underscore notation:

```bash
# Database
Database__ConnectionString="Data Source=/app/data/app.db"

# JWT
Jwt__Secret="your-32-char-secret-key-minimum"
Jwt__ExpirationMinutes=60

# Docker
Docker__Host="unix:///var/run/docker.sock"

# CORS
Cors__Origins__0="http://localhost:5173"

# Compose Discovery
ComposeDiscovery__RootPath="/app/compose-files"
ComposeDiscovery__ScanDepthLimit=5

# Logging
Serilog__MinimumLevel__Default="Information"
```

### Ports

| Service | Development | Production |
|---------|-------------|------------|
| Backend API | 5000 | 5000 (internal) |
| Frontend Dev | 5173 | N/A |
| Application | N/A | 3030 (exposed) |
