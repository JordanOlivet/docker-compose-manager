import { apiClient } from './client';

export interface ComposePath {
  id: number;
  path: string;
  isReadOnly: boolean;
  isEnabled: boolean;
}

export interface AddComposePathRequest {
  path: string;
  isReadOnly?: boolean;
}

export interface UpdateComposePathRequest {
  isReadOnly?: boolean;
  isEnabled?: boolean;
}

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
}

const configApi = {
  /**
   * Get all compose paths
   */
  getPaths: async (): Promise<ComposePath[]> => {
    const response = await apiClient.get('/config/paths');
    return response.data.data;
  },

  /**
   * Add new compose path
   */
  addPath: async (data: AddComposePathRequest): Promise<ComposePath> => {
    const response = await apiClient.post('/config/paths', data);
    return response.data.data;
  },

  /**
   * Update compose path
   */
  updatePath: async (id: number, data: UpdateComposePathRequest): Promise<ComposePath> => {
    const response = await apiClient.put(`/config/paths/${id}`, data);
    return response.data.data;
  },

  /**
   * Delete compose path
   */
  deletePath: async (id: number): Promise<void> => {
    await apiClient.delete(`/config/paths/${id}`);
  },

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
   * Browse filesystem directories
   */
  browseDirectories: async (path?: string): Promise<DirectoryBrowseResult> => {
    // Use URLSearchParams to properly encode the path parameter
    const params = new URLSearchParams();
    if (path) {
      params.append('path', path);
    }
    const queryString = params.toString();
    const url = queryString ? `/config/browse?${queryString}` : '/config/browse';
    const response = await apiClient.get(url);
    return response.data.data;
  },
};

export default configApi;


