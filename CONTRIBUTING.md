# Contributing

Thanks for contributing to Lighthouse! This guide gets you from a
fresh clone to a working dev loop and explains how changes get merged and released.

## 1. Get set up

Run the one-shot bootstrap (installs prerequisites, generates local dev config,
installs dependencies):

```bash
# Windows (PowerShell)
.\dev-setup\setup.ps1

# Linux / macOS
chmod +x dev-setup/setup.sh run.sh   # first time only
./dev-setup/setup.sh
```

Prerequisites it checks/installs: **Git**, **.NET SDK 10**, **Node.js 20+**,
**Docker**. Details and manual steps: [dev-setup/README.md](dev-setup/README.md).

## 2. Run the app

```bash
.\run.ps1     # Windows (or double-click run.cmd)
./run.sh      # Linux / macOS
```

> **Windows tip:** to launch by double-clicking, use `run.cmd` rather than the
> `.ps1`. Double-clicking the `.ps1` (or "Run with PowerShell") tends to close the
> window instantly on error; `run.cmd` keeps it open until you press a key.

This builds both apps then launches each in its own terminal window (so logs stay
separate; on macOS/Linux without a GUI terminal it falls back to combined logs):

- Backend → <http://localhost:5050> (`dotnet watch run`, HTTP dev profile)
- Frontend → <http://localhost:5173> (`npm run dev`)
- Login: **`admin` / `adminadmin`** (must change on first login)

You can also run each app on its own — see [dev-setup/README.md](dev-setup/README.md).

## 3. Project layout

```
lighthouse-back/     .NET 10 Web API (Controllers, Services, Data, ...)
lighthouse-front/    SvelteKit + Svelte 5 frontend (routes, lib, ...)
dev-setup/                       Dev bootstrap scripts + sample compose files
run.{ps1,sh}                     Build + launch both apps
run.cmd                          Windows double-click launcher for run.ps1
build-check.{ps1,sh}             Compile backend + type-check frontend
build-check.cmd                  Windows double-click launcher for build-check.ps1
```

Architecture, key services, and conventions are documented in
[CLAUDE.md](CLAUDE.md) and [SPECS.md](SPECS.md). Don't duplicate them here — read
those for the deep dive.

## 4. Before you push

Run the build/check script and the backend tests:

```bash
.\build-check.ps1            # or ./build-check.sh  -> Backend build + Frontend check
cd lighthouse-back && dotnet test
```

> **Windows tip:** if you'd rather double-click than run it from a terminal, use
> `build-check.cmd` instead of the `.ps1`. Double-clicking the `.ps1` directly
> (or "Run with PowerShell") tends to close the window instantly on error;
> `build-check.cmd` keeps it open until you press a key, whatever happens.

- Backend tests live in `lighthouse-back.Tests/` (xUnit + Moq +
  FluentAssertions). Add tests for new services/controllers.
- The frontend has no automated test suite yet; run `npm run check` (also covered
  by `build-check`) and verify manually.

### Database migrations

Migrations apply automatically on startup. You only need the EF CLI to **author**
a migration:

```bash
cd lighthouse-back
dotnet ef migrations add MyMigration --project lighthouse-back
```

`dev-setup` can install the `dotnet-ef` tool for you on request.

## 5. Branches, PRs & releases

- Branch off `main` with a descriptive name (e.g. `feat/...`, `fix/...`, `chore/...`).
- Keep PRs focused; describe the change and how you tested it.
- **Releases are automated from PR labels.** Apply exactly one of:
  - `release-major` — breaking changes
  - `release-minor` — new features
  - `release-patch` — bug fixes

  On merge to `main`, CI bumps the `VERSION` file, builds the image, and publishes
  a GitHub release. See [RELEASING.md](RELEASING.md) for the full flow.

## 6. Notes & conventions

- **Dev runs over HTTP** (`VITE_API_URL=http://localhost:5050`) to avoid dev-cert
  friction. HTTPS is optional — trust the cert with `dotnet dev-certs https --trust`
  and point the frontend at `https://localhost:5050`.
- Per-developer config goes in the gitignored
  `appsettings.Development.local.json` and `.env.development` — never commit machine
  paths or secrets.
- Match the style of surrounding code (backend C#, frontend Svelte 5 runes).
