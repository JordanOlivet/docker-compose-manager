import axios, { AxiosError } from 'axios';
import type { InternalAxiosRequestConfig } from 'axios';
import * as auth from '$lib/stores/auth.svelte';
import { reconnectSSEWithNewToken } from '$lib/stores/sse.svelte';
import { browser } from '$app/environment';

// Extend InternalAxiosRequestConfig to include _retry property for token refresh
interface RetryableRequestConfig extends InternalAxiosRequestConfig {
  _retry?: boolean;
  _retryCount?: number;
}

// Use relative URL by default so requests are proxied (Vite proxy in dev, nginx in prod).
// Override by setting VITE_API_URL (e.g. in .env.development.local for a remote backend).
const getApiUrl = () => {
  if (!browser) return '';

  const viteApiUrl = import.meta.env.VITE_API_URL;
  if (viteApiUrl !== undefined && viteApiUrl !== '') {
    return viteApiUrl;
  }
  return '';
};

const API_URL = getApiUrl();

/**
 * Check if a JWT token is near expiration (within 10 minutes).
 */
function isTokenNearExpiration(token: string): boolean {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    const expiresAt = payload.exp * 1000;
    const timeLeft = expiresAt - Date.now();
    // Refresh if less than 10 minutes remaining
    return timeLeft < 10 * 60 * 1000;
  } catch {
    return true; // If parsing fails, consider it expired
  }
}

/**
 * Proactively refresh the access token if it's near expiration.
 * Returns true if refresh was successful or not needed, false on failure.
 */
async function proactiveTokenRefresh(): Promise<boolean> {
  if (!browser) return true;

  const token = localStorage.getItem('accessToken');
  if (!token) return true; // No token, nothing to refresh

  if (!isTokenNearExpiration(token)) {
    return true; // Token is still valid, no refresh needed
  }

  try {
    // The refresh token is sent automatically via the HttpOnly `lh_refresh` cookie
    // (withCredentials), so no token is read from or written to JavaScript storage.
    const refreshUrl = API_URL ? `${API_URL}/api/auth/refresh` : '/api/auth/refresh';
    const response = await axios.post(refreshUrl, {}, { withCredentials: true });

    const { accessToken } = response.data.data;

    localStorage.setItem('accessToken', accessToken);

    // Synchronize with Svelte store
    auth.refreshTokens(accessToken);

    // Reconnect SSE with the new token
    reconnectSSEWithNewToken();

    return true;
  } catch (error) {
    // If refresh fails, log out the user
    auth.logout();
    window.location.href = '/login';
    return false;
  }
}

// Proactive token refresh: check every 60 seconds
if (browser) {
  setInterval(() => {
    proactiveTokenRefresh();
  }, 60_000);

  // Refresh token when user returns to the tab after being away
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') {
      proactiveTokenRefresh();
    }
  });
}

export const apiClient = axios.create({
  baseURL: API_URL ? `${API_URL}/api` : '/api',
  headers: {
    'Content-Type': 'application/json',
  },
  // Send the HttpOnly refresh-token cookie on auth requests (login/refresh/logout).
  withCredentials: true,
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
        auth.logout();
        window.location.href = '/login';
        return Promise.reject(error);
      }

      try {
        // Refresh via the HttpOnly cookie (withCredentials); no token in JS storage.
        const refreshUrl = API_URL ? `${API_URL}/api/auth/refresh` : '/api/auth/refresh';
        const response = await axios.post(refreshUrl, {}, { withCredentials: true });

        const { accessToken } = response.data.data;

        localStorage.setItem('accessToken', accessToken);

        // Synchronise le store Svelte après refresh
        auth.refreshTokens(accessToken);

        // Reconnect SSE with the new token
        reconnectSSEWithNewToken();

        originalRequest.headers.Authorization = `Bearer ${accessToken}`;
        return apiClient(originalRequest);
      } catch (refreshError) {
        // Refresh failed, logout user via store
        auth.logout();
        window.location.href = '/login';
        return Promise.reject(refreshError);
      }
    }

    return Promise.reject(error);
  }
);
