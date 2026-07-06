import { apiClient } from './client';
import type { ApiResponseWrapper } from '$lib/types';

// EventSource cannot set Authorization headers, so the app-log stream carries the
// access token as a query param and builds the full `/api/...` URL itself.
function getApiBase(): string {
  const viteApiUrl = import.meta.env.VITE_API_URL;
  return viteApiUrl !== undefined && viteApiUrl !== '' ? viteApiUrl : '';
}

export interface AppLogEntry {
  timestamp: string;
  level: string;
  category?: string;
  username?: string;
  message: string;
  exception?: string;
}

export interface AppLogPage {
  entries: AppLogEntry[];
  hasMore: boolean;
}

export interface AppLogFilter {
  levels?: string[];
  category?: string;
  user?: string;
  search?: string;
}

export interface AppLogHistoryQuery extends AppLogFilter {
  tail?: number;
  until?: string;
}

function toParams(query: AppLogHistoryQuery): Record<string, string> {
  const params: Record<string, string> = {};
  if (query.tail != null) params.tail = String(query.tail);
  if (query.until) params.until = query.until;
  if (query.levels && query.levels.length > 0) params.levels = query.levels.join(',');
  if (query.category) params.category = query.category;
  if (query.user) params.user = query.user;
  if (query.search) params.search = query.search;
  return params;
}

export const appLogsApi = {
  getHistory: async (query: AppLogHistoryQuery = {}): Promise<AppLogPage> => {
    const response = await apiClient.get<ApiResponseWrapper<AppLogPage>>('/app-logs/history', {
      params: toParams(query)
    });
    return response.data.data!;
  }
};

export function buildAppLogStreamUrl(
  token: string,
  filter: AppLogFilter & { tail?: number }
): string {
  const params = new URLSearchParams({ access_token: token });
  if (filter.tail != null) params.set('tail', String(filter.tail));
  if (filter.levels && filter.levels.length > 0) params.set('levels', filter.levels.join(','));
  if (filter.category) params.set('category', filter.category);
  if (filter.user) params.set('user', filter.user);
  if (filter.search) params.set('search', filter.search);
  return `${getApiBase()}/api/app-logs/stream?${params}`;
}
