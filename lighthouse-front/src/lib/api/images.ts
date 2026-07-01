import { apiClient } from './client';
import type { ApiResponseWrapper, Image, PruneImagesResult } from '$lib/types';

export const imagesApi = {
  list: async (): Promise<Image[]> => {
    const response = await apiClient.get<ApiResponseWrapper<Image[]>>('/images');
    return response.data.data!;
  },

  remove: async (id: string, force: boolean = false): Promise<void> => {
    await apiClient.delete(`/images/${id}`, { params: { force } });
  },

  prune: async (danglingOnly: boolean = true): Promise<PruneImagesResult> => {
    const response = await apiClient.post<ApiResponseWrapper<PruneImagesResult>>('/images/prune', {
      danglingOnly,
    });
    return response.data.data!;
  },
};
