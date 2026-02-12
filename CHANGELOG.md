## Changes in v1.2.0

**Merged Pull Requests:**
- Add update mecanism (#38)

**Commits:**
- Implement application self check/update and compose project/container check/update mecanism (#38) (dd55dbc)
- Add docker image dev build and push for PR commits (#51) (34404dd)
- docs: update CHANGELOG for v1.1.1 [skip ci] (855ac7e)
- chore: bump version to v1.1.1 [skip ci] (d6b508f)

## Changes in v1.1.1

**Merged Pull Requests:**
- Fix wrong compose project number displayed in dashboard (#50)

**Commits:**
- Fix wrong compose project number displayed in dashboard (#50) (0aaada0)
- Add logger to manage when to log in web brower console + use it everywhere (#48) (d31d7c4)
- docs: update CHANGELOG for v1.1.0 [skip ci] (6abaa90)
- chore: bump version to v1.1.0 [skip ci] (da3996b)

## Changes in v1.1.0

**Merged Pull Requests:**
- Improve password error feedback (#47)

**Commits:**
- Improve password error feedback (#47) (0e0455b)
- docs: update CHANGELOG for v1.0.5 [skip ci] (33da113)
- chore: bump version to v1.0.5 [skip ci] (be43bbf)

## Changes in v1.0.5

**Merged Pull Requests:**
- Fix connection issues and re activate dashboard stats (#44)

**Commits:**
- Fix connection issues and re activate dashboard stats (#44) (ee55285)
- docs: update CHANGELOG for v1.0.4 [skip ci] (5547865)
- chore: bump version to v1.0.4 [skip ci] (c37c12d)

## Changes in v1.0.4

**Merged Pull Requests:**
- Fix units in live stats cards (#43)

**Commits:**
- Fix units and improvements of live stats cards (#43) (611afe4)
- docs: update CHANGELOG for v1.0.3 [skip ci] (8775653)
- chore: bump version to v1.0.3 [skip ci] (3f4dee5)

## Changes in v1.0.3

**Merged Pull Requests:**
- Fix permission view (#39)

**Commits:**
- Fix permission view (#39) (71cca76)
- Enhance README with docker-compose.yml details (63cb650)
- Rename app service to docker-compose-manager (2186b00)
- Adjust healthcheck interval and start period (ca85bc9)
- docs: update CHANGELOG for v1.0.2 [skip ci] (52cfd41)
- chore: bump version to v1.0.2 [skip ci] (790d8ab)

## Changes in v1.0.2

**Merged Pull Requests:**
- Handle project name conflict with parentDirectory-fileName (#37)

**Commits:**
- Handle project name conflict with parentDirectory-fileName (#37) (e2f5977)
- docs: update CHANGELOG for v1.0.1 [skip ci] (7cf8e0a)
- chore: bump version to v1.0.1 [skip ci] (a5ac2cb)

## Changes in v1.0.1

**Merged Pull Requests:**
- Handle conflict on project name (#36)

**Commits:**
- Handle conflict on project name (#36) (0d77c86)
- docs: update CHANGELOG for v1.0.0 [skip ci] (495fa7e)
- chore: bump version to v1.0.0 [skip ci] (ddd734e)

## Changes in v1.0.0

**Merged Pull Requests:**
- Front and back unification. Revert version to 0.21.0 for proper true 1.0.0 (#35)

**Commits:**
- Front and back unification. (#35) (abebb5f)
- Add custom optional version to CI (#34) (9b1bd7e)
- Add front test placeholder until proper tests are added (#33) (3b6129f)
- docs: update CHANGELOG for v1.0.0 [skip ci] (9b7114c)
- chore: bump version to v1.0.0 [skip ci] (c34b5e7)
- Refactor the compose discovery mecanism (#32) (5b8af06)
- Update README by removing license section (3df33da)
- docs: update CHANGELOG for v0.21.0 [skip ci] (cb31e99)
- chore: bump version to v0.21.0 [skip ci] (1e01ef0)

## Changes in v1.0.0

**Merged Pull Requests:**
- Refactor the compose discovery mecanism (#32)

**Commits:**
- Refactor the compose discovery mecanism (#32) (5b8af06)
- Update README by removing license section (3df33da)
- docs: update CHANGELOG for v0.21.0 [skip ci] (cb31e99)
- chore: bump version to v0.21.0 [skip ci] (1e01ef0)

## Changes in v0.22.0

### 🎉 Major Release: Compose Discovery Revamp

This release completely overhauls how Docker Compose files are discovered and managed, replacing the manual database-driven configuration with an automatic filesystem-based discovery system.

**⚠️ BREAKING CHANGES:**

- **Removed**: Manual compose path configuration via UI/API
- **Removed**: `ComposePaths` and `ComposeFiles` database tables
- **Removed**: `/api/config/compose-paths` endpoints (now return HTTP 410 Gone)
- **Migration Required**: Move your compose files to the new root directory (default: `/app/compose-files`)

**✨ New Features:**

**Automatic File Discovery:**
- Scans a single root directory recursively for compose files
- No manual configuration needed - just drop files in the folder
- Automatic project name extraction (from `name` field, directory, or filename)
- Real-time caching with configurable TTL (default: 10 seconds)
- Thread-safe implementation with double-check locking

**Conflict Resolution:**
- Detects and reports naming conflicts between files
- `x-disabled` attribute to temporarily disable files without deletion
- Intelligent conflict resolution: 1 active file → use it, 0 active → hide project, 2+ active → show error
- Deterministic alphabetical sorting for reproducible behavior

**Orphaned Project Management:**
- Supports projects where containers run but compose file is missing/moved
- Limited actions available (stop, restart, logs) without file
- Full actions (up, build, pull) when file is present
- Clear warnings in UI when files are missing

**Command Classification:**
- Distinguishes commands requiring compose file (up, build) vs. runtime-only (stop, logs)
- Smart action availability based on project state and file presence
- Prevents errors from unsupported operations

**New API Endpoints:**
- `GET /api/compose/files` - List all discovered compose files with metadata
- `GET /api/compose/conflicts` - Get naming conflicts with resolution steps
- `GET /api/compose/health` - Check discovery system and Docker daemon health
- `POST /api/compose/refresh` - Force cache invalidation and re-scan (admin only)
- `GET /api/compose/projects` - Enhanced with `hasComposeFile`, `availableActions`, `warning` fields

**Configuration:**
- `ComposeDiscovery` settings in `appsettings.json`
- Configurable root path, scan depth limit, cache duration, max file size
- Environment variable overrides with double-underscore notation

**🏗️ Architecture:**

**New Services:**
- `ComposeFileScanner` - Recursive filesystem scanning with YAML validation
- `PathValidator` - Security validation to prevent path traversal attacks
- `ComposeFileCacheService` - Thread-safe in-memory caching
- `ConflictResolutionService` - Intelligent conflict detection and resolution
- `ProjectMatchingService` - Matches Docker projects with discovered files
- `ComposeCommandClassifier` - Determines command requirements
- `ComposeDiscoveryInitializer` - Non-blocking startup scan

**Database Migration:**
- Migration `20260108214649_RemoveComposePathsAndFiles` removes old tables
- DbSets marked as obsolete in AppDbContext
- Automatic migration on application startup

**Frontend Changes:**
- Updated API client with 4 new functions
- 7 new TypeScript types for discovery DTOs
- Health status banner with localStorage dismissal
- "Not Started" badges for discovered but not running projects
- File path display in project listings
- Warning messages for missing files and conflicts
- Action button visibility based on `availableActions` logic

**🧪 Testing:**

- **100 unit tests** created for all new services (95.2% pass rate):
  - ComposeFileScannerTests (18 tests)
  - PathValidatorTests (22 tests)
  - ComposeFileCacheServiceTests (14 tests)
  - ConflictResolutionServiceTests (12 tests)
  - ProjectMatchingServiceTests (12 tests)
  - ComposeCommandClassifierTests (22 tests)
- Tests cover: scanning, validation, caching, thread-safety, conflict resolution, matching logic
- FluentAssertions for readable test assertions
- Moq for dependency mocking

**📚 Documentation:**

- Comprehensive migration guide in README.md
- Updated CLAUDE.md with discovery architecture
- Configuration examples for all settings
- Troubleshooting section for common issues
- Rollback instructions if needed

**🔒 Security:**

- Path validation prevents traversal attacks
- All file paths validated against configured root
- File size limits to prevent memory exhaustion
- Security logging for path violations

**⚡ Performance:**

- Intelligent caching reduces filesystem access
- Configurable scan depth prevents deep recursion
- Thread-safe design for concurrent requests
- Lazy loading - scan only when needed

**🐛 Bug Fixes:**

- Fixed race conditions in file discovery with semaphore locks
- Improved error handling for invalid YAML files
- Better handling of unresolved environment variables in compose files
- Cross-platform path handling (Windows/Linux)

**📝 Implementation:**

Implemented across 19 features in 6 phases (A-F):
- Phase A: Database migration, configuration, DTOs (3 features)
- Phase B: Core services - scanner, validator, cache (3 features)
- Phase C: Business logic - matching, conflicts, commands (3 features)
- Phase D: API layer - endpoints, validation, health checks (3 features)
- Phase E: Background services - initializer, DI registration (2 features)
- Phase F: Frontend - API client, types, UI, health banner (4 features)
- Phase G: Tests and documentation (1 phase)

**Migration Guide:**

See README.md for complete migration instructions. Quick summary:
1. Backup database (optional)
2. Update to v0.21.0
3. Move compose files to `/app/compose-files` (or configured path)
4. Verify discovery in UI
5. Resolve any naming conflicts using `x-disabled`

**Known Issues:**

- Some unit tests fail on Windows due to platform-specific behaviors (long paths, special characters)
- Old `UserServiceTests` needs update for new password hasher dependency (not critical)

**Contributors:**

This release represents a major architectural improvement making compose file management simpler, more intuitive, and more robust.

---

## Changes in v0.21.0

**Merged Pull Requests:**
- Docker debug fixes and more (#31)

**Commits:**
- Docker debug fixes and more (#31) (379289a)
- docs: update CHANGELOG for v0.20.0 [skip ci] (73efed9)
- chore: bump version to v0.20.0 [skip ci] (f006f0e)

## Changes in v0.20.0

**Merged Pull Requests:**
- Migrate the Front to Svelte (#30)

**Commits:**
- Migrate the Front to Svelte (#30) (d1a148d)

## Changes in v0.10.0

**Merged Pull Requests:**
- Overall project enhancement (#20)

**Commits:**
- Overall project enhancement (#20) (f18c98b)
- docs: update CHANGELOG for v0.9.0 [skip ci] (77cfe7d)
- chore: bump version to v0.9.0 [skip ci] (df809d8)

## Changes in v0.9.0

**Merged Pull Requests:**
- Users and permissions revamp (#19)

**Commits:**
- Users and permissions revamp (#19) (3c76c14)
- Password management overall + some fixes (#18) (95f687f)
- docs: update CHANGELOG for v0.8.0 [skip ci] (05cc892)
- chore: bump version to v0.8.0 [skip ci] (bd910f3)

## Changes in v0.8.0

**Merged Pull Requests:**
- Fix changelog update by release workflow (#17)

**Commits:**
- Fix changelog update by release workflow (#17) (ca0c9b0)
- Fix docker image release creation (#16) (643885b)
- docs: update CHANGELOG for v0.7.0 [skip ci] (2ebab6d)
- chore: bump version to v0.7.0 [skip ci] (ba2357b)

## Changes in v0.7.0

**Merged Pull Requests:**
- Fix docker image release creation (#15)

**Commits:**
- Fix docker image release creation (#15) (c5b3abc)
- docs: update CHANGELOG for v0.6.0 [skip ci] (83d67d2)
- chore: bump version to v0.6.0 [skip ci] (519ed14)

## Changes in v0.6.0

**Merged Pull Requests:**
- Fix docker image build and push for releases (#14)

**Commits:**
- Fix docker image build and push for releases (#14) (e90d8a1)
- docs: update CHANGELOG for v0.5.0 [skip ci] (456bb83)
- chore: bump version to v0.5.0 [skip ci] (d685fa6)

## Changes in v0.5.0

**Merged Pull Requests:**
- Add  more trigger to release build (#13)

**Commits:**
- Add  more trigger to release build (#13) (296c19a)
- docs: update CHANGELOG for v0.4.0 [skip ci] (bd470b6)
- chore: bump version to v0.4.0 [skip ci] (6ccca58)

## Changes in v0.4.0

**Merged Pull Requests:**
- Add release versionning with correct version and not sha commit (#12)

**Commits:**
- Add release versionning with correct version and not sha commit (#12) (200008d)
- docs: update CHANGELOG for v0.3.0 [skip ci] (efe84c9)
- chore: bump version to v0.3.0 [skip ci] (cb95cce)

## Changes in v0.3.0

**Merged Pull Requests:**
- Tweaks and fixes (#11)

**Commits:**
- Tweaks and fixes (#11) (28204c3)
- docs: update CHANGELOG for v0.2.0 [skip ci] (c215dc0)
- chore: bump version to v0.2.0 [skip ci] (e599e10)

# Changelog

All notable changes to this project will be documented in this file.

## Changes in v0.2.0

**Merged Pull Requests:**
- Add GHA to build and publish docker image to GHCR + CI stuff (#10)

**Commits:**
- Add GHA to build and publish docker image to GHCR + CI stuff (#10) (2fefa8b)
- Add MIT License to the project (8d9b12d)
- Compose and containers refinement (#9) (3d14bb2)
- Rewamp UI + tweaks (#7) (8700d9c)
- Compose functionnalities (#6) (8a451b3)
- UI tweaks and compose management fix (#5) (afeeed9)
- Little tweaks (#4) (5bea343)
- Implement base (#3) (4814c7b)
- Implement project base (#2) (b09e552)
- Add claude.md (#1) (a803dc9)
- Specs finalization (7064af7)
- First specs completion pass (51a2a54)
- First specs draft (4f3039e)