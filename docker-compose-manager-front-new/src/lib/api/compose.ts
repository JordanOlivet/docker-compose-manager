import { apiClient } from './client';
import type {
  ApiResponseWrapper,
  ComposeFile,
  ComposeFileContent,
  CreateComposeFileRequest,
  UpdateComposeFileRequest,
  ComposeProject,
  ComposeUpRequest,
  ComposeDownRequest,
  ComposeOperationResponse,
  ComposeLogsResponse,
  ComposeLogsRequest,
  ComposeFileDetails,
  DiscoveredComposeFileDto,
  ConflictsResponse,
  ComposeHealthDto,
  RefreshComposeResponse
} from '$lib/types';

// Compose Files API

export const composeApi = {
  // ============================================
  // Compose Discovery Endpoints (New)
  // ============================================

  /**
   * Get all discovered compose files from the configured root directory.
   *
   * Returns all compose files found during automatic discovery, including:
   * - File paths and project names
   * - Validity status (YAML parsing)
   * - Disabled status (x-disabled flag)
   * - Service lists extracted from each file
   *
   * @returns Promise resolving to an array of discovered compose files
   * @throws Error if the request fails or the server returns an error
   */
  getComposeFiles: async (): Promise<DiscoveredComposeFileDto[]> => {
    const response = await apiClient.get<ApiResponseWrapper<DiscoveredComposeFileDto[]>>('/compose/files');
    return response.data.data || [];
  },

  /**
   * Get project name conflicts between compose files.
   *
   * Conflicts occur when multiple compose files have the same project name.
   * To resolve conflicts, add 'x-disabled: true' to unwanted files.
   *
   * @returns Promise resolving to conflict information including conflicting files and resolution steps
   * @throws Error if the request fails or the server returns an error
   */
  getComposeConflicts: async (): Promise<ConflictsResponse> => {
    const response = await apiClient.get<ApiResponseWrapper<ConflictsResponse>>('/compose/conflicts');
    if (!response.data.data) {
      throw new Error('Failed to retrieve conflicts');
    }
    return response.data.data;
  },

  /**
   * Get compose discovery and Docker daemon health status.
   *
   * Returns health information for:
   * - Compose discovery subsystem (directory access, degraded mode)
   * - Docker daemon connection (version, connectivity)
   * - Overall system status (healthy, degraded, critical)
   *
   * No authentication required - this is a diagnostic endpoint.
   *
   * @returns Promise resolving to health status information
   * @throws Error if the request fails or the server returns an error
   */
  getComposeHealth: async (): Promise<ComposeHealthDto> => {
    const response = await apiClient.get<ApiResponseWrapper<ComposeHealthDto>>('/compose/health');
    if (!response.data.data) {
      throw new Error('Failed to retrieve health status');
    }
    return response.data.data;
  },

  /**
   * Manually refresh compose file discovery cache (admin only).
   *
   * Invalidates the cache and performs a fresh filesystem scan.
   * Use this after adding, modifying, or removing compose files.
   *
   * Requires admin role.
   *
   * @returns Promise resolving to refresh results with file count and timestamp
   * @throws Error if the request fails, user is not authorized, or the server returns an error
   */
  refreshComposeFiles: async (): Promise<RefreshComposeResponse> => {
    const response = await apiClient.post<ApiResponseWrapper<RefreshComposeResponse>>('/compose/refresh');
    if (!response.data.data) {
      throw new Error('Failed to refresh compose files');
    }
    return response.data.data;
  },

  // ============================================
  // Legacy Compose Files Endpoints
  // ============================================

  // List all compose files
  listFiles: async (): Promise<ComposeFile[]> => {
    const response = await apiClient.get<ApiResponseWrapper<ComposeFile[]>>('/compose/files');
    return response.data.data || [];
  },

  // Get compose file by ID
  getFile: async (id: number): Promise<ComposeFileContent> => {
    const response = await apiClient.get<ApiResponseWrapper<ComposeFileContent>>(`/compose/files/${id}`);
    if (!response.data.data) {
      throw new Error('File not found');
    }
    return response.data.data;
  },

  // Get compose file by path
  getFileByPath: async (path: string): Promise<ComposeFileContent> => {
    const response = await apiClient.get<ApiResponseWrapper<ComposeFileContent>>(
      '/compose/files/by-path',
      { params: { path } }
    );
    if (!response.data.data) {
      throw new Error('File not found');
    }
    return response.data.data;
  },

  // Create new compose file
  createFile: async (request: CreateComposeFileRequest): Promise<ComposeFileContent> => {
    const response = await apiClient.post<ApiResponseWrapper<ComposeFileContent>>(
      '/compose/files',
      request
    );
    if (!response.data.data) {
      throw new Error('Failed to create file');
    }
    return response.data.data;
  },

  // Update compose file
  updateFile: async (id: number, request: UpdateComposeFileRequest): Promise<ComposeFileContent> => {
    const response = await apiClient.put<ApiResponseWrapper<ComposeFileContent>>(
      `/compose/files/${id}`,
      request
    );
    if (!response.data.data) {
      throw new Error('Failed to update file');
    }
    return response.data.data;
  },

  // Delete compose file
  deleteFile: async (id: number): Promise<void> => {
    await apiClient.delete<ApiResponseWrapper<void>>(`/compose/files/${id}`);
  },

  // Compose Projects API

  // List all compose projects
  listProjects: async (): Promise<ComposeProject[]> => {
    const response = await apiClient.get<ApiResponseWrapper<ComposeProject[]>>('/compose/projects');
    return response.data.data || [];
  },

  // Start compose project (docker-compose up)
  upProject: async (projectName: string, request?: ComposeUpRequest): Promise<ComposeOperationResponse> => {
    const response = await apiClient.post<ApiResponseWrapper<ComposeOperationResponse>>(
      `/compose/projects/${encodeURIComponent(projectName)}/up`,
      request || {}
    );
    if (!response.data.data) {
      throw new Error('Failed to start project');
    }
    return response.data.data;
  },

  // Stop compose project (docker-compose down)
  downProject: async (projectName: string, request?: ComposeDownRequest): Promise<ComposeOperationResponse> => {
    const response = await apiClient.post<ApiResponseWrapper<ComposeOperationResponse>>(
      `/compose/projects/${encodeURIComponent(projectName)}/down`,
      request || {}
    );
    if (!response.data.data) {
      throw new Error('Failed to stop project');
    }
    return response.data.data;
  },

  // Restart compose project
  restartProject: async (projectName: string, timeout?: number): Promise<ComposeOperationResponse> => {
    const response = await apiClient.post<ApiResponseWrapper<ComposeOperationResponse>>(
      `/compose/projects/${encodeURIComponent(projectName)}/restart`,
      { timeout }
    );
    if (!response.data.data) {
      throw new Error('Failed to restart project');
    }
    return response.data.data;
  },

  // Stop compose project services
  stopProject: async (projectName: string, timeout?: number): Promise<ComposeOperationResponse> => {
    const response = await apiClient.post<ApiResponseWrapper<ComposeOperationResponse>>(
      `/compose/projects/${encodeURIComponent(projectName)}/stop`,
      { timeout }
    );
    if (!response.data.data) {
      throw new Error('Failed to stop project');
    }
    return response.data.data;
  },

  // Start compose project services
  startProject: async (projectName: string): Promise<ComposeOperationResponse> => {
    const response = await apiClient.post<ApiResponseWrapper<ComposeOperationResponse>>(
      `/compose/projects/${encodeURIComponent(projectName)}/start`,
      {}
    );
    if (!response.data.data) {
      throw new Error('Failed to start project');
    }
    return response.data.data;
  },

  // Pull compose project images
  pullProject: async (projectName: string, ignorePullFailures?: boolean): Promise<ComposeOperationResponse> => {
    const response = await apiClient.post<ApiResponseWrapper<ComposeOperationResponse>>(
      `/compose/projects/${encodeURIComponent(projectName)}/pull`,
      { ignorePullFailures }
    );
    if (!response.data.data) {
      throw new Error('Failed to pull project images');
    }
    return response.data.data;
  },

  // Build compose project
  buildProject: async (
    projectName: string,
    noCache?: boolean,
    pull?: boolean
  ): Promise<ComposeOperationResponse> => {
    const response = await apiClient.post<ApiResponseWrapper<ComposeOperationResponse>>(
      `/compose/projects/${encodeURIComponent(projectName)}/build`,
      { noCache, pull }
    );
    if (!response.data.data) {
      throw new Error('Failed to build project');
    }
    return response.data.data;
  },

// Get compose project details
  getProjectDetails: async (
    projectName: string
  ): Promise<ComposeProject> => {
    const response = await apiClient.get<ApiResponseWrapper<ComposeProject>>(
      `/compose/projects/${encodeURIComponent(projectName)}`
    );
    if (!response.data.data) {
      throw new Error(`Failed to get project ${projectName}`);
    }
    return response.data.data;
  },

  // Get parsed compose file details with structured information
  getProjectParsedDetails: async (
    projectName: string
  ): Promise<ComposeFileDetails> => {
    const response = await apiClient.get<ApiResponseWrapper<ComposeFileDetails>>(
      `/compose/projects/${encodeURIComponent(projectName)}/parsed`
    );
    if (!response.data.data) {
      throw new Error(`Failed to get parsed details for project ${projectName}`);
    }
    return response.data.data;
  },

  // Get compose project logs
  getProjectLogs: async (
    projectName: string,
    request?: ComposeLogsRequest
  ): Promise<ComposeLogsResponse> => {
    const response = await apiClient.post<ApiResponseWrapper<ComposeLogsResponse>>(
      `/compose/projects/${encodeURIComponent(projectName)}/logs`,
      request || {}
    );
    if (!response.data.data) {
      throw new Error('Failed to get project logs');
    }
    return response.data.data;
  }
};
