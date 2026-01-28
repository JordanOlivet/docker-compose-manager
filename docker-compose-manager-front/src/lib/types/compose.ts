import type { EntityState } from "./global";

// Compose File Types
export interface ComposeFile {
  id: number;
  fileName: string;
  fullPath: string;
  directory: string;
  size: number;
  lastModified: string;
  lastScanned: string;
  composePathId: number;
  isDiscovered: boolean;
}

/**
 * Discovered compose file information from the discovery system.
 * Used by the compose file discovery API.
 */
export interface DiscoveredComposeFileDto {
  /** Full absolute path to the compose file */
  filePath: string;
  /** Project name (from 'name:' attribute or directory name) */
  projectName: string;
  /** Directory containing the compose file */
  directoryPath: string;
  /** Last modification timestamp */
  lastModified: string;
  /** Whether the file is valid YAML with required structure */
  isValid: boolean;
  /** Whether the file is marked with x-disabled: true */
  isDisabled: boolean;
  /** List of service names in the compose file */
  services: string[];
}

export interface ComposeFileContent {
  id: number;
  fileName: string;
  fullPath: string;
  content: string;
  etag: string;
  lastModified: string;
}

export interface CreateComposeFileRequest {
  filePath: string;
  content: string;
}

export interface UpdateComposeFileRequest {
  content: string;
  etag: string;
}

// Compose Project Types
export interface ComposeProject {
  name: string;
  path?: string;
  state: EntityState;
  services: ComposeService[];
  composeFiles: string[];
  lastUpdated: Date;
  // New fields from compose discovery revamp
  /** Path to the primary compose file associated with this project (null if not found) */
  composeFilePath?: string | null;
  /** Indicates whether a compose file was found for this project */
  hasComposeFile?: boolean;
  /** Warning message if project has issues (e.g., "No compose file found for this project") */
  warning?: string | null;
  /** Dictionary of available actions and whether they can be performed */
  availableActions?: Record<string, boolean> | null;
}

export interface ComposeService {
  id: string;
  name: string;
  image?: string;
  state: EntityState;
  status?: string;
  ports?: string[];  
  health?: string;
}

export interface ComposeProjectDetailsDto{
    name: string,
    path: string,
    isRunning: boolean,
    totalServices: number,
    runningServices: number,
    stoppedServices: number,
    ComposeServiceStatusDto: ComposeService[]
}


// Compose Operation Request Types
export interface ComposeUpRequest {
  detach?: boolean;
  build?: boolean;
  forceRecreate?: boolean;
  noRecreate?: boolean;
  noBuild?: boolean;
  removeOrphans?: boolean;
  timeout?: number;
}

export interface ComposeDownRequest {
  removeVolumes?: boolean;
  removeImages?: 'all' | 'local' | null;
  removeOrphans?: boolean;
  timeout?: number;
}

export interface ComposeRestartRequest {
  timeout?: number;
  noDeps?: boolean;
}

export interface ComposeStopRequest {
  timeout?: number;
}

export interface ComposeStartRequest {
  noDeps?: boolean;
}

export interface ComposePullRequest {
  ignorePullFailures?: boolean;
  quiet?: boolean;
}

export interface ComposeBuildRequest {
  noCache?: boolean;
  pull?: boolean;
  parallel?: boolean;
}

export interface ComposeLogsRequest {
  serviceName?: string;
  tail?: number;
  follow?: boolean;
  timestamps?: boolean;
}

// Compose Operation Response Types
export interface ComposeOperationResponse {
  operationId: string;
  status: string;
  message: string;
}

export interface ComposeLogsResponse {
  logs: string;
  hasMore: boolean;
}

// Compose Path Types (for validation)
export interface ComposePath {
  id: number;
  path: string;
  description?: string;
  isEnabled: boolean;
  isRecursive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateComposePathRequest {
  path: string;
  description?: string;
  isEnabled?: boolean;
  isRecursive?: boolean;
}

export interface UpdateComposePathRequest {
  path?: string;
  description?: string;
  isEnabled?: boolean;
  isRecursive?: boolean;
}

// Compose File Parsed Details Types
export interface ComposeFileDetails {
  projectName: string;
  version?: string;
  services: Record<string, ServiceDetails>;
  networks?: Record<string, NetworkDetails>;
  volumes?: Record<string, VolumeDetails>;
}

export interface ServiceDetails {
  name: string;
  image?: string;
  build?: string;
  ports?: string[];
  environment?: Record<string, string>;
  labels?: Record<string, string>;
  volumes?: string[];
  dependsOn?: string[];
  restart?: string;
  networks?: Record<string, string>;
}

export interface NetworkDetails {
  name: string;
  driver?: string;
  external?: boolean;
  driverOpts?: Record<string, string>;
  labels?: Record<string, string>;
}

export interface VolumeDetails {
  name: string;
  driver?: string;
  external?: boolean;
  driverOpts?: Record<string, string>;
  labels?: Record<string, string>;
}

// ============================================
// Conflict Detection Types
// ============================================

/**
 * Error information about conflicting compose files with the same project name.
 */
export interface ConflictErrorDto {
  /** The project name that has conflicts */
  projectName: string;
  /** List of file paths that conflict (all have same project name, none are disabled) */
  conflictingFiles: string[];
  /** User-friendly error message */
  message: string;
  /** Step-by-step instructions to resolve the conflict */
  resolutionSteps: string[];
}

/**
 * Response wrapper for the conflicts endpoint
 */
export interface ConflictsResponse {
  /** List of all detected conflicts */
  conflicts: ConflictErrorDto[];
  /** Whether any conflicts exist */
  hasConflicts: boolean;
}

// ============================================
// Health Check Types
// ============================================

/**
 * Status information for the compose file discovery subsystem
 */
export interface ComposeHealthStatusDto {
  /** Status: "healthy" or "degraded" */
  status: 'healthy' | 'degraded';
  /** Configured root path for compose files */
  rootPath: string;
  /** Whether the root path exists on filesystem */
  exists: boolean;
  /** Whether the root path is accessible (readable) */
  accessible: boolean;
  /** Whether the system is running in degraded mode (compose discovery disabled) */
  degradedMode: boolean;
  /** Optional message explaining degraded status */
  message?: string;
  /** Description of the impact when in degraded mode */
  impact?: string;
}

/**
 * Status information for the Docker daemon connection
 */
export interface DockerDaemonStatusDto {
  /** Status: "healthy" or "unhealthy" */
  status: 'healthy' | 'unhealthy';
  /** Whether connected to Docker daemon */
  connected: boolean;
  /** Docker version (if connected) */
  version?: string;
  /** Docker API version (if connected) */
  apiVersion?: string;
  /** Error message if connection failed */
  error?: string;
}

/**
 * Health status information for the compose discovery system.
 * Used by the GET /api/compose/health endpoint.
 */
export interface ComposeHealthDto {
  /** Overall system status: "healthy", "degraded", or "critical" */
  status: 'healthy' | 'degraded' | 'critical';
  /** Compose discovery subsystem status */
  composeDiscovery: ComposeHealthStatusDto;
  /** Docker daemon connection status */
  dockerDaemon: DockerDaemonStatusDto;
}

/**
 * Response from the refresh endpoint
 */
export interface RefreshComposeResponse {
  /** Number of files discovered */
  filesDiscovered: number;
  /** Timestamp of the refresh operation */
  timestamp: string;
  /** Success status */
  success: boolean;
  /** Optional message */
  message?: string;
}
