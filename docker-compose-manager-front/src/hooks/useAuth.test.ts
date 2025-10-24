import { describe, it, expect, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useAuth } from './useAuth';
import { useAuthStore } from '../stores/authStore';

describe('useAuth', () => {
  beforeEach(() => {
    // Reset store before each test
    useAuthStore.setState({
      user: null,
      accessToken: null,
      refreshToken: null,
      isAuthenticated: false,
    });
  });

  it('should return initial unauthenticated state', () => {
    const { result } = renderHook(() => useAuth());

    expect(result.current.isAuthenticated).toBe(false);
    expect(result.current.user).toBeNull();
    expect(result.current.isAdmin).toBe(false);
  });

  it('should identify admin user correctly', () => {
    // Set admin user in store
    useAuthStore.setState({
      user: {
        id: 1,
        username: 'admin',
        role: 'admin',
        isEnabled: true,
        mustChangePassword: false,
        createdAt: new Date().toISOString(),
      },
      accessToken: 'token',
      refreshToken: 'refresh',
      isAuthenticated: true,
    });

    const { result } = renderHook(() => useAuth());

    expect(result.current.isAuthenticated).toBe(true);
    expect(result.current.isAdmin).toBe(true);
    expect(result.current.user?.role).toBe('admin');
  });

  it('should identify regular user correctly', () => {
    // Set regular user in store
    useAuthStore.setState({
      user: {
        id: 2,
        username: 'user',
        role: 'user',
        isEnabled: true,
        mustChangePassword: false,
        createdAt: new Date().toISOString(),
      },
      accessToken: 'token',
      refreshToken: 'refresh',
      isAuthenticated: true,
    });

    const { result } = renderHook(() => useAuth());

    expect(result.current.isAuthenticated).toBe(true);
    expect(result.current.isAdmin).toBe(false);
    expect(result.current.user?.role).toBe('user');
  });

  it('should provide login and logout functions', () => {
    const { result } = renderHook(() => useAuth());

    expect(typeof result.current.login).toBe('function');
    expect(typeof result.current.logout).toBe('function');
  });
});
