import { apiClient } from './client';
import type {
  AppUpdateCheckResponse,
  UpdateTriggerResponse,
  UpdateTriggerRequest,
  UpdateStatusResponse,
  ProjectUpdateCheckResponse,
  ProjectUpdateRequest,
  ProjectUpdateSummary,
  UpdateAllResponse
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

// ============================================
// Compose Project Update API
// ============================================

/**
 * Check for available updates for a project's images
 */
export const checkProjectUpdates = async (projectName: string): Promise<ProjectUpdateCheckResponse> => {
  const response = await apiClient.get(`/compose/projects/${encodeURIComponent(projectName)}/check-updates`);
  return response.data.data;
};

/**
 * Update selected services in a project
 */
export const updateProject = async (projectName: string, request: ProjectUpdateRequest): Promise<UpdateTriggerResponse> => {
  const response = await apiClient.post(`/compose/projects/${encodeURIComponent(projectName)}/update`, request);
  return response.data.data;
};

/**
 * Get global update status for all cached projects
 */
export const getProjectUpdateStatus = async (): Promise<ProjectUpdateSummary[]> => {
  const response = await apiClient.get('/compose/update-status');
  return response.data.data;
};

/**
 * Update all projects that have available updates
 */
export const updateAllProjects = async (): Promise<UpdateAllResponse> => {
  const response = await apiClient.post('/compose/update-all');
  return response.data.data;
};

/**
 * Clear the update check cache
 */
export const clearUpdateCache = async (): Promise<void> => {
  await apiClient.post('/compose/clear-update-cache');
};

export const updateApi = {
  // App update methods
  checkAppUpdate,
  triggerAppUpdate,
  getUpdateStatus,
  // Project update methods
  checkProjectUpdates,
  updateProject,
  getProjectUpdateStatus,
  updateAllProjects,
  clearUpdateCache
};
