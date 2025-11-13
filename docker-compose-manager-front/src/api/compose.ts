import { apiClient } from './client';
import type {
  ApiResponse,
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
  ComposeFileDetails
} from '../types';

// Compose Files API

export const composeApi = {
  // List all compose files
  listFiles: async (): Promise<ComposeFile[]> => {
    const response = await apiClient.get<ApiResponse<ComposeFile[]>>('/compose/files');
    return response.data.data || [];
  },

  // Get compose file by ID
  getFile: async (id: number): Promise<ComposeFileContent> => {
    const response = await apiClient.get<ApiResponse<ComposeFileContent>>(`/compose/files/${id}`);
    if (!response.data.data) {
      throw new Error('File not found');
    }
    return response.data.data;
  },

  // Get compose file by path
  getFileByPath: async (path: string): Promise<ComposeFileContent> => {
    const response = await apiClient.get<ApiResponse<ComposeFileContent>>(
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
    const response = await apiClient.post<ApiResponse<ComposeFileContent>>(
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
    const response = await apiClient.put<ApiResponse<ComposeFileContent>>(
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
    await apiClient.delete<ApiResponse<void>>(`/compose/files/${id}`);
  },

  // Compose Projects API

  // List all compose projects
  listProjects: async (): Promise<ComposeProject[]> => {
    const response = await apiClient.get<ApiResponse<ComposeProject[]>>('/compose/projects');
    return response.data.data || [];
  },

  // Start compose project (docker-compose up)
  upProject: async (projectName: string, request?: ComposeUpRequest): Promise<ComposeOperationResponse> => {
    const response = await apiClient.post<ApiResponse<ComposeOperationResponse>>(
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
    const response = await apiClient.post<ApiResponse<ComposeOperationResponse>>(
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
    const response = await apiClient.post<ApiResponse<ComposeOperationResponse>>(
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
    const response = await apiClient.post<ApiResponse<ComposeOperationResponse>>(
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
    const response = await apiClient.post<ApiResponse<ComposeOperationResponse>>(
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
    const response = await apiClient.post<ApiResponse<ComposeOperationResponse>>(
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
    const response = await apiClient.post<ApiResponse<ComposeOperationResponse>>(
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
    const response = await apiClient.get<ApiResponse<ComposeProject>>(
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
    const response = await apiClient.get<ApiResponse<ComposeFileDetails>>(
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
    const response = await apiClient.post<ApiResponse<ComposeLogsResponse>>(
      `/compose/projects/${encodeURIComponent(projectName)}/logs`,
      request || {}
    );
    if (!response.data.data) {
      throw new Error('Failed to get project logs');
    }
    return response.data.data;
  }
};
