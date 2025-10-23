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

1. Copy `.env.example` to `.env` and configure:
```bash
cp .env.example .env
```

2. Build and start all services:
```bash
docker compose up --build
```

3. Access the application at `http://localhost:3000`

4. Default credentials:
   - Username: `admin`
   - Password: `admin`

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

## API Endpoints

### Authentication
- `POST /api/auth/login` - User login
- `POST /api/auth/logout` - User logout
- `POST /api/auth/refresh` - Refresh access token
- `GET /api/auth/me` - Get current user
- `PUT /api/auth/change-password` - Change password

### Containers
- `GET /api/containers` - List all containers
- `GET /api/containers/{id}` - Get container details
- `POST /api/containers/{id}/start` - Start container
- `POST /api/containers/{id}/stop` - Stop container
- `POST /api/containers/{id}/restart` - Restart container
- `DELETE /api/containers/{id}` - Remove container

## Next Steps

The base architecture is ready. See `SPECS.md` for the complete feature roadmap including:

- Docker Compose file management
- User management (admin functionality)
- Real-time updates via WebSockets
- Container logs streaming
- Dashboard with statistics
- And much more...

## Security Notes

⚠️ **Important**: The backend requires access to the Docker socket, which grants root-level access to the host system. Only deploy in trusted environments.

## License

MIT
