import { browser } from '$app/environment';
import type { User } from '$lib/types';

interface AuthState {
  user: User | null;
  accessToken: string | null;
  refreshToken: string | null;
}

function createAuthStore() {
  let state = $state<AuthState>({
    user: null,
    accessToken: browser ? localStorage.getItem('accessToken') : null,
    refreshToken: browser ? localStorage.getItem('refreshToken') : null,
  });

  return {
    get user() { return state.user; },
    get accessToken() { return state.accessToken; },
    get refreshToken() { return state.refreshToken; },
    get isAuthenticated() { return !!state.accessToken; },
    get isAdmin() { return state.user?.role?.toLowerCase() === 'admin'; },

    login(accessToken: string, refreshToken: string, user: User) {
      if (browser) {
        localStorage.setItem('accessToken', accessToken);
        localStorage.setItem('refreshToken', refreshToken);
      }
      state.user = user;
      state.accessToken = accessToken;
      state.refreshToken = refreshToken;
    },

    logout() {
      if (browser) {
        localStorage.removeItem('accessToken');
        localStorage.removeItem('refreshToken');
      }
      state.user = null;
      state.accessToken = null;
      state.refreshToken = null;
    },

    updateUser(user: User) {
      state.user = user;
    },

    refreshTokens(accessToken: string, refreshToken: string) {
      if (browser) {
        localStorage.setItem('accessToken', accessToken);
        localStorage.setItem('refreshToken', refreshToken);
      }
      state.accessToken = accessToken;
      state.refreshToken = refreshToken;
    },
  };
}

export const authStore = createAuthStore();
