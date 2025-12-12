import axios, { AxiosError } from 'axios';
import type { InternalAxiosRequestConfig } from 'axios';
import { authStore } from '$lib/stores/auth.svelte';
import { browser } from '$app/environment';

// Extend InternalAxiosRequestConfig to include _retry property for token refresh
interface RetryableRequestConfig extends InternalAxiosRequestConfig {
  _retry?: boolean;
  _retryCount?: number;
}

// Use relative URL in production (nginx proxy), or env variable for development
// In production (built in Docker), VITE_API_URL should be empty/undefined to use nginx proxy
const getApiUrl = () => {
  if (!browser) return '';
  
  const viteApiUrl = import.meta.env.VITE_API_URL;
  // If VITE_API_URL is explicitly set (even if empty string), use it
  if (viteApiUrl !== undefined && viteApiUrl !== '') {
    return viteApiUrl;
  }
  // In production build, use empty string for relative URLs (nginx proxy)
  if (import.meta.env.PROD) {
    return '';
  }
  // In development, default to local backend
  return 'http://localhost:5000';
};

const API_URL = getApiUrl();

export const apiClient = axios.create({
  baseURL: API_URL ? `${API_URL}/api` : '/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor to add auth token
apiClient.interceptors.request.use(
  (config) => {
    if (!browser) return config;
    
    const token = localStorage.getItem('accessToken');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Response interceptor to handle token refresh
apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    if (!browser) return Promise.reject(error);
    
    const originalRequest = error.config as RetryableRequestConfig;

    // Don't attempt token refresh for auth endpoints
    const isAuthEndpoint = originalRequest?.url?.includes('/auth/login') ||
                          originalRequest?.url?.includes('/auth/refresh');

    if (error.response?.status === 401 && !isAuthEndpoint) {
      originalRequest._retryCount = (originalRequest._retryCount || 0) + 1;
      if (originalRequest._retryCount > 2) {
        // Trop de tentatives, logout
        authStore.logout();
        window.location.href = '/login';
        return Promise.reject(error);
      }

      try {
        const refreshToken = localStorage.getItem('refreshToken');
        if (!refreshToken) {
          throw new Error('No refresh token available');
        }

        const refreshUrl = API_URL ? `${API_URL}/api/auth/refresh` : '/api/auth/refresh';
        const response = await axios.post(refreshUrl, {
          refreshToken,
        });

        const { accessToken, refreshToken: newRefreshToken } = response.data.data;

        localStorage.setItem('accessToken', accessToken);
        localStorage.setItem('refreshToken', newRefreshToken);

        // Synchronise le store Svelte après refresh
        authStore.refreshTokens(accessToken, newRefreshToken);

        originalRequest.headers.Authorization = `Bearer ${accessToken}`;
        return apiClient(originalRequest);
      } catch (refreshError) {
        // Refresh failed, logout user via store
        authStore.logout();
        window.location.href = '/login';
        return Promise.reject(refreshError);
      }
    }

    return Promise.reject(error);
  }
);
