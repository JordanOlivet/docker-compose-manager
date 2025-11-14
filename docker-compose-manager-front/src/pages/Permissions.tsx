import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { AxiosError } from 'axios';
import permissionsApi from '../api/permissions';
import { useToast } from '../hooks/useToast';
import { LoadingSpinner } from '../components/common/LoadingSpinner';
import { ErrorDisplay } from '../components/common/ErrorDisplay';
import {
  PermissionResourceType,
  PermissionFlags,
  getPermissionLabels,
  getResourceTypeLabel,
  type ResourcePermission,
} from '../types';
import { type ApiErrorResponse } from '../utils/errorFormatter';
import { t } from '../i18n';

function Permissions() {
  const [showEditModal, setShowEditModal] = useState(false);
  const [editingPermission, setEditingPermission] = useState<ResourcePermission | null>(null);
  const [editPermissions, setEditPermissions] = useState<number>(0);
  const [filterResourceType, setFilterResourceType] = useState<PermissionResourceType | 'all'>('all');

  const queryClient = useQueryClient();
  const toast = useToast();

  const { data: permissions, isLoading, error } = useQuery({
    queryKey: ['permissions', filterResourceType],
    queryFn: () =>
      permissionsApi.list(
        filterResourceType !== 'all' ? { resourceType: filterResourceType } : undefined
      ),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, permissions }: { id: number; permissions: number }) =>
      permissionsApi.update(id, { permissions }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['permissions'] });
      toast.success('Permission updated successfully');
      setShowEditModal(false);
      setEditingPermission(null);
    },
    onError: (error: AxiosError<ApiErrorResponse>) => {
      toast.error(error.response?.data?.message || 'Failed to update permission');
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => permissionsApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['permissions'] });
      toast.success('Permission deleted successfully');
    },
    onError: (error: AxiosError<ApiErrorResponse>) => {
      toast.error(error.response?.data?.message || 'Failed to delete permission');
    },
  });

  const handleEdit = (permission: ResourcePermission) => {
    setEditingPermission(permission);
    setEditPermissions(permission.permissions);
    setShowEditModal(true);
  };

  const handleUpdateSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingPermission) return;

    updateMutation.mutate({
      id: editingPermission.id,
      permissions: editPermissions
    });
  };

  const togglePermissionFlag = (flag: PermissionFlags) => {
    setEditPermissions(prev => {
      if ((prev & flag) === flag) {
        return prev & ~flag; // Remove flag
      } else {
        return prev | flag; // Add flag
      }
    });
  };

  const setPreset = (preset: 'readonly' | 'standard' | 'full') => {
    switch (preset) {
      case 'readonly':
        setEditPermissions(PermissionFlags.View | PermissionFlags.Logs);
        break;
      case 'standard':
        setEditPermissions(
          PermissionFlags.View |
          PermissionFlags.Start |
          PermissionFlags.Stop |
          PermissionFlags.Restart |
          PermissionFlags.Logs
        );
        break;
      case 'full':
        setEditPermissions(
          PermissionFlags.View |
          PermissionFlags.Start |
          PermissionFlags.Stop |
          PermissionFlags.Restart |
          PermissionFlags.Delete |
          PermissionFlags.Update |
          PermissionFlags.Logs |
          PermissionFlags.Execute
        );
        break;
    }
  };

  const permissionOptions = [
    { flag: PermissionFlags.View, label: 'View' },
    { flag: PermissionFlags.Start, label: 'Start' },
    { flag: PermissionFlags.Stop, label: 'Stop' },
    { flag: PermissionFlags.Restart, label: 'Restart' },
    { flag: PermissionFlags.Delete, label: 'Delete' },
    { flag: PermissionFlags.Update, label: 'Update' },
    { flag: PermissionFlags.Logs, label: 'Logs' },
    { flag: PermissionFlags.Execute, label: 'Execute' },
  ];

  if (isLoading) return <LoadingSpinner />;
  if (error) return <ErrorDisplay message="Failed to load permissions" />;

  return (
    <div className="space-y-8">
      {/* Page Header */}
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-4xl font-bold text-gray-900 dark:text-white mb-3">{t('users.permissions')}</h1>
          <p className="text-lg text-gray-600 dark:text-gray-400">
            {t('users.permissionsSubtitle')}
          </p>
          <p className="text-sm text-gray-500 dark:text-gray-500 mt-2">
            💡 {t('common.all')}
          </p>
        </div>
      </div>

      {/* Filter */}
      <div className="bg-white dark:bg-gray-800 rounded-xl shadow-lg border border-gray-100 dark:border-gray-700 p-4">
        <div className="flex items-center gap-4">
          <label className="text-sm font-medium text-gray-700 dark:text-gray-300">
            {t('common.filter')}:
          </label>
          <select
            value={filterResourceType}
            onChange={(e) => setFilterResourceType(e.target.value as PermissionResourceType | 'all')}
            className="px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 dark:bg-gray-700 dark:text-white"
          >
            <option value="all">{t('common.all')}</option>
            <option value={PermissionResourceType.Container}>{t('containers.title')}</option>
            <option value={PermissionResourceType.ComposeProject}>{t('compose.projects')}</option>
          </select>
        </div>
      </div>

      {/* Permissions Table */}
      <div className="bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl shadow-lg border border-gray-100 dark:border-gray-700 overflow-hidden">
        <table className="w-full">
          <thead className="bg-white/50 dark:bg-gray-800/50 border-b border-gray-200 dark:border-gray-700">
            <tr>
              <th className="px-6 py-4 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                {t('audit.resourceType')}
              </th>
              <th className="px-6 py-4 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                {t('common.filter')}
              </th>
              <th className="px-6 py-4 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                {t('users.username')}
              </th>
              <th className="px-6 py-4 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                {t('users.permissions')}
              </th>
              <th className="px-6 py-4 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                {t('users.actions')}
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100 dark:divide-gray-700">
            {permissions?.length === 0 && (
              <tr>
                <td colSpan={5} className="px-6 py-12 text-center">
                  <p className="text-gray-500 dark:text-gray-400 text-lg">
                    {t('audit.noLogs')}
                  </p>
                </td>
              </tr>
            )}
            {permissions?.map((permission: ResourcePermission) => (
              <tr key={permission.id} className="hover:bg-white dark:hover:bg-gray-800 transition-all">
                <td className="px-6 py-4">
                  <span className="font-medium text-gray-900 dark:text-white">
                    {permission.resourceName}
                  </span>
                </td>
                <td className="px-6 py-4">
                  <span className="px-3 py-1 bg-purple-100 dark:bg-purple-900/30 text-purple-800 dark:text-purple-300 rounded-full text-xs font-medium">
                    {getResourceTypeLabel(permission.resourceType)}
                  </span>
                </td>
                <td className="px-6 py-4">
                  <div className="flex flex-col gap-1">
                    <span className="text-sm font-medium text-gray-900 dark:text-white">
                      {permission.username || permission.userGroupName}
                    </span>
                    <span className="text-xs text-gray-500 dark:text-gray-400">
                      {permission.userId ? t('users.title') : t('users.groups')}
                    </span>
                  </div>
                </td>
                <td className="px-6 py-4">
                  <div className="flex flex-wrap gap-1">
                    {getPermissionLabels(permission.permissions).map((label) => (
                      <span
                        key={label}
                        className="px-2 py-1 bg-blue-100 dark:bg-blue-900/30 text-blue-800 dark:text-blue-300 rounded text-xs font-medium"
                      >
                        {label}
                      </span>
                    ))}
                  </div>
                </td>
                <td className="px-6 py-4">
                  <div className="flex gap-2">
                    <button
                      onClick={() => handleEdit(permission)}
                      className="px-3 py-1 bg-green-600 text-white rounded-lg hover:bg-green-700 transition-colors text-sm font-medium"
                    >
                      {t('common.edit')}
                    </button>
                    <button
                      onClick={() => {
                        if (
                          confirm(
                            `${t('common.delete')} ${t('users.permissions').toLowerCase()} "${permission.resourceName}"?`
                          )
                        ) {
                          deleteMutation.mutate(permission.id);
                        }
                      }}
                      className="px-3 py-1 bg-red-600 text-white rounded-lg hover:bg-red-700 transition-colors text-sm font-medium"
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

      {/* Edit Modal */}
      {showEditModal && editingPermission && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-2xl w-full max-w-2xl border border-gray-200 dark:border-gray-700">
            <div className="p-8">
              <h2 className="text-2xl font-bold text-gray-900 dark:text-white mb-6">
                {t('common.edit')} {t('users.permissions')}
              </h2>

              <form onSubmit={handleUpdateSubmit} className="space-y-6">
                <div className="bg-gray-50 dark:bg-gray-700 rounded-lg p-4 space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-sm text-gray-600 dark:text-gray-400">{t('audit.resourceType')}:</span>
                    <span className="font-medium text-gray-900 dark:text-white">
                      {editingPermission.resourceName}
                    </span>
                  </div>
                  <div className="flex items-center justify-between">
                    <span className="text-sm text-gray-600 dark:text-gray-400">{t('common.filter')}:</span>
                    <span className="font-medium text-gray-900 dark:text-white">
                      {getResourceTypeLabel(editingPermission.resourceType)}
                    </span>
                  </div>
                  <div className="flex items-center justify-between">
                    <span className="text-sm text-gray-600 dark:text-gray-400">{t('users.username')}:</span>
                    <span className="font-medium text-gray-900 dark:text-white">
                      {editingPermission.username || editingPermission.userGroupName}
                      <span className="ml-2 text-xs text-gray-500 dark:text-gray-400">
                        ({editingPermission.userId ? t('users.title') : t('users.groups')})
                      </span>
                    </span>
                  </div>
                </div>

                <div>
                  <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-3">
                    {t('users.permissions')}
                  </label>

                  {/* Presets */}
                  <div className="flex gap-2 mb-4">
                    <button
                      type="button"
                      onClick={() => setPreset('readonly')}
                      className="px-4 py-2 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors text-sm font-medium"
                    >
                      {t('settings.access')}
                    </button>
                    <button
                      type="button"
                      onClick={() => setPreset('standard')}
                      className="px-4 py-2 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors text-sm font-medium"
                    >
                      {t('common.all')}
                    </button>
                    <button
                      type="button"
                      onClick={() => setPreset('full')}
                      className="px-4 py-2 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors text-sm font-medium"
                    >
                      {t('settings.access')}
                    </button>
                  </div>

                  {/* Individual permissions */}
                  <div className="grid grid-cols-2 gap-3">
                    {permissionOptions.map(({ flag, label }) => (
                      <label
                        key={flag}
                        className="flex items-center p-3 bg-gray-50 dark:bg-gray-700 rounded-lg cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600 transition-colors"
                      >
                        <input
                          type="checkbox"
                          checked={(editPermissions & flag) === flag}
                          onChange={() => togglePermissionFlag(flag)}
                          className="mr-3 h-4 w-4"
                        />
                        <span className="text-sm font-medium text-gray-900 dark:text-white">
                          {label}
                        </span>
                      </label>
                    ))}
                  </div>
                </div>

                <div className="flex gap-3 pt-4">
                  <button
                    type="submit"
                    disabled={updateMutation.isPending}
                    className="flex-1 bg-gradient-to-r from-blue-600 to-blue-700 text-white py-3 rounded-xl hover:shadow-lg hover:scale-105 transition-all duration-200 font-semibold disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    {updateMutation.isPending ? `${t('common.save')}...` : `${t('common.edit')} ${t('users.permissions')}`}
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setShowEditModal(false);
                      setEditingPermission(null);
                    }}
                    className="flex-1 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 py-3 rounded-xl hover:bg-gray-200 dark:hover:bg-gray-600 transition-all duration-200 font-semibold"
                  >
                    {t('common.cancel')}
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default Permissions;
