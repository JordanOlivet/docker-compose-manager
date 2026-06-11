import { apiClient } from './client';

export interface UpdateSettingRequest {
  value: string;
  description?: string;
}

export interface DirectoryInfo {
  name: string;
  path: string;
  isAccessible: boolean;
}

export interface DirectoryBrowseResult {
  currentPath: string;
  parentPath?: string;
  directories: DirectoryInfo[];
  files: DirectoryInfo[];
}

export interface LogLevelInfo {
  current: string;
  available: string[];
}

const configApi = {
  /**
   * Get all settings
   */
  getSettings: async (): Promise<Record<string, string>> => {
    const response = await apiClient.get('/config/settings');
    return response.data.data;
  },

  /**
   * Update setting
   */
  updateSetting: async (key: string, data: UpdateSettingRequest): Promise<void> => {
    await apiClient.put(`/config/settings/${key}`, data);
  },

  /**
   * Delete setting
   */
  deleteSetting: async (key: string): Promise<void> => {
    await apiClient.delete(`/config/settings/${key}`);
  },

  /**
   * Get current application log level and available levels
   */
  getLogLevel: async (): Promise<LogLevelInfo> => {
    const response = await apiClient.get('/config/log-level');
    return response.data.data;
  },

  /**
   * Update application log level (takes effect immediately)
   */
  updateLogLevel: async (value: string): Promise<LogLevelInfo> => {
    const response = await apiClient.put('/config/log-level', { value });
    return response.data.data;
  },

  /**
   * Browse filesystem directories
   */
  browseDirectories: async (path?: string, includeFiles = false): Promise<DirectoryBrowseResult> => {
    // Use URLSearchParams to properly encode the path parameter
    const params = new URLSearchParams();
    if (path) {
      params.append('path', path);
    }
    if (includeFiles) {
      params.append('includeFiles', 'true');
    }
    const queryString = params.toString();
    const url = queryString ? `/config/browse?${queryString}` : '/config/browse';
    const response = await apiClient.get(url);
    return response.data.data;
  },
};

export default configApi;
