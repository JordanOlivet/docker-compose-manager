# Guide de Déploiement - Docker Compose Manager

Ce guide explique comment déployer l'application Docker Compose Manager en utilisant les images Docker pré-buildées depuis GitHub Container Registry.

## Table des Matières

1. [Configuration Initiale](#configuration-initiale)
2. [Rendre les Images Publiques](#rendre-les-images-publiques)
3. [Déploiement avec Docker Compose](#déploiement-avec-docker-compose)
4. [Configuration de Production](#configuration-de-production)
5. [Mise à Jour](#mise-à-jour)
6. [Rollback](#rollback)
7. [Monitoring et Logs](#monitoring-et-logs)

## Configuration Initiale

### 1. Prérequis

- Docker Engine 20.10+ ou Docker Desktop
- Docker Compose v2+
- Accès SSH au serveur (pour déploiement distant)
- Compte GitHub avec le repository

### 2. Cloner le Repository

```bash
git clone https://github.com/your-username/docker-compose-manager.git
cd docker-compose-manager
```

### 3. Configurer les Variables d'Environnement

```bash
# Copier le fichier exemple
cp .env.example .env

# Éditer le fichier .env
nano .env
```

Configurez les variables suivantes:

```bash
# Repository GitHub
GITHUB_REPOSITORY=your-username/docker-compose-manager

# Tag de l'image (latest, 1.0.0, sha-abc1234, etc.)
IMAGE_TAG=latest

# JWT Secret - IMPORTANT: Générez une clé forte!
# Générer avec: openssl rand -base64 32
JWT_SECRET=your-super-secret-jwt-key-min-32-characters

# Niveau de log (pour production: Information ou Warning)
LOG_LEVEL=Information
```

## Rendre les Images Publiques

Par défaut, les images publiées sur GitHub Container Registry sont privées. Pour les rendre publiques:

### Via l'Interface GitHub

1. Allez sur votre profil GitHub
2. Cliquez sur **Packages**
3. Sélectionnez le package `docker-compose-manager-backend`
4. Cliquez sur **Package settings** (en bas à droite)
5. Scrollez jusqu'à **Danger Zone**
6. Cliquez sur **Change visibility**
7. Sélectionnez **Public** et confirmez
8. Répétez pour `docker-compose-manager-frontend`

### Via GitHub CLI

```bash
# Installer GitHub CLI si nécessaire
# https://cli.github.com/

# Authentification
gh auth login

# Rendre les packages publics
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

## Déploiement avec Docker Compose

### Déploiement Local ou Serveur Unique

```bash
# 1. Authentification (si images privées)
echo $GITHUB_TOKEN | docker login ghcr.io -u your-username --password-stdin

# 2. Pull des images
docker compose pull

# 3. Démarrer les services
docker compose up -d

# 4. Vérifier le statut
docker compose ps

# 5. Voir les logs
docker compose logs -f
```

### Accès à l'Application

L'application sera accessible sur `http://localhost:3000` ou `http://your-server-ip:3000`

Identifiants par défaut:
- Username: `admin`
- Password: `admin`

**Important**: Changez le mot de passe dès la première connexion!

## Configuration de Production

### 1. Reverse Proxy avec Nginx

Pour exposer l'application avec un nom de domaine et SSL:

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

Activez SSL avec Let's Encrypt:

```bash
sudo certbot --nginx -d docker-manager.example.com
```

### 2. Modifier le Port d'Exposition

Éditez `docker-compose.yml` pour changer le port:

```yaml
services:
  frontend:
    ports:
      - "8080:80"  # Change 3000 to your desired port
```

### 3. Sécuriser l'Accès au Socket Docker

Sur le serveur de production, assurez-vous que seul le container backend a accès:

```bash
# Vérifier les permissions
ls -l /var/run/docker.sock

# Si nécessaire, créer un groupe docker
sudo groupadd docker
sudo usermod -aG docker $USER
```

### 4. Backup de la Base de Données

La base de données SQLite est stockée dans un volume Docker. Pour sauvegarder:

```bash
# Créer un backup
docker compose exec backend sh -c 'cp /app/data/app.db /app/data/app.db.backup'

# Copier le backup hors du container
docker cp docker-manager-backend:/app/data/app.db.backup ./backup-$(date +%Y%m%d).db

# Automatiser avec cron
0 2 * * * cd /path/to/docker-compose-manager && docker compose exec backend sh -c 'cp /app/data/app.db /app/data/app.db.backup'
```

## Mise à Jour

### Mise à Jour vers la Dernière Version

```bash
# 1. Sauvegarder la base de données
docker compose exec backend sh -c 'cp /app/data/app.db /app/data/app.db.backup'

# 2. Pull des nouvelles images
docker compose pull

# 3. Redémarrer avec les nouvelles images
docker compose up -d

# 4. Vérifier les logs
docker compose logs -f

# 5. Vérifier que l'application fonctionne
curl http://localhost:3000/health
```

### Mise à Jour vers une Version Spécifique

```bash
# 1. Éditer .env
echo "IMAGE_TAG=1.2.3" >> .env

# 2. Pull et redémarrer
docker compose pull
docker compose up -d
```

## Rollback

Si une mise à jour pose problème:

```bash
# 1. Changer vers la version précédente dans .env
IMAGE_TAG=1.2.2  # version stable précédente

# 2. Pull de l'ancienne version
docker compose pull

# 3. Redémarrer
docker compose down
docker compose up -d

# 4. Restaurer le backup si nécessaire
docker cp backup-20240115.db docker-manager-backend:/app/data/app.db
docker compose restart backend
```

## Monitoring et Logs

### Voir les Logs

```bash
# Tous les services
docker compose logs -f

# Backend uniquement
docker compose logs -f backend

# Frontend uniquement
docker compose logs -f frontend

# Dernières 100 lignes
docker compose logs --tail=100

# Logs avec timestamps
docker compose logs -f -t
```

### Vérifier la Santé des Services

```bash
# Status des containers
docker compose ps

# Stats en temps réel
docker stats

# Utilisation de l'espace disque
docker system df
```

### Logs Persistants

Les logs du backend sont sauvegardés dans `./logs/backend/`:

```bash
# Voir les logs fichiers
tail -f ./logs/backend/app-*.log

# Archiver les anciens logs
tar -czf logs-archive-$(date +%Y%m%d).tar.gz ./logs/backend/
```

## Commandes Utiles

### Gestion des Containers

```bash
# Arrêter les services
docker compose stop

# Démarrer les services
docker compose start

# Redémarrer les services
docker compose restart

# Arrêter et supprimer les containers
docker compose down

# Supprimer containers + volumes (ATTENTION: perte de données!)
docker compose down -v
```

### Nettoyage

```bash
# Supprimer les images inutilisées
docker image prune -a

# Supprimer tous les containers arrêtés
docker container prune

# Nettoyage complet (ATTENTION)
docker system prune -a --volumes
```

### Débug

```bash
# Accéder au shell du backend
docker compose exec backend sh

# Accéder au shell du frontend
docker compose exec frontend sh

# Inspecter un container
docker inspect docker-manager-backend

# Voir les variables d'environnement
docker compose exec backend env
```

## Sécurité en Production

### Checklist de Sécurité

- [ ] Changé le mot de passe admin par défaut
- [ ] Généré un JWT_SECRET fort (min 32 caractères)
- [ ] Configuré HTTPS avec certificat SSL
- [ ] Restreint l'accès au port 3000 (firewall)
- [ ] Configuré des backups réguliers
- [ ] Activé les logs et monitoring
- [ ] Mise à jour régulière des images
- [ ] Sécurisé l'accès au socket Docker
- [ ] Configuré rate limiting (si exposé publiquement)

### Mise à Jour des Secrets

Si vous devez changer le JWT_SECRET:

```bash
# 1. Éditer .env avec le nouveau secret
nano .env

# 2. Redémarrer le backend
docker compose restart backend

# Note: Tous les utilisateurs devront se reconnecter
```

## Support

Pour plus d'informations:
- Documentation: voir `README.md` et `CLAUDE.md`
- Issues: https://github.com/your-username/docker-compose-manager/issues
- Workflow CI/CD: `.github/workflows/docker-build-publish.yml`
