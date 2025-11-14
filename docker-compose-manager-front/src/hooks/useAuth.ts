import { useAuthStore, selectIsAuthenticated } from '../stores/authStore';

export const useAuth = () => {
  const { user, login, logout, updateUser } = useAuthStore();
  const isAuthenticated = useAuthStore(selectIsAuthenticated);

  const isAdmin = user?.role === 'admin';

  return {
    user,
    isAuthenticated,
    isAdmin,
    login,
    logout,
    updateUser,
  };
};
