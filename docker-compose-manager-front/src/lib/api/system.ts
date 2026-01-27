import { apiClient } from './client';

export interface VersionInfo {
  version: string;
  buildDate: string;
  gitCommit: string;
  environment: string;
}

export const getVersion = async (): Promise<VersionInfo> => {
  const response = await apiClient.get('/system/version');
  return response.data.data;
};

export const systemApi = {
  getVersion,
};
