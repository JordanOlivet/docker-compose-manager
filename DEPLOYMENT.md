# Production Deployment Guide

This guide covers deploying Docker Compose Manager in production environments.

## Prerequisites

- Docker Engine 24+ or Docker Desktop
- Docker Compose v2
- Access to Docker socket or Docker API
- Minimum 512MB RAM, 1 CPU core

## Quick Deploy

### 1. Create docker-compose.yml

```yaml
services:
  app:
    image: ghcr.io/jordanolivet/docker-compose-manager:latest
    container_name: docker-compose-manager
    ports:
      - "3030:80"
    environment:
      - Jwt__Secret=${JWT_SECRET}
      - Docker__Host=unix:///var/run/docker.sock
      - Serilog__MinimumLevel__Default=Information
    volumes:
      - app-data:/app/data
      - /var/run/docker.sock:/var/run/docker.sock
      - ./compose-files:/app/compose-files
      - ./logs:/app/logs
    restart: unless-stopped
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost/health || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

volumes:
  app-data:
```

### 2. Generate JWT Secret

```bash
# Linux/Mac
openssl rand -base64 32

# PowerShell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])
```

### 3. Create .env File

```bash
JWT_SECRET=your-generated-secret-here-minimum-32-characters
```

### 4. Deploy

```bash
docker compose up -d
```

### 5. Access Application

- URL: `http://your-server:3030`
- Default credentials: `admin` / `adminadmin`
- **Change password immediately on first login**

---

## Configuration

### Required Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `Jwt__Secret` | JWT signing key (min 32 chars) | Random base64 string |

### Optional Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `Docker__Host` | Docker daemon connection | `unix:///var/run/docker.sock` |
| `Serilog__MinimumLevel__Default` | Log level | `Information` |
| `ComposeDiscovery__RootPath` | Compose files directory | `/app/compose-files` |
| `ComposeDiscovery__ScanDepthLimit` | Directory scan depth | `5` |
| `ComposeDiscovery__CacheDurationSeconds` | File cache TTL | `10` |
| `ComposeDiscovery__MaxFileSizeKB` | Max compose file size | `1024` |

### Volumes

| Container Path | Purpose | Required |
|----------------|---------|----------|
| `/app/data` | SQLite database | Yes |
| `/var/run/docker.sock` | Docker socket (Linux) | Yes* |
| `//./pipe/docker_engine` | Docker pipe (Windows) | Yes* |
| `/app/compose-files` | Your compose files | Recommended |
| `/app/logs` | Application logs | Optional |

*One of the Docker connection methods is required.

---

## Deployment Scenarios

### Linux Server

Standard deployment with Unix socket:

```yaml
services:
  app:
    image: ghcr.io/jordanolivet/docker-compose-manager:latest
    ports:
      - "3030:80"
    environment:
      - Jwt__Secret=${JWT_SECRET}
      - Docker__Host=unix:///var/run/docker.sock
    volumes:
      - app-data:/app/data
      - /var/run/docker.sock:/var/run/docker.sock
      - /opt/compose-files:/app/compose-files
    restart: unless-stopped

volumes:
  app-data:
```

### Windows Server

Using named pipe for Docker Desktop:

```yaml
services:
  app:
    image: ghcr.io/jordanolivet/docker-compose-manager:latest
    ports:
      - "3030:80"
    environment:
      - Jwt__Secret=${JWT_SECRET}
      - Docker__Host=npipe://./pipe/docker_engine
    volumes:
      - app-data:/app/data
      - //./pipe/docker_engine://./pipe/docker_engine
      - C:/compose-files:/app/compose-files
    restart: unless-stopped

volumes:
  app-data:
```

### Behind Nginx Reverse Proxy

If running behind an existing Nginx:

```yaml
# docker-compose.yml
services:
  app:
    image: ghcr.io/jordanolivet/docker-compose-manager:latest
    # No ports exposed - accessed via reverse proxy
    environment:
      - Jwt__Secret=${JWT_SECRET}
    volumes:
      - app-data:/app/data
      - /var/run/docker.sock:/var/run/docker.sock
      - ./compose-files:/app/compose-files
    networks:
      - proxy-network
    restart: unless-stopped

networks:
  proxy-network:
    external: true

volumes:
  app-data:
```

```nginx
# nginx.conf
server {
    listen 443 ssl http2;
    server_name docker-manager.example.com;

    ssl_certificate /etc/ssl/certs/cert.pem;
    ssl_certificate_key /etc/ssl/private/key.pem;

    location / {
        proxy_pass http://app:80;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # WebSocket support for SignalR
    location /hub/ {
        proxy_pass http://app:80;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
    }
}
```

### With Traefik

```yaml
services:
  app:
    image: ghcr.io/jordanolivet/docker-compose-manager:latest
    environment:
      - Jwt__Secret=${JWT_SECRET}
    volumes:
      - app-data:/app/data
      - /var/run/docker.sock:/var/run/docker.sock
      - ./compose-files:/app/compose-files
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.docker-manager.rule=Host(`docker-manager.example.com`)"
      - "traefik.http.routers.docker-manager.entrypoints=websecure"
      - "traefik.http.routers.docker-manager.tls.certresolver=letsencrypt"
      - "traefik.http.services.docker-manager.loadbalancer.server.port=80"
    networks:
      - traefik-network
    restart: unless-stopped

networks:
  traefik-network:
    external: true

volumes:
  app-data:
```

---

## Security Hardening

### Docker Socket Implications

Mounting the Docker socket grants **root-level access** to the host system. The container can:
- Create privileged containers
- Access host filesystem via volume mounts
- Execute commands on the host

**Recommendations:**
- Only deploy in trusted environments
- Use network segmentation
- Consider Docker API over TLS for remote access
- Implement host-based firewall rules

### HTTPS Configuration

Always use HTTPS in production. Options:

1. **Reverse proxy with TLS termination** (recommended)
2. **Docker API over TLS** for remote Docker hosts

### JWT Secret Management

- Generate cryptographically secure secrets (min 256 bits)
- Rotate secrets periodically
- Use secret management tools (Vault, AWS Secrets Manager)
- Never commit secrets to version control

### Network Security

```yaml
services:
  app:
    # ... other config ...
    networks:
      - internal
      - frontend

networks:
  internal:
    internal: true  # No external access
  frontend:
    # Exposed for reverse proxy
```

### User Permissions

After initial deployment:

1. Login with default credentials (`admin`/`adminadmin`)
2. Change admin password immediately
3. Create individual user accounts
4. Disable or rename default admin if possible
5. Use principle of least privilege for roles

---

## Maintenance

### Updating

```bash
# Pull latest image
docker compose pull

# Recreate container
docker compose up -d

# Check logs for issues
docker compose logs -f app
```

### Backup

**Database backup:**
```bash
# Stop for consistent backup
docker compose stop

# Copy database file
docker cp docker-compose-manager:/app/data/app.db ./backup/app.db

# Restart
docker compose start
```

**Automated backup script:**
```bash
#!/bin/bash
BACKUP_DIR="/backup/docker-manager"
DATE=$(date +%Y%m%d_%H%M%S)

mkdir -p $BACKUP_DIR
docker cp docker-compose-manager:/app/data/app.db "$BACKUP_DIR/app_$DATE.db"

# Keep last 7 days
find $BACKUP_DIR -name "app_*.db" -mtime +7 -delete
```

### Restore

```bash
# Stop container
docker compose stop

# Restore database
docker cp ./backup/app.db docker-compose-manager:/app/data/app.db

# Start container
docker compose start
```

### Log Management

Logs are written to `/app/logs` with daily rotation (30 days retention).

```bash
# View logs
docker compose logs -f app

# Or access log files directly
ls -la ./logs/
tail -f ./logs/app-*.log
```

---

## Troubleshooting

### Container Won't Start

```bash
# Check logs
docker compose logs app

# Common issues:
# - Missing JWT_SECRET
# - Docker socket not accessible
# - Port already in use
```

### Cannot Connect to Docker

```bash
# Linux: Check socket permissions
ls -la /var/run/docker.sock
sudo chmod 666 /var/run/docker.sock

# Windows: Ensure Docker Desktop is running
# Check "Expose daemon on tcp://localhost:2375" in settings
```

### Database Locked

SQLite can have issues with concurrent access:

```bash
# Stop container
docker compose stop

# Check for .db-wal and .db-shm files
ls -la ./data/

# Restart
docker compose start
```

### WebSocket Connection Failed

If real-time updates don't work:

1. Check reverse proxy WebSocket configuration
2. Ensure `/hub/*` paths are proxied with upgrade headers
3. Check browser console for connection errors

### Health Check Failing

```bash
# Test health endpoint
docker compose exec app curl http://localhost/health

# Check application logs
docker compose logs --tail 100 app
```

---

## Monitoring

### Health Endpoint

```
GET /health
```

Returns:
```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "docker": "Healthy"
  }
}
```

### Prometheus Metrics (Optional)

If enabled, metrics available at `/metrics`:

- `http_requests_total`
- `http_request_duration_seconds`
- `docker_containers_total`

### Recommended Alerts

- Health check failures for > 5 minutes
- High error rate (> 5% 5xx responses)
- Docker daemon unreachable
- Disk space < 10%

---

## Version Information

Check running version:

```bash
# API endpoint
curl http://localhost:3030/api/system/version

# Or check container labels
docker inspect docker-compose-manager --format '{{.Config.Labels}}'
```
