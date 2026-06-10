# Dev Setup

One-shot bootstrap to go from a fresh machine to a working development environment
for Lighthouse. Cross-platform: **Windows**, **Linux**, **macOS**.

## TL;DR

```bash
# Windows (PowerShell)
.\dev-setup\setup.ps1
.\run.ps1

# Linux / macOS
chmod +x dev-setup/setup.sh run.sh    # first time only
./dev-setup/setup.sh
./run.sh
```

Then open <http://localhost:5173> and log in with **`admin` / `adminadmin`**
(you must change the password on first login).

## What the setup script does

`setup.ps1` / `setup.sh` is **install + configure only — it never launches the app**:

1. **Detects prerequisites** and prints each version (or `MISSING`):
   - git, .NET SDK (>= 10), Node.js (>= 20), npm, Docker (+ daemon reachable).
2. **Prompts before installing** anything missing (hybrid — nothing is installed
   without a `Y`). Uses `winget`/`choco` on Windows, `brew` on macOS, and
   `apt`/`dnf`/`pacman` on Linux. If no package manager is found it prints the
   manual download link instead.
3. **Generates gitignored local dev config** (idempotent — existing files are kept):
   - `lighthouse-back/lighthouse-back/appsettings.Development.local.json`
     with `ComposeDiscovery:HostPathMapping` pointing at
     `dev-setup/sample-compose-files`.
   - `lighthouse-front/.env.development` with
     `VITE_API_URL=http://localhost:5050`.
4. **Installs dependencies**: `dotnet restore` (backend) and `npm install` (frontend).
5. **Optional prompts**: install the `dotnet-ef` global tool (only needed to *author*
   DB migrations — running the app auto-applies them) and, on Windows/macOS, trust
   the .NET HTTPS dev cert.

## Building & running

Use the repo-root run scripts (separate from setup, so you can rebuild and relaunch
anytime without re-running setup):

```bash
.\run.ps1     # Windows
./run.sh      # Linux / macOS
```

These **build both apps**, then **launch each in its own terminal window** (so
the logs stay separate):

| App      | Command                                                              | URL                     |
|----------|---------------------------------------------------------------------|-------------------------|
| Backend  | `dotnet watch run --launch-profile http`                            | <http://localhost:5050> |
| Frontend | `npm run dev`                                                       | <http://localhost:5173> |

Close a window (or press **Ctrl-C** in it) to stop that app. The backend
auto-applies EF migrations on startup and seeds the default admin user.

> **macOS / Linux:** separate windows require a GUI terminal (Terminal.app, or
> `gnome-terminal`/`konsole`/`xterm`). With no graphical session (e.g. SSH), the
> script falls back to running both in the current terminal with combined logs —
> there, **Ctrl-C stops both**.

### Running each app manually

```bash
# Backend
cd lighthouse-back
dotnet watch run --project lighthouse-back --launch-profile http

# Frontend (separate terminal)
cd lighthouse-front
npm run dev
```

## Manual setup (if you decline auto-install)

| Tool        | Required | Get it                                                       |
|-------------|----------|--------------------------------------------------------------|
| Git         | yes      | <https://git-scm.com/downloads>                              |
| .NET SDK 10 | yes      | <https://dotnet.microsoft.com/download/dotnet/10.0>          |
| Node.js 20+ | yes      | <https://nodejs.org/>                                        |
| Docker      | yes      | <https://www.docker.com/products/docker-desktop/>           |

Then create the two config files listed in step 3 above and run
`dotnet restore` + `npm install`.

## Ports

| Port | Used by                                  |
|------|------------------------------------------|
| 5050 | Backend API (dev)                        |
| 5173 | Frontend dev server (Vite)               |
| 3030 | Unified container (`docker compose up`)  |

## Troubleshooting

- **Backend can't reach Docker** — Linux/macOS use `unix:///var/run/docker.sock`,
  Windows uses `npipe://./pipe/docker_engine` (already set in
  `appsettings.Development.json`). On Linux you may need
  `sudo chmod 666 /var/run/docker.sock` or to be in the `docker` group.
- **Frontend can't reach the backend** — dev runs over **HTTP**; confirm
  `lighthouse-front/.env.development` has
  `VITE_API_URL=http://localhost:5050` and that the backend is started with the
  `http` launch profile. If you prefer HTTPS, trust the dev cert
  (`dotnet dev-certs https --trust`) and set the URL to `https://localhost:5050`.
- **"Database is locked"** — SQLite is single-writer; close any tool (e.g. DB
  Browser) holding `Data/app.db`.
- **PATH not updated after install** — open a new terminal and re-run the setup script.
- **Compose Discovery shows nothing** — confirm `HostPathMapping` in
  `appsettings.Development.local.json` points at a real folder with `.yml`/`.yaml`
  compose files (the sample `dev-setup/sample-compose-files` is used by default).

See also: [../CONTRIBUTING.md](../CONTRIBUTING.md) and the project [CLAUDE.md](../CLAUDE.md).
