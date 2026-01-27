# Docker Compose Manager

A full-stack application for managing Docker containers and Docker Compose files through a modern web interface.

## Tech Stack

### Backend
- .NET 9 Web API
- Entity Framework Core with SQLite
- JWT Authentication
- Docker.DotNet
- Serilog

### Frontend
- React 18 + TypeScript
- Vite
- React Router
- Zustand (state management)
- Axios + TanStack Query
- Tailwind CSS

## Quick Start

### Prerequisites
- Docker Desktop (Windows/Mac) or Docker Engine (Linux)
- .NET 9 SDK (for local development)
- Node.js 20+ (for local development)

### Using Docker Compose (Recommended)

#### Option 1: Using Pre-built Images from GitHub Container Registry (Production-Ready)

1. Copy `.env.example` to `.env` and configure:
```bash
cp .env.example .env
```

2. Edit `.env` and set your GitHub repository:
```bash
GITHUB_REPOSITORY=your-github-username/docker-compose-manager
```

3. Pull and start the services:
```bash
docker compose pull
docker compose up -d
```

This will use the pre-built multi-architecture images from GitHub Container Registry.

#### Option 2: Building Locally (Development)

If you want to build the images locally instead of using the registry:

1. Copy `.env.example` to `.env` and configure:
```bash
cp .env.example .env
```

2. Build and start with the development compose file:
```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

This will build the Docker images from source on your machine.

#### Accessing the Application

3. Access the application at `http://localhost:3000`

4. Default credentials:
   - Username: `admin`
   - Password: `adminadmin`
   - **Important**: Change the password on first login

#### Using Specific Image Versions

You can specify which version to use by setting `IMAGE_TAG` in your `.env`:

```bash
# Use latest version (default)
IMAGE_TAG=latest

# Use a specific version
IMAGE_TAG=1.0.0

# Use a specific commit
IMAGE_TAG=sha-abc1234

# Use main branch
IMAGE_TAG=main
```

### Local Development

#### Quick Start Scripts (Windows)

**Start both applications with hot-reload:**
```bash
start-dev.bat
```

This will open two terminal windows:
- Backend at `http://localhost:5000` (Swagger auto-opens)
- Frontend at `http://localhost:5173`

**Stop all development processes:**
```bash
stop-dev.bat
```

#### Manual Start (if you prefer separate terminals)

**Backend:**
```bash
cd docker-compose-manager-back
dotnet restore
dotnet ef database update
dotnet watch run
```

Backend will be available at `http://localhost:5000`
Swagger UI at `http://localhost:5000/swagger`

**Frontend:**
```bash
cd docker-compose-manager-front
npm install
npm run dev
```

Frontend will be available at `http://localhost:5173`

## Features Implemented

### ✅ Authentication & Authorization
- JWT-based authentication with refresh tokens
- Secure password hashing with BCrypt
- Session management
- Protected routes

### ✅ Docker Container Management
- List all containers
- View container details
- Start/Stop/Restart containers
- Remove containers

### ✅ API Documentation
- Swagger/OpenAPI integration
- Comprehensive API documentation

### ✅ Security
- CORS configuration
- Input validation
- Rate limiting ready
- Audit logging infrastructure

### ✅ Compose File Discovery (NEW in v0.21.0)
- Automatic discovery of compose files in designated directory
- No manual path configuration needed
- Real-time conflict detection
- `x-disabled` flag to temporarily disable files
- Intelligent project name matching
- Orphaned project management (containers without files)
- Thread-safe caching with configurable TTL

## Project Structure

```
docker-compose-manager/
├── docker-compose-manager-back/    # .NET 9 Backend
│   ├── src/
│   │   ├── Controllers/            # API endpoints
│   │   ├── Services/               # Business logic
│   │   ├── Models/                 # Database entities
│   │   ├── DTOs/                   # Data transfer objects
│   │   └── Data/                   # EF Core context & migrations
│   ├── Dockerfile
│   └── docker-compose-manager-back.csproj
├── docker-compose-manager-front/   # React Frontend
│   ├── src/
│   │   ├── api/                    # API client functions
│   │   ├── components/             # React components
│   │   ├── pages/                  # Page components
│   │   ├── stores/                 # Zustand stores
│   │   └── types/                  # TypeScript types
│   ├── Dockerfile
│   ├── nginx.conf
│   └── package.json
├── docker-compose.yml
├── .env.example
└── README.md
```

## Environment Variables

See `.env.example` for all configuration options.

Key variables:
- `JWT_SECRET`: Secret key for JWT signing (MUST be changed in production)
- `DOCKER_HOST`: Docker daemon connection string

## Configuration

### Compose File Discovery

Configure the automatic file discovery system in `appsettings.json`:

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

**Configuration Options**:

- **RootPath**: Directory to scan for compose files (default: `/app/compose-files`)
  - Can be an absolute path or relative to the application directory
  - Must be readable by the backend process

- **ScanDepthLimit**: Maximum directory depth to scan (default: `5`)
  - Prevents infinite recursion in deep directory structures
  - Set to `0` to scan only the root directory

- **CacheDurationSeconds**: How long to cache discovery results (default: `10`)
  - Lower values: More responsive to file changes
  - Higher values: Better performance, less filesystem access

- **MaxFileSizeKB**: Maximum compose file size in KB (default: `1024` = 1MB)
  - Prevents scanning extremely large files
  - Files exceeding this limit are ignored

**Environment Variable Override**:

You can override settings using environment variables with double underscore notation:

```bash
ComposeDiscovery__RootPath=/custom/path
ComposeDiscovery__ScanDepthLimit=3
ComposeDiscovery__CacheDurationSeconds=30
```

**Docker Compose Example**:

```yaml
services:
  backend:
    environment:
      - ComposeDiscovery__RootPath=/app/compose-files
      - ComposeDiscovery__CacheDurationSeconds=30
    volumes:
      - ./my-compose-files:/app/compose-files:ro  # Mount your compose files
```

## CI/CD with GitHub Actions

The project includes automated CI/CD pipeline using GitHub Actions that builds and publishes Docker images to GitHub Container Registry (ghcr.io).

### Workflow Triggers

The pipeline runs automatically on:
- **Push to main branch**: Builds, tests, and publishes images with `latest` and `main` tags
- **Pull requests**: Builds and tests only (no publication) for validation
- **Git tags** (v1.0.0, v2.1.3, etc.): Publishes versioned releases with semantic version tags
- **Manual dispatch**: Can be triggered manually from the Actions tab

### Published Images

Images are published to GitHub Container Registry:
- Backend: `ghcr.io/<username>/docker-compose-manager-backend`
- Frontend: `ghcr.io/<username>/docker-compose-manager-frontend`

Both images support multi-architecture:
- `linux/amd64` (x86_64)
- `linux/arm64` (ARM64, including Apple Silicon)

### Image Tags

Images are automatically tagged with:
- `latest` - Latest build from main branch
- `main` - Main branch builds
- `sha-<commit>` - Specific commit SHA (e.g., `sha-abc1234`)
- Semantic versions for tagged releases:
  - `v1.2.3` → `1.2.3`, `1.2`, `1`

### Docker Image Management

#### Pulling Images from GitHub Container Registry

If the images are public, simply run:
```bash
docker compose pull
```

If the images are private, you need to authenticate first:

```bash
# Create a GitHub Personal Access Token with 'read:packages' permission
# Then login to GitHub Container Registry
echo $GITHUB_TOKEN | docker login ghcr.io -u your-github-username --password-stdin

# Pull the images
docker compose pull
```

#### Updating to Latest Version

To update to the latest version of the images:

```bash
# Pull latest images
docker compose pull

# Restart services with new images
docker compose up -d

# Remove old images (optional)
docker image prune -f
```

## Migrating from v0.20.x to v0.21.0

**Major Change**: Compose file management has been completely revamped with automatic discovery.

### What Changed

**Removed**:
- Manual compose path configuration (database-stored paths)
- `/api/config/compose-paths` endpoints (now return HTTP 410 Gone)
- `ComposePaths` and `ComposeFiles` database tables

**New**:
- Automatic file discovery by scanning a single root directory
- No manual configuration needed - just drop your compose files in the designated folder
- Real-time conflict detection and resolution
- `x-disabled` feature to temporarily disable files

### Migration Steps

1. **Backup your existing setup** (optional):
   ```bash
   docker compose exec backend cp /app/data/app.db /app/data/app.db.backup
   ```

2. **Update to v0.21.0**:
   ```bash
   docker compose pull
   docker compose up -d
   ```

3. **Move your compose files** to the new root directory:
   ```bash
   # Default location: /app/compose-files
   # Create subdirectories to organize your files:
   mkdir -p /path/to/compose-files/project1
   mkdir -p /path/to/compose-files/project2

   # Move your files
   cp /old/location/docker-compose.yml /path/to/compose-files/project1/
   ```

4. **Verify discovery**:
   - Visit the Compose page in the UI
   - Check that all projects are discovered
   - Review any naming conflicts in the UI

5. **Configure the root path** (if needed):
   Edit your `appsettings.json` or `.env`:
   ```json
   {
     "ComposeDiscovery": {
       "RootPath": "/your/custom/path",
       "ScanDepthLimit": 5,
       "CacheDurationSeconds": 10
     }
   }
   ```

### Troubleshooting

**Problem**: "No compose files found"
- **Solution**: Check that files are in the configured `RootPath`
- Check file permissions (must be readable by the backend process)
- Verify files have `.yml` or `.yaml` extension

**Problem**: "Multiple files with same project name"
- **Solution**: Add `x-disabled: true` to files you want to ignore:
  ```yaml
  x-disabled: true
  name: my-project
  services:
    web:
      image: nginx
  ```

**Problem**: "Project running but no actions available"
- **Solution**: This happens when containers exist but the compose file is missing/outside root path
- Only runtime actions (stop, restart, logs) work without the file
- To enable all actions (up, build, etc.), ensure the compose file is discovered

**Problem**: "Discovery is slow"
- **Solution**: Reduce `ScanDepthLimit` to limit recursion depth
- Increase `CacheDurationSeconds` to cache results longer
- Remove unnecessary subdirectories from the root path

### Rollback (if needed)

To rollback to v0.20.x:

```bash
# Stop current version
docker compose down

# Set IMAGE_TAG to v0.20.x in .env
echo "IMAGE_TAG=0.20.0" >> .env

# Start old version
docker compose up -d

# Restore database backup if needed
docker compose exec backend cp /app/data/app.db.backup /app/data/app.db
docker compose restart backend
```

#### Building and Pushing Your Own Images

If you've forked this repository and want to build and push your own images:

```bash
# Build locally
docker compose -f docker-compose.yml -f docker-compose.dev.yml build

# Tag for your registry
docker tag docker-compose-manager-backend:dev ghcr.io/your-username/docker-compose-manager-backend:latest
docker tag docker-compose-manager-frontend:dev ghcr.io/your-username/docker-compose-manager-frontend:latest

# Push to registry (requires authentication)
docker push ghcr.io/your-username/docker-compose-manager-backend:latest
docker push ghcr.io/your-username/docker-compose-manager-frontend:latest
```

Or simply push to your fork and let GitHub Actions build and publish automatically!

### Pipeline Steps

The CI/CD pipeline performs the following:

**Backend:**
1. Setup .NET 9 environment
2. Restore NuGet dependencies
3. Build project in Release mode
4. Run xUnit tests with code coverage
5. Build multi-architecture Docker image
6. Publish to GitHub Container Registry

**Frontend:**
1. Setup Node.js 20 environment
2. Install npm dependencies
3. Run Vitest tests
4. Build multi-architecture Docker image
5. Publish to GitHub Container Registry

### Monitoring Builds

- View workflow runs in the **Actions** tab of your GitHub repository
- Failed builds will prevent image publication
- All tests must pass before images are built

### Permissions

The workflow requires the following repository permissions:
- `contents: read` - Read repository code
- `packages: write` - Publish to GitHub Container Registry

These permissions are automatically granted to the `GITHUB_TOKEN` in the workflow.

## Security Notes

⚠️ **Important**: The backend requires access to the Docker socket, which grants root-level access to the host system. Only deploy in trusted environments.
