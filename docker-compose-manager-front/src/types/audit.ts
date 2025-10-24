// Audit Log Types
export interface AuditLog {
  id: number;
  userId?: number;
  username?: string;
  action: string;
  resourceType?: string;
  resourceId?: string;
  ipAddress: string;
  timestamp: string;
}

export interface AuditLogDetails extends AuditLog {
  details?: string;
  beforeState?: string;
  afterState?: string;
  parsedBeforeState?: any;
  parsedAfterState?: any;
}

// Audit Filter Request
export interface AuditFilterRequest {
  userId?: number;
  action?: string;
  resourceType?: string;
  resourceId?: string;
  ipAddress?: string;
  startDate?: string;
  endDate?: string;
  search?: string;
  page?: number;
  pageSize?: number;
  sortBy?: AuditSortField;
  sortOrder?: SortOrder;
}

export const AuditSortField = {
  Timestamp: 'Timestamp',
  Action: 'Action',
  UserId: 'UserId',
  ResourceType: 'ResourceType'
} as const;

export type AuditSortField = typeof AuditSortField[keyof typeof AuditSortField];

export const SortOrder = {
  Ascending: 'Ascending',
  Descending: 'Descending'
} as const;

export type SortOrder = typeof SortOrder[keyof typeof SortOrder];

// Audit Logs Response
export interface AuditLogsResponse {
  logs: AuditLog[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// Audit Stats
export interface AuditStats {
  totalLogs: number;
  totalUsers: number;
  totalActions: number;
  recentActivity: AuditLog[];
  topActions: ActionCount[];
  topUsers: UserActionCount[];
}

export interface ActionCount {
  action: string;
  count: number;
}

export interface UserActionCount {
  userId: number;
  username: string;
  count: number;
}

// Distinct Values Response (for filters)
export interface DistinctActionsResponse {
  actions: string[];
}

export interface DistinctResourceTypesResponse {
  resourceTypes: string[];
}

// User Audit Activity Response
export interface UserAuditActivityResponse {
  userId: number;
  username: string;
  totalActions: number;
  recentActions: AuditLog[];
  actionBreakdown: ActionCount[];
}

// Purge Logs Request
export interface PurgeLogsRequest {
  olderThanDays: number;
  dryRun?: boolean;
}

export interface PurgeLogsResponse {
  deletedCount: number;
  message: string;
  dryRun: boolean;
}

// Audit Action Constants (matching backend)
export const AUDIT_ACTIONS = {
  // Authentication
  LOGIN: 'User.Login',
  LOGIN_FAILED: 'User.LoginFailed',
  LOGOUT: 'User.Logout',
  PASSWORD_CHANGE: 'User.PasswordChange',
  TOKEN_REFRESH: 'User.TokenRefresh',

  // User Management
  USER_CREATE: 'User.Create',
  USER_UPDATE: 'User.Update',
  USER_DELETE: 'User.Delete',
  USER_ENABLE: 'User.Enable',
  USER_DISABLE: 'User.Disable',

  // Compose Files
  COMPOSE_FILE_CREATE: 'ComposeFile.Create',
  COMPOSE_FILE_READ: 'ComposeFile.Read',
  COMPOSE_FILE_UPDATE: 'ComposeFile.Update',
  COMPOSE_FILE_DELETE: 'ComposeFile.Delete',

  // Compose Operations
  COMPOSE_UP: 'Compose.Up',
  COMPOSE_DOWN: 'Compose.Down',
  COMPOSE_RESTART: 'Compose.Restart',
  COMPOSE_STOP: 'Compose.Stop',
  COMPOSE_START: 'Compose.Start',
  COMPOSE_PULL: 'Compose.Pull',
  COMPOSE_BUILD: 'Compose.Build',
  COMPOSE_LOGS: 'Compose.Logs',

  // Container Operations
  CONTAINER_START: 'Container.Start',
  CONTAINER_STOP: 'Container.Stop',
  CONTAINER_RESTART: 'Container.Restart',
  CONTAINER_PAUSE: 'Container.Pause',
  CONTAINER_UNPAUSE: 'Container.Unpause',
  CONTAINER_REMOVE: 'Container.Remove',

  // System Operations
  SETTINGS_UPDATE: 'Settings.Update',
  AUDIT_VIEW: 'Audit.View',
  AUDIT_PURGE: 'Audit.Purge'
} as const;

export type AuditAction = typeof AUDIT_ACTIONS[keyof typeof AUDIT_ACTIONS];

// Resource Type Constants
export const RESOURCE_TYPES = {
  USER: 'User',
  COMPOSE_FILE: 'ComposeFile',
  COMPOSE_PROJECT: 'ComposeProject',
  CONTAINER: 'Container',
  SETTINGS: 'Settings',
  AUDIT_LOG: 'AuditLog'
} as const;

export type ResourceType = typeof RESOURCE_TYPES[keyof typeof RESOURCE_TYPES];
