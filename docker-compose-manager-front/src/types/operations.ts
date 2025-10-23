// Operation Types
export interface Operation {
  operationId: string;
  type: OperationType;
  status: OperationStatus;
  progress?: number;
  userId?: number;
  projectPath?: string;
  projectName?: string;
  startedAt: string;
  completedAt?: string;
}

export interface OperationDetails extends Operation {
  logs?: string;
  errorMessage?: string;
}

export interface OperationProgress {
  operationId: string;
  status: OperationStatus;
  progress: number;
  message?: string;
  timestamp: string;
}

// Operation Request/Response Types
export interface CreateOperationRequest {
  type: OperationType;
  projectPath?: string;
  projectName?: string;
}

export interface UpdateOperationStatusRequest {
  status: OperationStatus;
  progress?: number;
  logs?: string;
  errorMessage?: string;
}

export interface OperationFilterRequest {
  type?: OperationType;
  status?: OperationStatus;
  userId?: number;
  projectName?: string;
  startDate?: string;
  endDate?: string;
  page?: number;
  pageSize?: number;
}

export interface OperationsListResponse {
  operations: Operation[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ActiveOperationsCountResponse {
  count: number;
}

// Operation Type
export const OperationType = {
  ComposeUp: 'ComposeUp',
  ComposeDown: 'ComposeDown',
  ComposeRestart: 'ComposeRestart',
  ComposeStop: 'ComposeStop',
  ComposeStart: 'ComposeStart',
  ComposePull: 'ComposePull',
  ComposeBuild: 'ComposeBuild',
  ContainerStart: 'ContainerStart',
  ContainerStop: 'ContainerStop',
  ContainerRestart: 'ContainerRestart',
  ContainerRemove: 'ContainerRemove',
  ContainerPause: 'ContainerPause',
  ContainerUnpause: 'ContainerUnpause'
} as const;

export type OperationType = typeof OperationType[keyof typeof OperationType];

// Operation Status
export const OperationStatus = {
  Pending: 'Pending',
  Running: 'Running',
  Completed: 'Completed',
  Failed: 'Failed',
  Cancelled: 'Cancelled'
} as const;

export type OperationStatus = typeof OperationStatus[keyof typeof OperationStatus];

// SignalR Operation Update Event
export interface OperationUpdateEvent {
  operationId: string;
  status: OperationStatus;
  progress: number;
  logs?: string;
  errorMessage?: string;
  timestamp: string;
}
