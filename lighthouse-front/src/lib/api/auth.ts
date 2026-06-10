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

export const changePassword = async (currentPassword: string, newPassword: string): Promise<LoginResponse> => {
  const response = await apiClient.put<ApiResponseWrapper<LoginResponse>>('/auth/change-password', { currentPassword, newPassword });
  return response.data.data!;
};

export const requestPasswordReset = async (usernameOrEmail: string): Promise<void> => {
  await apiClient.post('/auth/forgot-password', { usernameOrEmail });
};

export const validateResetToken = async (token: string): Promise<boolean> => {
  try {
    const response = await apiClient.get<ApiResponseWrapper<boolean>>(`/auth/validate-reset-token/${token}`);
    return response.data.data ?? false;
  } catch {
    return false;
  }
};

export const resetPassword = async (token: string, newPassword: string): Promise<void> => {
  await apiClient.post('/auth/reset-password', { token, newPassword });
};

export const addEmail = async (email: string): Promise<void> => {
  await apiClient.put('/auth/add-email', { email });
};

export const authApi = {
  login,
  logout,
  getCurrentUser,
  changePassword,
  requestPasswordReset,
  validateResetToken,
  resetPassword,
  addEmail,
};
