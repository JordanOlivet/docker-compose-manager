import { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { AxiosError } from 'axios';
import usersApi, { type User } from '../api/users';
import permissionsApi from '../api/permissions';
import { useToast } from '../hooks/useToast';
import { LoadingSpinner } from '../components/common/LoadingSpinner';
import { ErrorDisplay } from '../components/common/ErrorDisplay';
import { PasswordInput } from '../components/common/PasswordInput';
import { PermissionSelector } from '../components/PermissionSelector';
import { CopyPermissionsDialog } from '../components/CopyPermissionsDialog';
import { formatApiError, type ApiErrorResponse } from '../utils/errorFormatter';
import type { ResourcePermissionInput } from '../types/permissions';
import { t } from '../i18n';

function UserManagement() {
  const [showModal, setShowModal] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [editingUserId, setEditingUserId] = useState<number | null>(null);
  const [activeTab, setActiveTab] = useState<'general' | 'permissions'>('general');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState<'admin' | 'user'>('user');
  const [permissions, setPermissions] = useState<ResourcePermissionInput[]>([]);
  const [showCopyDialog, setShowCopyDialog] = useState(false);

  const queryClient = useQueryClient();
  const toast = useToast();

  const { data: users, isLoading, error } = useQuery({
    queryKey: ['users'],
    queryFn: usersApi.list,
  });

  // Fetch user permissions when editing
  const { data: userPermissions } = useQuery({
    queryKey: ['permissions', 'user', editingUserId],
    queryFn: () => editingUserId ? permissionsApi.list({ userId: editingUserId }) : Promise.resolve([]),
    enabled: editMode && !!editingUserId
  });

  // Update permissions when userPermissions changes
  useEffect(() => {
    if (userPermissions) {
      setPermissions(userPermissions.map(p => ({
        resourceType: p.resourceType,
        resourceName: p.resourceName,
        permissions: p.permissions
      })));
    }
  }, [userPermissions]);

  const createMutation = useMutation({
    mutationFn: (data: { username: string; password: string; role: string; permissions?: ResourcePermissionInput[] }) =>
      usersApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
      queryClient.invalidateQueries({ queryKey: ['permissions'] });
      toast.success('User created successfully');
      closeModal();
    },
    onError: (error: AxiosError<ApiErrorResponse>) => {
      toast.error(formatApiError(error, 'Failed to create user'));
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: number; data: { username?: string; role?: string; newPassword?: string; permissions?: ResourcePermissionInput[] } }) =>
      usersApi.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
      queryClient.invalidateQueries({ queryKey: ['permissions'] });
      toast.success('User updated successfully');
      closeModal();
    },
    onError: (error: AxiosError<ApiErrorResponse>) => {
      toast.error(formatApiError(error, 'Failed to update user'));
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => usersApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
      queryClient.invalidateQueries({ queryKey: ['permissions'] });
      toast.success('User deleted successfully');
    },
    onError: (error: AxiosError<ApiErrorResponse>) => {
      toast.error(error.response?.data?.message || 'Failed to delete user');
    },
  });

  const toggleEnabledMutation = useMutation({
    mutationFn: ({ id, enabled }: { id: number; enabled: boolean }) =>
      enabled ? usersApi.disable(id) : usersApi.enable(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
      toast.success('User status updated');
    },
  });

  const openCreateModal = () => {
    setEditMode(false);
    setEditingUserId(null);
    setUsername('');
    setPassword('');
    setRole('user');
    setPermissions([]);
    setActiveTab('general');
    setShowModal(true);
  };

  const openEditModal = (user: User) => {
    setEditMode(true);
    setEditingUserId(user.id);
    setUsername(user.username);
    setPassword('');
    setRole(user.role as 'admin' | 'user');
    setActiveTab('general');
    setShowModal(true);
  };

  const closeModal = () => {
    setShowModal(false);
    setEditMode(false);
    setEditingUserId(null);
    setUsername('');
    setPassword('');
    setRole('user');
    setPermissions([]);
    setActiveTab('general');
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const userData = {
      username,
      role,
      permissions: permissions.length > 0 ? permissions : undefined
    };

    if (editMode && editingUserId) {
      const updateData: { username: string; role: string; permissions?: typeof permissions; newPassword?: string } = { ...userData };
      if (password) {
        updateData.newPassword = password;
      }
      updateMutation.mutate({ id: editingUserId, data: updateData });
    } else {
      createMutation.mutate({ ...userData, password });
    }
  };

  const handleCopySuccess = () => {
    // Refresh permissions after copy
    if (editingUserId) {
      queryClient.invalidateQueries({ queryKey: ['permissions', 'user', editingUserId] });
    }
  };

  if (isLoading) return <LoadingSpinner />;
  if (error) return <ErrorDisplay message={t('users.failedToCreate')} />;

  return (
    <div className="space-y-8">
      {/* Page Header */}
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-4xl font-bold text-gray-900 dark:text-white mb-3">{t('users.title')}</h1>
          <p className="text-lg text-gray-600 dark:text-gray-400">
            {t('users.subtitle')}
          </p>
        </div>
        <button
          onClick={openCreateModal}
          className="flex items-center gap-2 bg-linear-to-r from-blue-600 to-blue-700 text-white px-6 py-3 rounded-xl hover:shadow-lg hover:scale-105 transition-all duration-200 font-medium"
        >
          <span>+</span> {t('users.createUser')}
        </button>
      </div>

      <div className="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl shadow-lg border border-gray-100 dark:border-gray-700 overflow-hidden">
        <table className="w-full">
          <thead className="bg-white/50 dark:bg-gray-800/50 border-b border-gray-200 dark:border-gray-700">
            <tr>
              <th className="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">{t('users.username')}</th>
              <th className="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">{t('users.role')}</th>
              <th className="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">{t('users.status')}</th>
              <th className="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">{t('users.actions')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100 dark:divide-gray-700">
            {users?.map((user: User) => (
              <tr key={user.id} className="hover:bg-white dark:hover:bg-gray-800 transition-all">
                <td className="px-8 py-5">
                  <span className="text-sm font-medium text-gray-900 dark:text-white">{user.username}</span>
                </td>
                <td className="px-8 py-5">
                  <span className={`px-3 py-1.5 rounded-full text-xs font-medium ${user.role === 'admin' ? 'bg-purple-100 dark:bg-purple-900/30 text-purple-800 dark:text-purple-300' : 'bg-blue-100 dark:bg-blue-900/30 text-blue-800 dark:text-blue-300'}`}>
                    {user.role}
                  </span>
                </td>
                <td className="px-8 py-5">
                  <span className={`px-3 py-1.5 rounded-full text-xs font-medium ${user.isEnabled ? 'bg-green-100 dark:bg-green-900/30 text-green-800 dark:text-green-300' : 'bg-red-100 dark:bg-red-900/30 text-red-800 dark:text-red-300'}`}>
                    {user.isEnabled ? t('users.enabled') : t('users.disabled')}
                  </span>
                </td>
                <td className="px-8 py-5">
                  <div className="flex items-center gap-3">
                    <button
                      onClick={() => openEditModal(user)}
                      className="px-3 py-1.5 text-sm font-medium text-green-600 dark:text-green-400 hover:bg-green-50 dark:hover:bg-green-900/20 rounded-lg transition-all"
                    >
                      {t('common.edit')}
                    </button>
                    <button
                      onClick={() => toggleEnabledMutation.mutate({ id: user.id, enabled: user.isEnabled })}
                      className="px-3 py-1.5 text-sm font-medium text-blue-600 dark:text-blue-400 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded-lg transition-all"
                    >
                      {user.isEnabled ? t('users.disabled') : t('users.enabled')}
                    </button>
                    <button
                      onClick={() => { if (confirm(`${t('users.deleteUser')}: ${user.username}?`)) deleteMutation.mutate(user.id); }}
                      className="px-3 py-1.5 text-sm font-medium text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20 rounded-lg transition-all"
                    >
                      {t('common.delete')}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showModal && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-2xl w-full max-w-4xl border border-gray-200 dark:border-gray-700 max-h-[90vh] overflow-hidden flex flex-col">
            <div className="p-8 border-b border-gray-200 dark:border-gray-700">
              <h2 className="text-2xl font-bold text-gray-900 dark:text-white">
                {editMode ? t('users.editUser') : t('users.createUser')}
              </h2>
            </div>

            {/* Tabs */}
            <div className="border-b border-gray-200 dark:border-gray-700 px-8">
              <div className="flex gap-4">
                <button
                  onClick={() => setActiveTab('general')}
                  className={`py-4 px-2 font-medium text-sm transition-all relative ${
                    activeTab === 'general'
                      ? 'text-blue-600 dark:text-blue-400'
                      : 'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300'
                  }`}
                >
                  {t('users.general')}
                  {activeTab === 'general' && (
                    <div className="absolute bottom-0 left-0 right-0 h-0.5 bg-blue-600 dark:bg-blue-400"></div>
                  )}
                </button>
                <button
                  onClick={() => setActiveTab('permissions')}
                  className={`py-4 px-2 font-medium text-sm transition-all relative ${
                    activeTab === 'permissions'
                      ? 'text-blue-600 dark:text-blue-400'
                      : 'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300'
                  }`}
                >
                  {t('users.permissions')}
                  {permissions.length > 0 && (
                    <span className="ml-2 px-2 py-0.5 bg-blue-100 dark:bg-blue-900/30 text-blue-600 dark:text-blue-400 rounded-full text-xs">
                      {permissions.length}
                    </span>
                  )}
                  {activeTab === 'permissions' && (
                    <div className="absolute bottom-0 left-0 right-0 h-0.5 bg-blue-600 dark:bg-blue-400"></div>
                  )}
                </button>
              </div>
            </div>

            <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto">
              {/* General Information Tab */}
              {activeTab === 'general' && (
                <div className="p-8 space-y-6">
                  <div>
                    <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">{t('users.username')}</label>
                    <input
                      type="text"
                      value={username}
                      onChange={(e) => setUsername(e.target.value)}
                      className="w-full border border-gray-300 dark:border-gray-600 rounded-xl px-4 py-3 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
                      placeholder={t('users.usernamePlaceholder')}
                      required
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">
                      {t('users.password')} {editMode && <span className="text-gray-500 text-xs">(leave blank to keep current)</span>}
                    </label>
                    <PasswordInput
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      placeholder={editMode ? "Enter new password (optional)" : "Enter password"}
                      required={!editMode}
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">{t('users.role')}</label>
                    <select
                      value={role}
                      onChange={(e) => setRole(e.target.value as 'admin' | 'user')}
                      className="w-full border border-gray-300 dark:border-gray-600 rounded-xl px-4 py-3 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
                    >
                      <option value="user">{t('users.user')}</option>
                      <option value="admin">{t('users.admin')}</option>
                    </select>
                  </div>
                </div>
              )}

              {/* Permissions Tab */}
              {activeTab === 'permissions' && (
                <div className="p-8">
                  <PermissionSelector
                    permissions={permissions}
                    onChange={setPermissions}
                    onCopyClick={() => setShowCopyDialog(true)}
                    showCopyButton={editMode}
                  />
                </div>
              )}

              {/* Footer */}
              <div className="p-8 border-t border-gray-200 dark:border-gray-700 flex gap-3">
                <button
                  type="submit"
                  disabled={createMutation.isPending || updateMutation.isPending}
                  className="flex-1 bg-linear-to-r from-blue-600 to-blue-700 text-white py-3 rounded-xl hover:shadow-lg hover:scale-105 transition-all duration-200 font-semibold disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {createMutation.isPending || updateMutation.isPending
                    ? `${t('common.save')}...`
                    : editMode
                    ? t('users.editUser')
                    : t('users.createUser')}
                </button>
                <button
                  type="button"
                  onClick={closeModal}
                  className="flex-1 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 py-3 rounded-xl hover:bg-gray-200 dark:hover:bg-gray-600 transition-all duration-200 font-semibold"
                >
                  {t('common.cancel')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Copy Permissions Dialog */}
      {editingUserId && (
        <CopyPermissionsDialog
          open={showCopyDialog}
          onOpenChange={setShowCopyDialog}
          targetType="user"
          targetId={editingUserId}
          onSuccess={handleCopySuccess}
        />
      )}
    </div>
  );
}

export default UserManagement;
