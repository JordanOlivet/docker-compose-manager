import { browser } from '$app/environment';
import type { User } from '$lib/types';

// Svelte 5 pattern: export state object with properties (not individual $state variables)
export const auth = $state({
  user: null as User | null,
  accessToken: browser ? localStorage.getItem('accessToken') : null,
  refreshToken: browser ? localStorage.getItem('refreshToken') : null
});

// Derived state as getters - Svelte 5 doesn't allow exporting $derived
export const isAuthenticated = {
  get current() { return !!auth.accessToken; }
};
export const isAdmin = {
  get current() { return auth.user?.role?.toLowerCase() === 'admin'; }
};

// Actions
export function login(newAccessToken: string, newRefreshToken: string, newUser: User) {
  if (browser) {
    localStorage.setItem('accessToken', newAccessToken);
    localStorage.setItem('refreshToken', newRefreshToken);
  }
  auth.user = newUser;
  auth.accessToken = newAccessToken;
  auth.refreshToken = newRefreshToken;
}

export function logout() {
  if (browser) {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
  }
  auth.user = null;
  auth.accessToken = null;
  auth.refreshToken = null;
}

export function updateUser(newUser: User) {
  auth.user = newUser;
}

export function refreshTokens(newAccessToken: string, newRefreshToken: string) {
  if (browser) {
    localStorage.setItem('accessToken', newAccessToken);
    localStorage.setItem('refreshToken', newRefreshToken);
  }
  auth.accessToken = newAccessToken;
  auth.refreshToken = newRefreshToken;
}
