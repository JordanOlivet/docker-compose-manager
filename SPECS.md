This repository host 2 applications, docker-compose-manager-back and docker-compose-manager-front.

These applications have to work together to provide a docker/docker-compose management.
This stack is primaraly designed to run in docker containers and so both app should be build into a docker image.
Also a compose file aggregate both services to be able to start the entire stack by running docker compose up.
The stack is cross-platform and should be executable on Windows and Linux at least.

For testing debugging the stack should be able to be run locally on the developper computer OS.

Here is the specs for those two apps.

1. docker-compose-manager-back

This is an API service application in .Net 9 and C#.
This service is in charge of interfacing the docker engine running on the host with the web app but also expose all the functionalities necessary to manager docker compose files via the wab app.
This is done by exposing REST API routes.

So the app should provide the following functionalities :

* **Authentication & Authorization:**
  * POST /api/auth/login - User authentication with username/password, returns JWT token
  * POST /api/auth/logout - Invalidate user session
  * POST /api/auth/refresh - Refresh JWT token
  * GET /api/auth/me - Get current user profile information
  * JWT-based authentication middleware for protected routes
  * Role-based authorization middleware (admin, user roles)
* **User Management API (Admin Only):**
  * GET /api/users - List all users with pagination, filtering, and sorting
  * GET /api/users/{id} - Get specific user details
  * POST /api/users - Create new user with username, password, and role
  * PUT /api/users/{id} - Update user information (role, status, password)
  * DELETE /api/users/{id} - Delete user
  * PUT /api/users/{id}/enable - Enable user account
  * PUT /api/users/{id}/disable - Disable user account
  * GET /api/roles - List available roles and permissions
  * POST /api/roles - Create custom role with specific permissions
  * PUT /api/roles/{id} - Update role permissions
* **User Profile API:**
  * GET /api/profile - Get current user's profile
  * PUT /api/profile - Update current user's profile
  * PUT /api/profile/password - Change current user's password
* **Docker Container Management API:**
  * GET /api/containers - List all containers with filters (status, name, image)
  * GET /api/containers/{id} - Get detailed container information (env vars, mounts, networks, ports)
  * POST /api/containers/{id}/start - Start a container
  * POST /api/containers/{id}/stop - Stop a container
  * POST /api/containers/{id}/restart - Restart a container
  * DELETE /api/containers/{id} - Remove a container
  * GET /api/containers/{id}/logs - Stream container logs (with tail, follow, timestamps options)
  * GET /api/containers/{id}/stats - Get real-time container statistics (CPU, memory, network, I/O)
  * POST /api/containers/bulk-action - Perform bulk actions on multiple containers
* **Docker Compose API:**
  * GET /api/compose/projects - List all compose projects from configured directories
  * GET /api/compose/projects/{projectName} - Get compose project details and service status
  * POST /api/compose/projects/{projectName}/up - Execute docker-compose up (with options: detached, build, force-recreate)
  * POST /api/compose/projects/{projectName}/down - Execute docker-compose down (with options: volumes, images)
  * POST /api/compose/projects/{projectName}/start - Start compose services
  * POST /api/compose/projects/{projectName}/stop - Stop compose services
  * POST /api/compose/projects/{projectName}/restart - Restart compose services
  * GET /api/compose/projects/{projectName}/logs - Stream compose project logs
  * GET /api/compose/projects/{projectName}/ps - Get status of all services in compose project
* **Compose File Management API:**
  * GET /api/compose/files - List all compose files in configured directories
  * GET /api/compose/files/{path} - Read specific compose file content
  * POST /api/compose/files - Create new compose file
  * PUT /api/compose/files/{path} - Update/edit compose file content
  * DELETE /api/compose/files/{path} - Delete compose file
  * POST /api/compose/files/{path}/validate - Validate YAML syntax and docker-compose structure
  * POST /api/compose/files/{path}/duplicate - Duplicate/clone compose file
  * GET /api/compose/files/{path}/download - Download compose file
  * GET /api/compose/templates - Get available compose file templates
* **Dashboard API:**
  * GET /api/dashboard/stats - Get aggregated statistics (total containers, running, stopped, compose projects)
  * GET /api/dashboard/system - Get system resource usage (CPU, memory, disk)
  * GET /api/dashboard/activity - Get recent activity/events log
  * GET /api/dashboard/health - Get health status of all services
* **Configuration API (Admin Only):**
  * GET /api/config/paths - Get list of configured compose file paths
  * POST /api/config/paths - Add new compose file path
  * PUT /api/config/paths/{id} - Update compose file path
  * DELETE /api/config/paths/{id} - Remove compose file path
  * GET /api/config/settings - Get application settings
  * PUT /api/config/settings - Update application settings (compose defaults, refresh intervals, log retention, docker connection)
  * GET /api/config/security - Get security settings (session timeout, password policies)
  * PUT /api/config/security - Update security settings
* **Real-Time Updates:**
  * WebSocket endpoint /ws for real-time container status updates
  * WebSocket endpoint /ws/logs for real-time log streaming
  * Server-Sent Events (SSE) as fallback for real-time updates
* **Activity & Audit Logging:**
  * Log all user actions (container operations, file modifications, config changes)
  * GET /api/audit/logs - Retrieve audit logs with filtering (admin only)
* **Security Features:**
  * Input validation and sanitization for all endpoints
  * Secure file path handling with whitelist validation to prevent directory traversal
  * Rate limiting on authentication endpoints
  * CORS configuration for frontend
  * Password hashing using bcrypt or similar
  * SQL injection prevention (parameterized queries if using database)
* **Cross-Platform Support:**
  * Support for Windows Docker Desktop (named pipe: npipe:////./pipe/docker_engine)
  * Support for Linux Docker daemon (unix socket: unix:///var/run/docker.sock)
  * Automatic detection of platform and Docker connection method
* **Error Handling:**
  * Standardized error response format with error codes
  * Proper HTTP status codes for different scenarios
  * Detailed error messages for debugging (development mode)
  * User-friendly error messages (production mode)

**Data Storage System:**

The backend uses **SQLite** as the primary database for storing application data.

* **Why SQLite:**
  * Simple deployment - single file database, no separate container required
  * Lightweight and efficient for the application's data needs
  * Perfect for moderate concurrency (typical usage: <50 simultaneous users)
  * Zero configuration - works out-of-the-box on Windows and Linux
  * Easy backup and restore (simple file copy)
  * Low resource consumption
  * ACID transactions for data integrity
  * Excellent .NET support via Entity Framework Core

* **Database Schema:**
  * **Users** table - User accounts, credentials (hashed passwords), roles, status, created/updated timestamps
  * **Roles** table - Role definitions with associated permissions
  * **ComposePaths** table - Configured paths where compose files are stored
  * **AppSettings** table - Application configuration (key-value pairs)
  * **AuditLogs** table - User activity tracking (user, action, timestamp, details, IP address)
  * **Sessions** table - Active user sessions for JWT token management

* **ORM and Database Access:**
  * Use **Entity Framework Core** for database access
  * Repository pattern for abstraction and testability
  * Database migrations for version control of schema changes
  * Configurable connection string in appsettings.json

* **Migration Path:**
  * Architecture designed to support migration to PostgreSQL if needed in the future
  * Database type configurable via settings
  * Example scenarios requiring PostgreSQL:
    * High concurrency (>50 simultaneous users)
    * Need for database replication or high availability
    * Very large audit log volumes requiring advanced query optimization
    * Requirement for multi-instance deployment with shared database

* **Database Location:**
  * Development: Local file in project directory (Data/app.db)
  * Docker: Mounted volume to persist data across container restarts
  * Configuration example:
    ```json
    {
      "Database": {
        "Type": "SQLite",
        "ConnectionString": "Data Source=/app/data/app.db"
      }
    }
    ```

2. docker-compose-manager-front

This is a web app in React.
This app should provide the following functionalities :

* **Authentication and User Management:**
  * Login/logout functionality with session management
  * User profile page (view and edit own profile)
  * Admin-only user management interface:
    * List all users with their roles and permissions
    * Create new users with username, password, and role assignment
    * Edit existing users (change role, enable/disable, reset password)
    * Delete users
    * Define user roles (admin, user) and custom permissions
* **Dashboard (Main Page):**
  * Overview of all containers with status (running, stopped, exited)
  * System resource usage (CPU, memory, disk)
  * Quick stats: total containers, running containers, stopped containers
  * Quick stats: total compose projects, active projects
  * Recent activity/logs feed
  * Health status indicators for services
  * Search and filter capabilities
* **Container Management:**
  * List all containers with detailed information (name, image, status, ports, created date)
  * Filter containers by status, name, or image
  * Container actions: start, stop, restart, delete (with confirmation)
  * View container details (environment variables, mounts, networks)
  * View real-time container logs with auto-refresh option
  * View container statistics (CPU, memory, network usage)
  * Bulk actions on multiple containers
* **Docker Compose Management:**
  * List all compose files from configured directories
  * View compose file structure and services
  * Compose project actions:
    * Compose up (with options: detached, build, force-recreate)
    * Compose down (with options: remove volumes, remove images)
    * Compose start/stop/restart
    * View compose project status and service health
  * Real-time logs for compose services
* **Compose File Editor:**
  * Browse and search compose files in configured paths
  * Create new compose files with template options
  * Edit existing compose files with:
    * Syntax highlighting for YAML
    * Real-time validation and error detection
    * Auto-completion for common docker-compose directives
    * Preview mode before saving
  * Delete compose files (with confirmation)
  * Duplicate/clone compose files
  * Download compose files to local machine
* **Configuration (Admin Only):**
  * Manage paths where compose files are stored (add, edit, remove)
  * Configure application settings:
    * Default compose command options
    * Auto-refresh intervals for dashboard
    * Log retention settings
    * Docker engine connection settings
  * User role and permission configuration
  * Security settings (session timeout, password policies)
* **UI/UX Features:**
  * Responsive design for desktop and tablet
  * Dark/light theme toggle
  * Real-time updates using WebSockets or polling
  * Notifications for long-running operations
  * Confirmation dialogs for destructive actions
  * Loading states and progress indicators
  * Error handling with user-friendly messages
  * Breadcrumb navigation
  * Keyboard shortcuts for common actions