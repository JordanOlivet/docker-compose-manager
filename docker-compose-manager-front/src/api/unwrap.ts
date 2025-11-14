// Helper pour extraire response.data.data
import type { AxiosResponse } from 'axios';
import type { ApiResponse } from '../types';

export function unwrap<T>(promise: Promise<AxiosResponse<ApiResponse<T>>>): Promise<T> {
  return promise.then(r => r.data.data!);
}
