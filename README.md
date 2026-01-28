# Docker Compose Manager

A web-based interface for managing Docker containers and Docker Compose projects.

## Overview

Docker Compose Manager provides a modern web UI for managing your Docker infrastructure. It allows you to:

- View and control Docker containers
- Manage Docker Compose projects with automatic file discovery
- Edit compose files with syntax highlighting
- Monitor container logs and statistics in real-time
- Manage users with role-based access control

**Current Version**: See [VERSION](VERSION) file or [Releases](../../releases)

## Quick Start

### Docker Deployment (Recommended)

```bash
# 1. Create a directory for your compose files
mkdir compose-files

# 2. Create docker-compose.yml
cat > docker-compose.yml << 'EOF'
services:
  app:
    image: ghcr.io/jordanolivet/docker-compose-manager:latest
    container_name: docker-compose-manager
    ports:
      - "3030:80"
    environment:
      - Jwt__Secret=YOUR-SECRET-KEY-MIN-32-CHARS-CHANGE-ME
    volumes:
      - app-data:/app/data
      - /var/run/docker.sock:/var/run/docker.sock
      - ./compose-files:/app/compose-files
    restart: unless-stopped

volumes:
  app-data:
EOF

# 3. Start the application
docker compose up -d

# 4. Access at http://localhost:3030
# Default credentials: admin / adminadmin (must be changed on first login)
```

For Windows hosts, replace the Docker socket volume with:
```yaml
- //./pipe/docker_engine://./pipe/docker_engine
```

## Features

### Dashboard
- Container status overview (running, stopped, total)
- System resource usage statistics
- Quick access to all managed containers

### Container Management
- List, start, stop, restart containers
- View container details (ports, volumes, networks)
- Real-time log streaming
- Container statistics (CPU, memory, network)

### Compose Project Management
- **Automatic file discovery** - Drop compose files in `/app/compose-files`
- Execute compose commands (up, down, start, stop, restart)
- View project status and service health
- Edit compose files with Monaco editor (syntax highlighting)

### User Management (Admin)
- Create and manage user accounts
- Role-based access control
- Activity audit logging
- Session management

### Real-Time Updates
- Live container status via SignalR WebSockets
- Real-time log streaming
- Instant notifications for operations

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `Jwt__Secret` | JWT signing key (min 32 chars) | **Required** |
| `Docker__Host` | Docker daemon connection | `unix:///var/run/docker.sock` |
| `Serilog__MinimumLevel__Default` | Log level | `Information` |
| `ComposeDiscovery__RootPath` | Compose files directory | `/app/compose-files` |
| `ComposeDiscovery__ScanDepthLimit` | Max directory depth | `5` |
| `ComposeDiscovery__CacheDurationSeconds` | File cache TTL | `10` |

### JWT Secret (Required)

The `Jwt__Secret` environment variable is **mandatory** for the application to start.

**What is it?**
A secret key used to cryptographically sign authentication tokens (JWT). When users log in, the server generates a token signed with this secret. On each subsequent request, the server verifies the token's signature to ensure it hasn't been tampered with.

**Why is it required?**
Without a JWT secret, the application cannot securely authenticate users. The secret ensures that only the server can generate valid tokens - anyone without the secret cannot forge authentication tokens.

**How to generate one:**

```bash
# Linux/macOS
openssl rand -base64 32

# PowerShell (Windows)
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])

# Or use any random string generator (minimum 32 characters)
```

**Important:**
- Must be at least 32 characters long
- Keep it secret - never commit to version control
- Use a different secret for each environment (dev, staging, prod)
- If you change the secret, all existing user sessions will be invalidated

### Volumes

| Path | Description |
|------|-------------|
| `/app/data` | SQLite database (persist this!) |
| `/var/run/docker.sock` | Docker socket (required) |
| `/app/compose-files` | Your compose files to manage |
| `/app/logs` | Application logs (optional) |

## Development Setup

### Prerequisites
- Docker Desktop or Docker Engine
- .NET 9 SDK
- Node.js 20+

### Backend

```bash
cd docker-compose-manager-back

# Restore and run
dotnet restore
dotnet watch run

# Access Swagger UI at http://localhost:5050/swagger
```

### Frontend

```bash
cd docker-compose-manager-front

# Install and run
npm install
npm run dev

# Access at http://localhost:5173
```

### Docker (Full Stack)

```bash
# Build and run both services
docker compose up --build

# Access at http://localhost:3030
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Docker Container                         │
│  ┌─────────────────────────────────────────────────────────┐│
│  │                    Nginx (:80)                          ││
│  │         Reverse Proxy + Static File Server              ││
│  └────────────────────────┬────────────────────────────────┘│
│                           │                                 │
│           ┌───────────────┴───────────────┐                 │
│           │                               │                 │
│           ▼                               ▼                 │
│  ┌─────────────────┐            ┌─────────────────┐         │
│  │   Frontend      │            │    Backend      │         │
│  │   (SvelteKit)   │  /api/*    │   (.NET 9)      │         │
│  │   Static Files  │ ─────────► │   :5050         │         │
│  └─────────────────┘            └────────┬────────┘         │
│                                          │                  │
│                                          ▼                  │
│                                 ┌─────────────────┐         │
│                                 │  SQLite + Docker│         │
│                                 │     Socket      │         │
│                                 └─────────────────┘         │
└─────────────────────────────────────────────────────────────┘
```

### Technology Stack

**Backend (.NET 9)**
- ASP.NET Core Web API
- Entity Framework Core + SQLite
- Docker.DotNet for Docker API
- SignalR for real-time updates
- Serilog for logging

**Frontend (SvelteKit 2)**
- Svelte 5 with runes
- TanStack Svelte Query
- bits-ui components
- Tailwind CSS 4
- Monaco Editor

## Security Notes

### Docker Socket Access

This application requires access to the Docker socket, which grants **root-level access** to the host system. Only deploy in trusted environments.

Recommendations:
- Use HTTPS with a reverse proxy (Traefik, Nginx)
- Change default credentials immediately
- Use strong JWT secrets (generate with `openssl rand -base64 32`)
- Consider Docker API over TLS for remote deployments

### Default Credentials

```
Username: admin
Password: adminadmin
```

You will be required to change the password on first login.

## Documentation

- [DEPLOYMENT.md](DEPLOYMENT.md) - Production deployment guide
- [SPECS.md](SPECS.md) - Technical specifications
- [CHANGELOG.md](CHANGELOG.md) - Version history

## License

GPL-2.0 License - see [LICENSE](LICENSE) for details.
