import { apiClient } from './client';
import type {
  AppUpdateCheckResponse,
  UpdateTriggerResponse,
  UpdateTriggerRequest,
  UpdateStatusResponse
} from '$lib/types/update';

/**
 * Check for available application updates
 */
export const checkAppUpdate = async (): Promise<AppUpdateCheckResponse> => {
  const response = await apiClient.get('/system/check-update');
  return response.data.data;
};

/**
 * Trigger application update
 */
export const triggerAppUpdate = async (request?: UpdateTriggerRequest): Promise<UpdateTriggerResponse> => {
  const response = await apiClient.post('/system/update', request ?? {});
  return response.data.data;
};

/**
 * Get current update status
 */
export const getUpdateStatus = async (): Promise<UpdateStatusResponse> => {
  const response = await apiClient.get('/system/update-status');
  return response.data.data;
};

export const updateApi = {
  checkAppUpdate,
  triggerAppUpdate,
  getUpdateStatus
};
