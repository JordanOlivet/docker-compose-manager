import type { EntityState } from "./global";
import type { PermissionFlags } from "./permissions";

// ============================================================================
// NEW ARCHITECTURE: Docker-Only Discovery (COMPOSE_DISCOVERY_REFACTOR.md)
// ============================================================================

/**
 * Project status enum based on `docker compose ls --all` output
 */
export enum ProjectStatus {
	/** Status contains "running" - project is active */
	Running = 'Running',
	/** Status contains "exited" with count > 0 - project is stopped but containers exist */
	Stopped = 'Stopped',
	/** Status contains "exited(0)" - project is down without containers */
	Removed = 'Removed',
	/** Status is not parsable */
	Unknown = 'Unknown',
  Down = "Down",
  Degraded = "Degraded", 
  Restarting = "Restarting",
  Exited = "Exited",
  Created = "Created",
}

/**
 * Compose Project DTO from the new Docker-only discovery system
 * Source of truth: `docker compose ls --all --format json`
 * No database persistence - projects are discovered dynamically from Docker
 */
export interface ComposeProjectDto {
	/** Identifiant du projet (nom Docker) */
	name: string;

	/** Informations découvertes depuis docker compose ls */
	/** Ex: "running(3)", "exited(2)" */
	rawStatus: string;
	/** Chemins des fichiers compose (informatif uniquement - backend n'y accède pas) */
	configFiles: string[];

	// /** État parsé */
	// status: ProjectStatus;
	/** Nombre de containers */
	containerCount: number;

  state: EntityState;
  services: ComposeService[];
	/** Permissions de l'utilisateur actuel sur ce projet */
	userPermissions: PermissionFlags;

	// /** Propriétés calculées pour l'UI */
	// canStart: boolean;
	// canStop: boolean;
	// statusColor: string;
}

// ============================================================================
// LEGACY TYPES: May be deprecated in favor of ComposeProjectDto
// ============================================================================

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
