# Feature List - Compose Discovery Revamp

**Base Branch:** `Revamp-compose-discover-mecanism`
**Strategy:** Progressive implementation by phases (Backend first, then Frontend)
**Branch naming:** `feature/<feature-name>` based on `Revamp-compose-discover-mecanism`

---

## 📋 Execution Order & Dependencies

```
PHASE A (Sequential - Foundations)
├─ A1: Database Migration (Remove ComposePaths)
├─ A2: Configuration & Options
└─ A3: Data Models & DTOs

PHASE B (Parallel - Core Services)
├─ B1: ComposeFileScanner Service
├─ B2: PathValidator Service (Security)
└─ B3: Thread-Safe Cache Service

PHASE C (Parallel - Business Logic)
├─ C1: Project Matching Logic
├─ C2: Conflict Resolution Service
└─ C3: Command Classification Service

PHASE D (Sequential - API Layer)
├─ D1: Update ComposeController (New Endpoints)
├─ D2: Health Check Endpoint
└─ D3: Refresh Endpoint

PHASE E (Sequential - Background Services)
├─ E1: ComposeDiscoveryInitializer (Background Scan)
└─ E2: Service Registration in Program.cs

PHASE F (Parallel - Frontend)
├─ F1: Update API Client
├─ F2: Update Types/DTOs
├─ F3: Update Compose Pages/Components
└─ F4: Add Health Status Banner

PHASE G (Final - Testing & Documentation)
├─ G1: Unit Tests
├─ G2: Integration Tests
└─ G3: Documentation Updates
```

---

## 🔧 PHASE A: Foundations (Sequential)

### A1: Database Migration - Remove ComposePaths
**Branch:** `feature/remove-compose-paths-migration`
**Dependencies:** None
**Complexity:** Low
**Estimated Time:** 30 min

**Files to Modify:**
- `Migrations/` (new migration file)
- `src/Models/ComposePath.cs` (remove)
- `src/Models/ComposeFile.cs` (remove)
- `src/Data/AppDbContext.cs` (remove DbSet<ComposePath>, DbSet<ComposeFile>)

**Tasks:**
- [ ] Create EF migration to drop `ComposePaths` and `ComposeFiles` tables
- [ ] Optional: Log existing paths before deletion (for user reference)
- [ ] Remove `ComposePath` and `ComposeFile` model classes
- [ ] Remove DbSets from AppDbContext
- [ ] Test migration (up and down)

**Acceptance Criteria:**
- Migration runs successfully
- Tables are removed
- Application starts without errors

---

### A2: Configuration & Options
**Branch:** `feature/compose-discovery-configuration`
**Dependencies:** A1
**Complexity:** Low
**Estimated Time:** 45 min

**Files to Modify:**
- `appsettings.json` (add new section)
- `appsettings.Development.json` (add dev config)
- New file: `src/Configuration/ComposeDiscoveryOptions.cs`

**Tasks:**
- [ ] Create `ComposeDiscoveryOptions` class:
  ```csharp
  public class ComposeDiscoveryOptions
  {
      public string RootPath { get; set; } = "/app/compose-files";
      public int ScanDepthLimit { get; set; } = 5;
      public int CacheDurationSeconds { get; set; } = 10;
      public int MaxFileSizeKB { get; set; } = 1024;
  }
  ```
- [ ] Add configuration section to `appsettings.json`
- [ ] Add development override to `appsettings.Development.json`
- [ ] Validate options at startup (throw if RootPath is empty)

**Acceptance Criteria:**
- Configuration loads correctly
- Default values work
- Dev environment can override paths

---

### A3: Data Models & DTOs
**Branch:** `feature/compose-discovery-dtos`
**Dependencies:** A1
**Complexity:** Low
**Estimated Time:** 45 min

**Files to Create:**
- `src/DTOs/ComposeDiscoveryDtos.cs`
- `src/Models/DiscoveredComposeFile.cs` (internal model, not DB entity)

**Tasks:**
- [ ] Create DTOs:
  - `DiscoveredComposeFileDto` (for API responses)
  - `ComposeHealthDto` (for health endpoint)
  - `ConflictErrorDto` (for conflict endpoint)
  - `ComposeProjectInfoDto` (enhanced with new fields)
- [ ] Update existing `ComposeProjectDto` to add:
  - `string? ComposeFilePath`
  - `bool HasComposeFile`
  - `List<string> Services`
  - `Dictionary<string, bool> AvailableActions`
  - `string? Warning`
- [ ] Create internal model `DiscoveredComposeFile`:
  ```csharp
  public class DiscoveredComposeFile
  {
      public string FilePath { get; set; }
      public string ProjectName { get; set; }
      public string DirectoryPath { get; set; }
      public DateTime LastModified { get; set; }
      public bool IsValid { get; set; }
      public bool IsDisabled { get; set; }
      public List<string> Services { get; set; }
  }
  ```

**Acceptance Criteria:**
- All DTOs compile
- No breaking changes to existing DTOs (additive only)

---

## 🛠️ PHASE B: Core Services (Parallel)

### B1: ComposeFileScanner Service
**Branch:** `feature/compose-file-scanner`
**Dependencies:** A2, A3
**Complexity:** High
**Estimated Time:** 2-3 hours

**Files to Create:**
- `src/Services/ComposeFileScanner.cs`
- `src/Services/IComposeFileScanner.cs`

**Tasks:**
- [ ] Implement `ScanComposeFilesRecursive()` with depth limit
- [ ] Scan `*.yml`, `*.yaml`, `*.YML`, `*.YAML` (case sensitivity)
- [ ] Validate file size (max configurable, default 1 MB)
- [ ] Parse YAML and validate structure (presence of `services` key)
- [ ] Extract project name (priority: `name` attribute → directory name → filename)
- [ ] Extract `x-disabled` attribute
- [ ] Extract list of services
- [ ] Handle YAML parsing errors gracefully (catch YamlException, OutOfMemoryException)
- [ ] Accept unresolved environment variables (${VERSION})
- [ ] Structured logging with metrics (duration, total files, valid/invalid counts)

**Acceptance Criteria:**
- Scans up to 5 levels deep
- Ignores non-compose YAML files
- Logs debug for invalid files
- Returns list of `DiscoveredComposeFile` objects

---

### B2: PathValidator Service (Security)
**Branch:** `feature/path-validator`
**Dependencies:** A2
**Complexity:** Low
**Estimated Time:** 30 min

**Files to Create:**
- `src/Services/PathValidator.cs`
- `src/Services/IPathValidator.cs`

**Tasks:**
- [ ] Implement `IsValidComposeFilePath(string userProvidedPath)`:
  ```csharp
  public bool IsValidComposeFilePath(string userProvidedPath)
  {
      var rootPath = Path.GetFullPath(_options.RootPath);
      var fullPath = Path.GetFullPath(userProvidedPath);

      if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
      {
          _logger.LogWarning("Path traversal attempt: {Path}", userProvidedPath);
          return false;
      }
      return true;
  }
  ```
- [ ] Log warnings for path traversal attempts

**Acceptance Criteria:**
- Blocks `../../../etc/passwd`
- Allows valid paths within RootPath

---

### B3: Thread-Safe Cache Service
**Branch:** `feature/thread-safe-cache`
**Dependencies:** A2, A3, B1
**Complexity:** Medium
**Estimated Time:** 1 hour

**Files to Create:**
- `src/Services/ComposeFileCacheService.cs`
- `src/Services/IComposeFileCacheService.cs`

**Tasks:**
- [ ] Implement thread-safe cache with `SemaphoreSlim`:
  ```csharp
  private readonly SemaphoreSlim _scanLock = new SemaphoreSlim(1, 1);
  ```
- [ ] Implement double-check pattern
- [ ] Cache key: `"compose_file_discovery"`
- [ ] Configurable TTL (from `ComposeDiscoveryOptions.CacheDurationSeconds`)
- [ ] Methods:
  - `Task<List<DiscoveredComposeFile>> GetOrScanAsync()`
  - `void Invalidate()`
- [ ] Log cache HIT/MISS with debug level

**Acceptance Criteria:**
- No concurrent scans
- Cache works correctly under load
- Logs provide visibility into cache behavior

---

## 🧩 PHASE C: Business Logic (Parallel)

### C1: Project Matching Logic
**Branch:** `feature/project-matching`
**Dependencies:** A3, B1, B3
**Complexity:** Medium
**Estimated Time:** 1.5 hours

**Files to Create:**
- `src/Services/ProjectMatchingService.cs`
- `src/Services/IProjectMatchingService.cs`

**Tasks:**
- [ ] Fetch Docker projects via existing `ComposeDiscoveryService`
- [ ] Fetch discovered files via `IComposeFileCacheService`
- [ ] Match projects by name (case-insensitive)
- [ ] Create unified `ComposeProjectDto` list:
  - Projects with files (status from Docker)
  - Projects without files (warning message)
  - Files without projects (status: "not-started")
- [ ] Populate `ComposeProjectDto` fields:
  - `ComposeFilePath`
  - `HasComposeFile`
  - `Services` (from file scan)
  - `Warning` (if no file found)

**Acceptance Criteria:**
- All active projects appear
- "not-started" projects appear
- Projects without files have warnings

---

### C2: Conflict Resolution Service
**Branch:** `feature/conflict-resolution`
**Dependencies:** A3, B1
**Complexity:** Medium
**Estimated Time:** 1.5 hours

**Files to Create:**
- `src/Services/ConflictResolutionService.cs`
- `src/Services/IConflictResolutionService.cs`

**Tasks:**
- [ ] Implement `ResolveProjectNameConflicts()`:
  - Group files by project name
  - Count active vs disabled files
  - **Case A:** 1 active → Use it (log Info)
  - **Case B:** 0 active → Ignore all (log Warning)
  - **Case C:** 2+ active → Error (log Error with file list)
- [ ] Sort files alphabetically for deterministic behavior
- [ ] Store conflict errors for API exposure
- [ ] Method to get conflict errors: `List<ConflictErrorDto> GetConflicts()`

**Acceptance Criteria:**
- Only one file per project name
- Conflicts logged clearly
- Conflict API returns useful information

---

### C3: Command Classification Service
**Branch:** `feature/command-classification`
**Dependencies:** None
**Complexity:** Low
**Estimated Time:** 30 min

**Files to Create:**
- `src/Services/ComposeCommandClassifier.cs`

**Tasks:**
- [ ] Create static class with command lists:
  ```csharp
  public static class ComposeCommandClassifier
  {
      public static readonly string[] RequiresFile = { "up", "create", "run", "build", "pull", "push", "config" };
      public static readonly string[] WorksWithoutFile = { "start", "stop", "restart", "pause", "unpause", "ps", "logs", "down", "rm", "kill" };

      public static bool RequiresComposeFile(string command) =>
          RequiresFile.Contains(command.ToLower());
  }
  ```
- [ ] Method to compute `AvailableActions` dictionary for a project

**Acceptance Criteria:**
- Correctly classifies all compose commands
- Returns accurate `AvailableActions` dict

---

## 🌐 PHASE D: API Layer (Sequential)

### D1: Update ComposeController
**Branch:** `feature/update-compose-controller`
**Dependencies:** B1, B2, B3, C1, C2, C3
**Complexity:** High
**Estimated Time:** 2-3 hours

**Files to Modify:**
- `src/Controllers/ComposeController.cs`

**Tasks:**
- [ ] Update `GET /api/compose/projects` to use new matching logic
- [ ] Add new endpoint `GET /api/compose/files` (list discovered files)
- [ ] Add new endpoint `GET /api/compose/conflicts` (list conflicts)
- [ ] Update command execution methods to check `ComposeCommandClassifier`
- [ ] Add validation for commands requiring files
- [ ] Return `availableActions` in project details
- [ ] Add `[Obsolete]` attribute to old ComposePaths endpoints with 410 Gone responses
- [ ] Validate file paths with `IPathValidator` on file-related endpoints
- [ ] Update Swagger/XML comments

**Acceptance Criteria:**
- All new endpoints work
- Old endpoints return 410 with helpful message
- Commands execute correctly based on file availability

---

### D2: Health Check Endpoint
**Branch:** `feature/health-check-endpoint`
**Dependencies:** A2, B1
**Complexity:** Medium
**Estimated Time:** 1 hour

**Files to Modify:**
- `src/Controllers/ComposeController.cs` or create `src/Controllers/HealthController.cs`

**Tasks:**
- [ ] Create `GET /api/compose/health` endpoint:
  - Check compose directory exists and accessible
  - Check Docker daemon connectivity
  - Return structured response:
    ```json
    {
      "status": "healthy|degraded|critical",
      "composeDiscovery": { "status": "...", "rootPath": "...", ... },
      "dockerDaemon": { "status": "...", "version": "...", ... }
    }
    ```
- [ ] Overall status logic:
  - `critical` if Docker unavailable
  - `degraded` if directory inaccessible
  - `healthy` if both OK

**Acceptance Criteria:**
- Endpoint returns accurate status
- Useful for monitoring and debugging

---

### D3: Refresh Endpoint
**Branch:** `feature/refresh-endpoint`
**Dependencies:** B3
**Complexity:** Low
**Estimated Time:** 20 min

**Files to Modify:**
- `src/Controllers/ComposeController.cs`

**Tasks:**
- [ ] Create `POST /api/compose/refresh` endpoint:
  ```csharp
  [HttpPost("refresh")]
  [Authorize(Roles = "Admin")]
  public async Task<IActionResult> RefreshComposeFiles()
  {
      _cacheService.Invalidate();
      var files = await _scanner.ScanComposeFiles();

      return Ok(new {
          success = true,
          message = $"Cache refreshed. Found {files.Count} compose files.",
          filesDiscovered = files.Count,
          timestamp = DateTime.UtcNow
      });
  }
  ```
- [ ] Admin-only authorization

**Acceptance Criteria:**
- Invalidates cache
- Returns scan results
- Non-admins get 403

---

## ⚙️ PHASE E: Background Services (Sequential)

### E1: ComposeDiscoveryInitializer
**Branch:** `feature/discovery-initializer`
**Dependencies:** B1, B3
**Complexity:** Medium
**Estimated Time:** 45 min

**Files to Create:**
- `src/Services/ComposeDiscoveryInitializer.cs`

**Tasks:**
- [ ] Implement `IHostedService`:
  ```csharp
  public class ComposeDiscoveryInitializer : IHostedService
  {
      public async Task StartAsync(CancellationToken cancellationToken)
      {
          _ = Task.Run(async () =>
          {
              using var scope = _serviceProvider.CreateScope();
              var scanner = scope.ServiceProvider.GetRequiredService<IComposeFileScanner>();

              _logger.LogInformation("Starting initial compose files scan...");
              var files = await scanner.ScanComposeFiles();
              _logger.LogInformation("Initial scan completed. Found {Count} files.", files.Count);
          }, cancellationToken);

          return Task.CompletedTask; // Non-blocking
      }
  }
  ```
- [ ] Handle exceptions gracefully
- [ ] Log start and completion with metrics

**Acceptance Criteria:**
- Doesn't block app startup
- Logs show scan progress
- Errors don't crash app

---

### E2: Service Registration in Program.cs
**Branch:** `feature/service-registration`
**Dependencies:** All backend services
**Complexity:** Low
**Estimated Time:** 30 min

**Files to Modify:**
- `Program.cs`

**Tasks:**
- [ ] Register configuration: `builder.Services.Configure<ComposeDiscoveryOptions>(...)`
- [ ] Register services:
  - `IComposeFileScanner → ComposeFileScanner`
  - `IPathValidator → PathValidator`
  - `IComposeFileCacheService → ComposeFileCacheService`
  - `IProjectMatchingService → ProjectMatchingService`
  - `IConflictResolutionService → ConflictResolutionService`
- [ ] Register hosted service: `builder.Services.AddHostedService<ComposeDiscoveryInitializer>()`
- [ ] Check directory at startup (degraded mode logic)

**Acceptance Criteria:**
- App starts successfully
- All services resolve correctly
- Background scan starts

---

## 🎨 PHASE F: Frontend (Parallel after Backend Complete)

### F1: Update API Client
**Branch:** `feature/frontend-api-client`
**Dependencies:** Backend complete
**Complexity:** Medium
**Estimated Time:** 1 hour

**Files to Modify:**
- `docker-compose-manager-front-new/src/lib/api/compose.ts`

**Tasks:**
- [ ] Add new API functions:
  - `getComposeFiles(): Promise<DiscoveredComposeFileDto[]>`
  - `getComposeConflicts(): Promise<ConflictErrorDto[]>`
  - `getComposeHealth(): Promise<ComposeHealthDto>`
  - `refreshComposeFiles(): Promise<{ filesDiscovered: number }>`
- [ ] Update existing functions for new DTO fields

**Acceptance Criteria:**
- API client compiles
- All endpoints callable

---

### F2: Update Types/DTOs
**Branch:** `feature/frontend-types`
**Dependencies:** Backend complete
**Complexity:** Low
**Estimated Time:** 30 min

**Files to Modify:**
- `docker-compose-manager-front-new/src/lib/types/` (create or update relevant type files)

**Tasks:**
- [ ] Add TypeScript interfaces matching backend DTOs:
  - `DiscoveredComposeFileDto`
  - `ComposeHealthDto`
  - `ConflictErrorDto`
- [ ] Update `ComposeProjectDto` interface with new fields

**Acceptance Criteria:**
- Types match backend DTOs
- No TypeScript errors

---

### F3: Update Compose Pages/Components
**Branch:** `feature/frontend-compose-ui`
**Dependencies:** F1, F2
**Complexity:** High
**Estimated Time:** 3-4 hours

**Files to Modify/Create:**
- Compose project list page
- Compose project detail page
- Relevant components

**Tasks:**
- [ ] Display "not-started" projects with badge
- [ ] Show compose file path for each project
- [ ] Warning badge for projects without files
- [ ] Disable Up/Build/Recreate buttons if no file (with tooltip)
- [ ] Keep Start/Stop/Restart active for projects without files
- [ ] Use `availableActions` from API to enable/disable buttons
- [ ] Add "Start" button for not-started projects

**Acceptance Criteria:**
- UI accurately reflects project state
- Actions only enabled when appropriate
- User understands why actions are disabled

---

### F4: Add Health Status Banner
**Branch:** `feature/frontend-health-banner`
**Dependencies:** F1, F2
**Complexity:** Medium
**Estimated Time:** 1 hour

**Files to Create:**
- New component: `ComposeHealthBanner.svelte`

**Tasks:**
- [ ] Fetch health status on app load
- [ ] Display banner if status is "degraded" or "critical"
- [ ] Show message and suggestions for resolution
- [ ] Add "Retry" button to re-check
- [ ] Dismissible (store dismissed state in localStorage)
- [ ] Different styles for degraded vs critical

**Acceptance Criteria:**
- Banner appears on degraded/critical status
- Provides helpful info to user
- Can be dismissed

---

## ✅ PHASE G: Testing & Documentation (Final)

### G1: Unit Tests
**Branch:** `feature/unit-tests`
**Dependencies:** All backend services
**Complexity:** High
**Estimated Time:** 4-5 hours

**Files to Create:**
- `docker-compose-manager-back.Tests/Services/ComposeFileScannerTests.cs`
- `docker-compose-manager-back.Tests/Services/PathValidatorTests.cs`
- `docker-compose-manager-back.Tests/Services/ConflictResolutionServiceTests.cs`
- `docker-compose-manager-back.Tests/Services/ProjectMatchingServiceTests.cs`
- `docker-compose-manager-back.Tests/Services/ComposeCommandClassifierTests.cs`

**Tasks:**
- [ ] Test file scanning (valid/invalid files, depth limits)
- [ ] Test YAML parsing (valid, invalid, variables, OutOfMemory)
- [ ] Test path validation (valid paths, path traversal attempts)
- [ ] Test conflict resolution (all 3 cases: 1 active, 0 active, 2+ active)
- [ ] Test command classification
- [ ] Test project matching
- [ ] Test cache behavior (hit/miss, expiration)
- [ ] Test thread safety (concurrent requests)

**Acceptance Criteria:**
- All tests pass
- Coverage > 80% for new code

---

### G2: Integration Tests
**Branch:** `feature/integration-tests`
**Dependencies:** Backend complete
**Complexity:** High
**Estimated Time:** 3-4 hours

**Files to Create:**
- `docker-compose-manager-back.Tests/Integration/ComposeDiscoveryIntegrationTests.cs`

**Tasks:**
- [ ] Test real file scanning with sample compose files
- [ ] Test API endpoints (GET /api/compose/projects, /files, /health, /conflicts)
- [ ] Test command execution with/without files
- [ ] Test conflict scenarios (create files with same project name)
- [ ] Test degraded mode (make directory inaccessible)
- [ ] Test path traversal via API

**Acceptance Criteria:**
- Integration tests pass
- Real scenarios covered

---

### G3: Documentation Updates
**Branch:** `feature/documentation`
**Dependencies:** All features complete
**Complexity:** Medium
**Estimated Time:** 2 hours

**Files to Modify:**
- `CLAUDE.md`
- `README.md`
- `CHANGELOG.md`

**Tasks:**
- [ ] Update CLAUDE.md with new discovery mechanism
- [ ] Add migration guide to README
- [ ] Document `x-disabled` label usage
- [ ] Update Swagger/OpenAPI descriptions
- [ ] Update CHANGELOG with breaking changes and new features
- [ ] Add troubleshooting section

**Acceptance Criteria:**
- Documentation is clear and complete
- Migration path is documented
- Examples are provided

---

## 📊 Summary

**Total Features:** 19
**Phases:** 7
**Estimated Total Time:** 25-30 hours (distributed across agents)

**Parallelization Opportunities:**
- Phase B: 3 agents in parallel (B1, B2, B3)
- Phase C: 3 agents in parallel (C1, C2, C3)
- Phase F: 4 agents in parallel (F1, F2, F3, F4)

**Critical Path:**
A1 → A2 → A3 → B1/B3 → C1 → D1 → E1 → E2 → Frontend → Testing

---

## 🚀 Next Steps

1. **Validate this feature list** with the user
2. **Start Phase A** (sequential implementation)
3. **After Phase A complete**: Launch Phase B agents in parallel
4. **Progressive validation** after each phase
5. **Merge to main** after all phases complete and tested
