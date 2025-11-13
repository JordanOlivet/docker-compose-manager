# Deployment Guide - Docker Compose Manager

This guide explains how to deploy the Docker Compose Manager application using pre-built Docker images from GitHub Container Registry.

## Table of Contents

1. [Initial Setup](#initial-setup)
2. [Making Images Public](#making-images-public)
3. [Deploying with Docker Compose](#deploying-with-docker-compose)
4. [Production Configuration](#production-configuration)
5. [Updating](#updating)
6. [Rollback](#rollback)
7. [Monitoring and Logs](#monitoring-and-logs)

## Initial Setup

### 1. Prerequisites

- Docker Engine 20.10+ or Docker Desktop
- Docker Compose v2+
- SSH access to the server (for remote deployment)
- GitHub account with repository access

### 2. Clone the Repository

```bash
git clone https://github.com/your-username/docker-compose-manager.git
cd docker-compose-manager
```

### 3. Configure Environment Variables

```bash
# Copy example file
cp .env.example .env

# Edit the .env file
nano .env
```

Set the following variables:

```bash
# GitHub repository
GITHUB_REPOSITORY=your-username/docker-compose-manager

# Image tag (latest, 1.0.0, sha-abc1234, etc.)
IMAGE_TAG=latest

# JWT Secret - IMPORTANT: Generate a strong key!
# Generate with: openssl rand -base64 32
JWT_SECRET=your-super-secret-jwt-key-min-32-characters

# Log level (production recommendation: Information or Warning)
LOG_LEVEL=Information
```

## Making Images Public

By default, images published to GitHub Container Registry are private. To make them public:

### Via GitHub Web Interface

1. Go to your GitHub profile
2. Click **Packages**
3. Select the `docker-compose-manager-backend` package
4. Click **Package settings** (bottom right)
5. Scroll to **Danger Zone**
6. Click **Change visibility**
7. Select **Public** and confirm
8. Repeat for `docker-compose-manager-frontend`

### Via GitHub CLI

```bash
# Install GitHub CLI if needed
# https://cli.github.com/

# Authenticate
gh auth login

# Make packages public
gh api \
  --method PATCH \
  -H "Accept: application/vnd.github+json" \
  /user/packages/container/docker-compose-manager-backend/versions/VERSION_ID \
  -f visibility='public'

gh api \
  --method PATCH \
  -H "Accept: application/vnd.github+json" \
  /user/packages/container/docker-compose-manager-frontend/versions/VERSION_ID \
  -f visibility='public'
```

## Deploying with Docker Compose

### Local or Single Server Deployment

```bash
# 1. Authenticate (if images are private)
echo $GITHUB_TOKEN | docker login ghcr.io -u your-username --password-stdin

# 2. Pull images
docker compose pull

# 3. Start services
docker compose up -d

# 4. Check status
docker compose ps

# 5. View logs
docker compose logs -f
```

### Application Access

The application will be accessible at `http://localhost:3000` or `http://your-server-ip:3000`.

Default credentials:
- Username: `admin`
- Password: `admin`

**Important**: Change the password immediately after first login!

## Production Configuration

### 1. Reverse Proxy with Nginx

To expose the application with a domain name and SSL:

```nginx
# /etc/nginx/sites-available/docker-manager

server {
    listen 80;
    server_name docker-manager.example.com;

    location / {
        proxy_pass http://localhost:3000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # WebSocket support
    location /hubs/ {
        proxy_pass http://localhost:3000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
    }
}
```

Enable SSL with Let's Encrypt:

```bash
sudo certbot --nginx -d docker-manager.example.com
```

### 2. Change Exposed Port

Edit `docker-compose.yml` to change the port:

```yaml
services:
  frontend:
    ports:
      - "8080:80"  # Change 3000 to your desired port
```

### 3. Secure Docker Socket Access

On the production server, ensure only the backend container has access:

```bash
# Check permissions
ls -l /var/run/docker.sock

# If needed, create a docker group
sudo groupadd docker
sudo usermod -aG docker $USER
```

### 4. Database Backup

The SQLite database is stored in a Docker volume. To back it up:

```bash
# Create a backup
docker compose exec backend sh -c 'cp /app/data/app.db /app/data/app.db.backup'

# Copy the backup out of the container
docker cp docker-manager-backend:/app/data/app.db.backup ./backup-$(date +%Y%m%d).db

# Automate with cron
0 2 * * * cd /path/to/docker-compose-manager && docker compose exec backend sh -c 'cp /app/data/app.db /app/data/app.db.backup'
```

## Updating

### Update to Latest Version

```bash
# 1. Backup the database
docker compose exec backend sh -c 'cp /app/data/app.db /app/data/app.db.backup'

# 2. Pull new images
docker compose pull

# 3. Restart with new images
docker compose up -d

# 4. Check logs
docker compose logs -f

# 5. Verify application health
curl http://localhost:3000/health
```

### Update to a Specific Version

```bash
# 1. Edit .env
echo "IMAGE_TAG=1.2.3" >> .env

# 2. Pull and restart
docker compose pull
docker compose up -d
```

## Rollback

If an update causes issues:

```bash
# 1. Switch to previous version in .env
IMAGE_TAG=1.2.2  # previous stable version

# 2. Pull previous version
docker compose pull

# 3. Restart
docker compose down
docker compose up -d

# 4. Restore backup if needed
docker cp backup-20240115.db docker-manager-backend:/app/data/app.db
docker compose restart backend
```

## Monitoring and Logs

### View Logs

```bash
# All services
docker compose logs -f

# Backend only
docker compose logs -f backend

# Frontend only
docker compose logs -f frontend

# Last 100 lines
docker compose logs --tail=100

# Logs with timestamps
docker compose logs -f -t
```

### Check Service Health

```bash
# Container status
docker compose ps

# Real-time stats
docker stats

# Disk usage
docker system df
```

### Persistent Logs

Backend logs are stored in `./logs/backend/`:

```bash
# View log files
tail -f ./logs/backend/app-*.log

# Archive old logs
tar -czf logs-archive-$(date +%Y%m%d).tar.gz ./logs/backend/
```

## Useful Commands

### Container Management

```bash
# Stop services
docker compose stop

# Start services
docker compose start

# Restart services
docker compose restart

# Stop and remove containers
docker compose down

# Remove containers + volumes (WARNING: data loss!)
docker compose down -v
```

### Cleanup

```bash
# Remove unused images
docker image prune -a

# Remove stopped containers
docker container prune

# Full cleanup (WARNING)
docker system prune -a --volumes
```

### Debug

```bash
# Access backend shell
docker compose exec backend sh

# Access frontend shell
docker compose exec frontend sh

# Inspect a container
docker inspect docker-manager-backend

# View environment variables
docker compose exec backend env
```

## Production Security

### Security Checklist

- [ ] Changed default admin password
- [ ] Generated a strong JWT_SECRET (min 32 characters)
- [ ] Configured HTTPS with SSL certificate
- [ ] Restricted access to port 3000 (firewall)
- [ ] Set up regular backups
- [ ] Enabled logging and monitoring
- [ ] Regularly updated images
- [ ] Secured access to Docker socket
- [ ] Configured rate limiting (if publicly exposed)

### Updating Secrets

If you need to change the JWT_SECRET:

```bash
# 1. Edit .env with new secret
nano .env

# 2. Restart backend
docker compose restart backend

# Note: All users will need to log in again
```

## Support

For more information:
- Documentation: see `README.md` and `CLAUDE.md`
- Issues: https://github.com/your-username/docker-compose-manager/issues
- CI/CD Workflow: `.github/workflows/docker-build-publish.yml`
