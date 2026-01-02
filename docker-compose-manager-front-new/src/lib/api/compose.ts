import { apiClient } from './client';
import type {
  ApiResponseWrapper,
  ComposeFile,
  ComposeFileContent,
  CreateComposeFileRequest,
  UpdateComposeFileRequest,
  ComposeProject,
  ComposeProjectDto,
  ComposeService,
  ComposeUpRequest,
  ComposeDownRequest,
  ComposeOperationResponse,
  ComposeLogsResponse,
  ComposeLogsRequest,
  ComposeFileDetails
} from '$lib/types';

// Compose Files API

export const composeApi = {
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

  // Compose Projects API (NEW ARCHITECTURE - Docker-only discovery)

  /**
   * List all compose projects using Docker-only discovery
   * Source: `docker compose ls --all --format json`
   * @param refresh - Force cache refresh (bypass 10s cache)
   * @returns Array of ComposeProjectDto with permissions and status
   */
  listProjects: async (refresh = false): Promise<ComposeProjectDto[]> => {
    const response = await apiClient.get<ApiResponseWrapper<ComposeProjectDto[]>>(
      '/compose/projects',
      { params: { refresh } }
    );
    return response.data.data || [];
  },

  /**
   * Get a specific project by name
   * @param projectName - Docker project name
   * @returns Project details with permissions
   */
  getProject: async (projectName: string): Promise<ComposeProjectDto> => {
    const response = await apiClient.get<ApiResponseWrapper<ComposeProjectDto>>(
      `/compose/projects/${encodeURIComponent(projectName)}`
    );
    if (!response.data.data) {
      throw new Error(`Project '${projectName}' not found or access denied`);
    }
    return response.data.data;
  },

  /**
   * Get services for a specific project (via docker compose ps)
   * Real-time data, no cache
   * @param projectName - Docker project name
   * @returns Array of services with their status
   */
  getProjectServices: async (projectName: string): Promise<ComposeService[]> => {
    const response = await apiClient.get<ApiResponseWrapper<ComposeService[]>>(
      `/compose/projects/${encodeURIComponent(projectName)}/services`
    );
    return response.data.data || [];
  },

  /**
   * Force refresh the projects cache
   * Invalidates the 10s memory cache on the backend
   */
  refreshCache: async (): Promise<void> => {
    await apiClient.post<ApiResponseWrapper<{ message: string }>>(
      '/compose/projects/refresh'
    );
  },

  /**
   * Start compose project (docker compose up)
   * Uses -p projectName flag, no file access required
   * @param projectName - Docker project name
   * @param request - Optional parameters (build, detach, etc.)
   */
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

  /**
   * Stop compose project (docker compose down)
   * Uses -p projectName flag, no file access required
   * @param projectName - Docker project name
   * @param request - Optional parameters (removeVolumes, etc.)
   */
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

  /**
   * Restart compose project
   * Uses -p projectName flag, no file access required
   * @param projectName - Docker project name
   * @param timeout - Optional timeout in seconds
   */
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
