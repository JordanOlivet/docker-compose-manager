import { apiClient } from './client';

export interface DevTestStatus {
  filesCreated: boolean;
  nginxImageExists: boolean;
  whoamiImageExists: boolean;
  effectiveRootPath: string;
  nginxComposePath: string;
  whoamiComposePath: string;
}

export interface DevTestActionResult {
  success: boolean;
  logs: string[];
  error: string | null;
}

export interface MaintenanceDevStatus {
  instanceId: string;
  isReady: boolean;
  uptimeSeconds: number;
  startupTimestamp: string;
}

export const devTestApi = {
  // Compose testing
  getStatus: (): Promise<DevTestStatus> =>
    apiClient.get('/dev/test-compose/status').then(r => r.data.data),
  setup: (): Promise<DevTestActionResult> =>
    apiClient.post('/dev/test-compose/setup').then(r => r.data.data),
  forceOutdated: (): Promise<DevTestActionResult> =>
    apiClient.post('/dev/test-compose/force-outdated', {}, { timeout: 300000 }).then(r => r.data.data),
  restore: (): Promise<DevTestActionResult> =>
    apiClient.post('/dev/test-compose/restore', {}, { timeout: 300000 }).then(r => r.data.data),
  teardown: (): Promise<DevTestActionResult> =>
    apiClient.delete('/dev/test-compose/teardown').then(r => r.data.data),

  // Maintenance mode simulation
  getMaintenanceStatus: (): Promise<MaintenanceDevStatus> =>
    apiClient.get('/dev/maintenance/status').then(r => r.data.data),
  simulateMaintenance: (delaySeconds: number): Promise<DevTestActionResult> =>
    apiClient.post(`/dev/maintenance/simulate?delaySeconds=${delaySeconds}`, {}, { timeout: 120000 }).then(r => r.data.data),
  resetInstance: (): Promise<DevTestActionResult> =>
    apiClient.post('/dev/maintenance/reset-instance').then(r => r.data.data),
  setReady: (ready: boolean): Promise<DevTestActionResult> =>
    apiClient.post(`/dev/maintenance/set-ready?ready=${ready}`).then(r => r.data.data),
};
