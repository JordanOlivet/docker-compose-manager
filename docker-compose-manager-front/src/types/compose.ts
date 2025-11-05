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
