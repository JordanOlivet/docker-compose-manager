import { apiClient } from './client';
import type { ApiResponseWrapper, LogPage } from '$lib/types';

// EventSource cannot set Authorization headers, so log streams carry the access token
// as a query param and build the full `/api/...` URL themselves (bypassing axios).
function getApiBase(): string {
  const viteApiUrl = import.meta.env.VITE_API_URL;
  return viteApiUrl !== undefined && viteApiUrl !== '' ? viteApiUrl : '';
}

export interface ContainerHistoryQuery {
  tail?: number;
  until?: string;
}

export interface ProjectHistoryQuery extends ContainerHistoryQuery {
  services?: string;
}

export interface ContainerStreamOptions {
  tail?: number;
  since?: string;
  token: string;
}

export interface ProjectStreamOptions extends ContainerStreamOptions {
  services?: string;
}

export const logsApi = {
  getContainerHistory: async (id: string, query: ContainerHistoryQuery = {}): Promise<LogPage> => {
    const response = await apiClient.get<ApiResponseWrapper<LogPage>>(
      `/containers/${encodeURIComponent(id)}/logs/history`,
      { params: query }
    );
    return response.data.data!;
  },

  getProjectHistory: async (name: string, query: ProjectHistoryQuery = {}): Promise<LogPage> => {
    const response = await apiClient.get<ApiResponseWrapper<LogPage>>(
      `/compose/projects/${encodeURIComponent(name)}/logs/history`,
      { params: query }
    );
    return response.data.data!;
  },
};

export function buildContainerStreamUrl(id: string, options: ContainerStreamOptions): string {
  const params = new URLSearchParams({ access_token: options.token });
  if (options.since) params.set('since', options.since);
  else if (options.tail != null) params.set('tail', String(options.tail));
  return `${getApiBase()}/api/containers/${encodeURIComponent(id)}/logs/stream?${params}`;
}

export function buildProjectStreamUrl(name: string, options: ProjectStreamOptions): string {
  const params = new URLSearchParams({ access_token: options.token });
  if (options.since) params.set('since', options.since);
  else if (options.tail != null) params.set('tail', String(options.tail));
  if (options.services) params.set('services', options.services);
  return `${getApiBase()}/api/compose/projects/${encodeURIComponent(name)}/logs/stream?${params}`;
}
