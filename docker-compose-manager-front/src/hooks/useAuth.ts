import { useAuthStore } from '../stores/authStore';

export const useAuth = () => {
  const { user, isAuthenticated, login, logout, updateUser } = useAuthStore();

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
