// Helper pour extraire response.data.data
import type { AxiosResponse } from 'axios';
import type { ApiResponseWrapper } from '$lib/types';

export function unwrap<T>(promise: Promise<AxiosResponse<ApiResponseWrapper<T>>>): Promise<T> {
  return promise.then(r => r.data.data!);
}
