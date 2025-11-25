import { apiClient } from './client';
import type { LoginRequest, LoginResponse, ApiResponseWrapper, User } from '$lib/types';


export const login = async (credentials: LoginRequest): Promise<LoginResponse> => {
  const response = await apiClient.post<ApiResponseWrapper<LoginResponse>>('/auth/login', credentials);
  return response.data.data!;
};

export const logout = async (refreshToken: string): Promise<void> => {
  await apiClient.post('/auth/logout', { refreshToken });
};

export const getCurrentUser = async (): Promise<User> => {
  const response = await apiClient.get<ApiResponseWrapper<User>>('/auth/me');
  return response.data.data!;
};

export const changePassword = async (currentPassword: string, newPassword: string): Promise<void> => {
  await apiClient.put('/auth/change-password', { currentPassword, newPassword });
};

export const authApi = {
  login,
  logout,
  getCurrentUser,
  changePassword,
};


