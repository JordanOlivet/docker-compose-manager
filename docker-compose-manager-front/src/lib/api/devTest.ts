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

export const devTestApi = {
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
};
