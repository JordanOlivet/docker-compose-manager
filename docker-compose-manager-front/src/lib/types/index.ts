// Re-export all types from individual modules
export * from './compose';
export * from './container';
export * from './operations';
export * from './audit';
export * from './permissions';
export * from './global';
export * from './api';
export * from './registry';

// Base types
export interface User {
  id: number;
  username: string;
  email?: string;
  role: string;
  isEnabled: boolean;
  mustChangePassword: boolean;
  mustAddEmail: boolean;
  createdAt: string;
  lastLoginAt?: string;
}

export interface LoginRequest {
  username: string;
  password: string;
  rememberMe?: boolean;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  username: string;
  role: string;
  mustChangePassword: boolean;
  mustAddEmail: boolean;
}

export interface ApiResponseWrapper<T> {
  data?: T;
  success: boolean;
  message?: string;
  errors?: Record<string, string[]>;
  errorCode?: string;
}
