import { useEffect, useState } from 'react';
import { useAuthStore } from '../stores/authStore';
import { authApi } from '../api';

interface AuthInitializerProps {
  children: React.ReactNode;
}

/**
 * Component that initializes user data on app startup
 * If a token exists in localStorage, fetches the current user from the API
 */
export function AuthInitializer({ children }: AuthInitializerProps) {
  const { user, isAuthenticated, updateUser, logout } = useAuthStore();
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const initializeAuth = async () => {
      // If user is already loaded, no need to fetch again
      if (user) {
        setIsLoading(false);
        return;
      }

      // If we have a token but no user, fetch the user
      if (isAuthenticated) {
        try {
          const currentUser = await authApi.getCurrentUser();
          updateUser(currentUser);
        } catch (error) {
          console.error('Failed to fetch current user:', error);
          // If token is invalid, logout
          logout();
        }
      }

      setIsLoading(false);
    };

    initializeAuth();
  }, [user, isAuthenticated, updateUser, logout]);

  // Show loading state while initializing
  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-100 dark:bg-gray-900">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto mb-4"></div>
          <p className="text-gray-600 dark:text-gray-400">Loading...</p>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
