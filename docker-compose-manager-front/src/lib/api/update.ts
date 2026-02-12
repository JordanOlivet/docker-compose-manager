import { apiClient } from './client';
import type {
  AppUpdateCheckResponse,
  UpdateTriggerResponse,
  UpdateTriggerRequest,
  UpdateStatusResponse,
  ProjectUpdateCheckResponse,
  ProjectUpdateRequest,
  ProjectUpdateSummary,
  UpdateAllResponse,
  CheckAllUpdatesResponse,
  ContainerUpdateCheckResponse,
  ContainerUpdateSummary,
  ContainerUpdatesCheckedEvent
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
 * Note: Long timeout (30 min) to allow large image pulls
 */
export const updateProject = async (projectName: string, request: ProjectUpdateRequest): Promise<UpdateTriggerResponse> => {
  const response = await apiClient.post(
    `/compose/projects/${encodeURIComponent(projectName)}/update`,
    request,
    { timeout: 1800000 } // 30 minutes timeout for large image pulls
  );
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

/**
 * Check for updates across all projects with compose files
 */
export const checkAllProjectUpdates = async (): Promise<CheckAllUpdatesResponse> => {
  const response = await apiClient.post('/compose/check-all-updates');
  return response.data.data;
};

// ============================================
// Container Update API
// ============================================

/**
 * Check if an update is available for a container's image
 */
export const checkContainerUpdate = async (containerId: string): Promise<ContainerUpdateCheckResponse> => {
  const response = await apiClient.get(`/containers/${encodeURIComponent(containerId)}/check-update`);
  return response.data.data;
};

/**
 * Update a container (pull new image and recreate)
 */
export const updateContainer = async (containerId: string): Promise<UpdateTriggerResponse> => {
  const response = await apiClient.post(
    `/containers/${encodeURIComponent(containerId)}/update`,
    {},
    { timeout: 1800000 } // 30 minutes timeout
  );
  return response.data.data;
};

/**
 * Get cached container update status (does not trigger new checks)
 */
export const getContainerUpdateStatus = async (): Promise<ContainerUpdateSummary[]> => {
  const response = await apiClient.get('/containers/update-status');
  return response.data.data;
};

/**
 * Check all containers for available updates
 */
export const checkAllContainerUpdates = async (): Promise<ContainerUpdatesCheckedEvent> => {
  const response = await apiClient.post('/containers/check-all-updates');
  return response.data.data;
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
  clearUpdateCache,
  checkAllProjectUpdates,
  // Container update methods
  checkContainerUpdate,
  updateContainer,
  getContainerUpdateStatus,
  checkAllContainerUpdates
};
