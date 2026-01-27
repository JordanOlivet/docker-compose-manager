# Unified Deployment Architecture

## Overview

The Docker Compose Manager has been refactored to use a **unified Docker image** that contains both the backend (.NET 9 API) and frontend (SvelteKit) in a single container.

## Architecture Changes

### Before (Two-Container Setup)

```
Host:3000 → Frontend Container (Nginx:80) → [network] → Backend Container:5000
```

- Two separate services in docker-compose.yml
- Inter-container networking required
- Two images to build and maintain
- Backend port exposed (security concern)

### After (Single-Container Setup)

```
Host:3000 → Single Container → Nginx:80 → localhost:5000 (.NET API)
                              └→ Frontend static files (/app/wwwroot)
```

- Single service in docker-compose.yml
- No inter-container networking needed
- One unified image
- Backend only accessible via localhost (improved security)

## Key Components

### 1. Multi-Stage Dockerfile

Located at: `Dockerfile` (repository root)

**Build stages:**
1. **backend-build**: Compiles .NET backend
2. **frontend-build**: Builds SvelteKit frontend
3. **base**: Installs runtime dependencies (nginx, supervisor, docker-cli)
4. **final**: Assembles the final image

### 2. Supervisor Configuration

Located at: `supervisord.conf`

Manages two processes within the container:
- **backend**: .NET application on localhost:5000
- **nginx**: Reverse proxy on port 80

### 3. Nginx Configuration

Located at: `nginx-unified.conf`

- Serves static frontend files from `/app/wwwroot`
- Proxies `/api` requests to `localhost:5000`
- Proxies `/hubs` (SignalR) to `localhost:5000`
- Provides `/health` endpoint for health checks

### 4. Docker Compose

Located at: `docker-compose.yml`

Simplified to a single service:
- Service name: `app`
- Image: `ghcr.io/jordanolivet/docker-compose-manager:latest`
- Single port exposed: `3000:80`
- Volumes: data, logs, compose-files, docker socket

## Benefits

✅ **Simpler deployment**: One service instead of two
✅ **Better security**: Backend port not exposed to host
✅ **Reduced resources**: Single container footprint
✅ **Easier configuration**: Fewer environment variables
✅ **No network overhead**: Internal localhost communication
✅ **Unified versioning**: Single image tag for both components

## Limitations

⚠️ **Debugging complexity**: Two processes in one container
⚠️ **No hot-reload in container**: Use separate dev environments
⚠️ **Larger image size**: ~50MB increase vs separate images
⚠️ **Restart scope**: Restarting one service restarts both

## Building Locally

### Quick Build & Run

```bash
# Build the unified image
docker build -t docker-compose-manager:local .

# Run the container
docker run -d \
  --name dcm-test \
  -p 3000:80 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -e Jwt__Secret=your-secret-key-minimum-32-characters-long \
  docker-compose-manager:local

# View logs
docker logs -f dcm-test

# Access the application
# Open http://localhost:3000 in your browser
```

### Using Docker Compose (Development)

```bash
# Build and start
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build

# View logs
docker compose logs -f

# Stop and clean up
docker compose down -v
```

## Testing Checklist

After building, verify the following:

### Build & Startup
- [ ] Image builds without errors
- [ ] Container starts successfully
- [ ] Both processes (backend + nginx) are running
- [ ] No errors in logs after 1 minute
- [ ] Health check passes

**Commands:**
```bash
# Check running processes
docker exec <container> ps aux

# Should show: supervisord, dotnet, nginx

# Check health
curl http://localhost:3000/health
```

### Frontend Functionality
- [ ] Homepage loads at http://localhost:3000
- [ ] Static assets load (CSS, JS, images)
- [ ] SPA routing works (navigate to /containers, refresh page)

### Backend Functionality
- [ ] API responds: `curl http://localhost:3000/api/health`
- [ ] Login works (POST /api/auth/login)
- [ ] Protected endpoints require JWT
- [ ] Container list loads (GET /api/containers)
- [ ] WebSocket connects (check browser DevTools)

### Docker Integration
- [ ] Docker CLI accessible in container
- [ ] docker-compose plugin works
- [ ] Container operations work (list, start, stop)
- [ ] Compose file discovery works

### Persistence
- [ ] Database persists after restart
- [ ] Logs written to volume
- [ ] Compose files discovered from volume

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `Jwt__Secret` | *(required)* | JWT signing key (min 32 chars) |
| `Docker__Host` | `unix:///var/run/docker.sock` | Docker socket path |
| `Serilog__MinimumLevel__Default` | `Information` | Log level |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Environment (use `Development` for verbose logs) |

## CI/CD Pipeline

The GitHub Actions workflow has been updated to:

1. **Run backend tests** (dotnet test)
2. **Run frontend tests** (npm test)
3. **Build unified Docker image** (multi-stage build)
4. **Push to GitHub Container Registry** (single image)

**Image naming:**
- `ghcr.io/jordanolivet/docker-compose-manager:latest`
- `ghcr.io/jordanolivet/docker-compose-manager:v1.0.0` (version tags)

## Migration Guide

### For Existing Deployments

If you're upgrading from the two-container setup:

1. **Pull the latest code:**
   ```bash
   git pull origin main
   ```

2. **Stop existing containers:**
   ```bash
   docker compose down
   ```

3. **Update configuration:**
   - The new `docker-compose.yml` is already configured for the unified image
   - Verify your `.env` file has `JWT_SECRET` set

4. **Start with new unified image:**
   ```bash
   docker compose pull
   docker compose up -d
   ```

5. **Verify everything works:**
   ```bash
   docker compose logs -f
   curl http://localhost:3000/health
   ```

### Volume Migration

No action needed - volume names have been updated:
- `backend-data` → `app-data` (Docker handles migration automatically)

## Development Workflow

For local development, continue using separate processes:

### Backend (docker-compose-manager-back/)
```bash
cd docker-compose-manager-back
dotnet watch run
# Access at http://localhost:5000
```

### Frontend (docker-compose-manager-front/)
```bash
cd docker-compose-manager-front
npm run dev
# Access at http://localhost:5173
```

This provides hot-reload capabilities that aren't available in the containerized setup.

## Troubleshooting

### Container won't start

**Check logs:**
```bash
docker logs <container-name>
```

**Common issues:**
- Missing `Jwt__Secret` environment variable
- Docker socket not mounted
- Port 3000 already in use

### Backend not responding

**Check if .NET process is running:**
```bash
docker exec <container> ps aux | grep dotnet
```

**Check backend logs:**
```bash
docker exec <container> cat /app/logs/*.log
```

### Frontend returns 502 Bad Gateway

This means nginx is running but can't reach the backend.

**Check supervisor status:**
```bash
docker exec <container> supervisorctl status
```

Both `backend` and `nginx` should show `RUNNING`.

### Permission errors with Docker socket

**On Linux:**
```bash
sudo chmod 666 /var/run/docker.sock
```

## Performance Considerations

### Resource Usage
- **Memory**: ~300-500MB idle (similar to two-container setup)
- **CPU**: Minimal when idle
- **Disk**: ~400-450MB image size

### Startup Time
- **Cold start**: ~30-40 seconds
- **Health check**: Passes after ~40 seconds
- **First request**: Immediate after health check passes

## Security Notes

- ✅ Backend port (5000) not exposed to host
- ✅ All API traffic goes through nginx reverse proxy
- ✅ Security headers configured (X-Frame-Options, etc.)
- ⚠️ Docker socket access required (root-level access to host)
- ⚠️ Use strong JWT secret (32+ characters)

## Support

For issues related to:
- **Deployment**: Open an issue on GitHub
- **Architecture**: See CLAUDE.md
- **Development**: See README.md

## Version History

- **v1.0.0+**: Unified single-container deployment
- **v0.x.x**: Legacy two-container deployment (deprecated)
