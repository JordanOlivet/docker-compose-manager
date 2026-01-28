// Permission related types

export const PermissionFlags = {
  None: 0,
  View: 1 << 0,      // 1
  Start: 1 << 1,     // 2
  Stop: 1 << 2,      // 4
  Restart: 1 << 3,   // 8
  Delete: 1 << 4,    // 16
  Update: 1 << 5,    // 32
  Logs: 1 << 6,      // 64
  Execute: 1 << 7,   // 128

  // Composite permissions
  get ReadOnly() { return this.View | this.Logs; },
  get Standard() { return this.View | this.Start | this.Stop | this.Restart | this.Logs; },
  get Full() { return this.View | this.Start | this.Stop | this.Restart | this.Delete | this.Update | this.Logs | this.Execute; }
} as const;

export type PermissionFlags = typeof PermissionFlags[keyof typeof PermissionFlags];

export const PermissionResourceType = {
  Container: 1,
  ComposeProject: 2
} as const;

export type PermissionResourceType = typeof PermissionResourceType[keyof typeof PermissionResourceType];

// User Groups

export interface UserGroup {
  id: number;
  name: string;
  description?: string;
  createdAt: string;
  updatedAt?: string;
  memberCount: number;
  memberIds: number[];
}

export interface CreateUserGroupRequest {
  name: string;
  description?: string;
  memberIds?: number[];
  permissions?: ResourcePermissionInput[];
}

export interface UpdateUserGroupRequest {
  name: string;
  description?: string;
  permissions?: ResourcePermissionInput[];
}

export interface AddUserToGroupRequest {
  userId: number;
}

// Resource Permissions

export interface ResourcePermission {
  id: number;
  resourceType: PermissionResourceType;
  resourceName: string;
  userId?: number;
  username?: string;
  userGroupId?: number;
  userGroupName?: string;
  permissions: PermissionFlags;
  createdAt: string;
  updatedAt?: string;
}

export interface CreatePermissionRequest {
  resourceType: PermissionResourceType;
  resourceName: string;
  userId?: number;
  userGroupId?: number;
  permissions: PermissionFlags;
}

export interface UpdatePermissionRequest {
  permissions: PermissionFlags;
}

export interface BulkCreatePermissionsRequest {
  permissions: CreatePermissionRequest[];
}

export interface CheckPermissionRequest {
  resourceType: PermissionResourceType;
  resourceName: string;
  requiredPermission: PermissionFlags;
}

export interface CheckPermissionResponse {
  hasPermission: boolean;
  userPermissions: PermissionFlags;
}

export interface UserPermissionsResponse {
  userId: number;
  isAdmin: boolean;
  directPermissions: ResourcePermission[];
  groupPermissions: ResourcePermission[];
}

// Helper functions for permissions

export const hasPermission = (
  userPermissions: PermissionFlags,
  requiredPermission: PermissionFlags
): boolean => {
  return (userPermissions & requiredPermission) === requiredPermission;
};

export const getPermissionLabel = (permission: PermissionFlags): string => {
  const labels: Record<number, string> = {
    [PermissionFlags.View]: 'View',
    [PermissionFlags.Start]: 'Start',
    [PermissionFlags.Stop]: 'Stop',
    [PermissionFlags.Restart]: 'Restart',
    [PermissionFlags.Delete]: 'Delete',
    [PermissionFlags.Update]: 'Update',
    [PermissionFlags.Logs]: 'Logs',
    [PermissionFlags.Execute]: 'Execute',
  };
  return labels[permission] || 'Unknown';
};

export const getPermissionLabels = (permissions: PermissionFlags): string[] => {
  const labels: string[] = [];

  if (permissions & PermissionFlags.View) labels.push('View');
  if (permissions & PermissionFlags.Start) labels.push('Start');
  if (permissions & PermissionFlags.Stop) labels.push('Stop');
  if (permissions & PermissionFlags.Restart) labels.push('Restart');
  if (permissions & PermissionFlags.Delete) labels.push('Delete');
  if (permissions & PermissionFlags.Update) labels.push('Update');
  if (permissions & PermissionFlags.Logs) labels.push('Logs');
  if (permissions & PermissionFlags.Execute) labels.push('Execute');

  return labels;
};

export const getResourceTypeLabel = (type: PermissionResourceType): string => {
  return type === PermissionResourceType.Container ? 'Container' : 'Compose Project';
};

// New types for permissions management

export interface ResourcePermissionInput {
  resourceType: PermissionResourceType;
  resourceName: string;
  permissions: PermissionFlags;
}

export interface CopyPermissionsRequest {
  sourceUserId?: number;
  sourceUserGroupId?: number;
  targetUserId?: number;
  targetUserGroupId?: number;
}
