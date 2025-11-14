import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { AlertCircle, Copy, X } from 'lucide-react';
import usersApi from '@/api/users';
import userGroupsApi from '@/api/userGroups';
import permissionsApi from '@/api/permissions';
import { useToast } from '@/hooks/useToast';
import { t } from '@/i18n';

interface CopyPermissionsDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  targetType: 'user' | 'group';
  targetId: number;
  onSuccess?: () => void;
}

export function CopyPermissionsDialog({
  open,
  onOpenChange,
  targetType,
  targetId,
  onSuccess,
}: CopyPermissionsDialogProps) {
  const [sourceType, setSourceType] = useState<'user' | 'group'>('user');
  const [sourceId, setSourceId] = useState<string>('');
  const queryClient = useQueryClient();
  const toast = useToast();

  // Fetch users and groups for selection
  const { data: users = [] } = useQuery({
    queryKey: ['users'],
    queryFn: usersApi.list,
    enabled: open && sourceType === 'user'
  });

  const { data: groups = [] } = useQuery({
    queryKey: ['userGroups'],
    queryFn: userGroupsApi.list,
    enabled: open && sourceType === 'group'
  });

  // Fetch permissions for preview
  const { data: sourcePermissions, isLoading: isLoadingPermissions } = useQuery({
    queryKey: ['permissions', sourceType, sourceId],
    queryFn: () => {
      if (!sourceId) return Promise.resolve([]);

      return permissionsApi.list({
        userId: sourceType === 'user' ? parseInt(sourceId) : undefined,
        userGroupId: sourceType === 'group' ? parseInt(sourceId) : undefined
      });
    },
    enabled: open && !!sourceId
  });

  const copyMutation = useMutation({
    mutationFn: async () => {
      if (!sourceId) {
        throw new Error('Please select a source');
      }

      return permissionsApi.copyPermissions({
        sourceUserId: sourceType === 'user' ? parseInt(sourceId) : undefined,
        sourceUserGroupId: sourceType === 'group' ? parseInt(sourceId) : undefined,
        targetUserId: targetType === 'user' ? targetId : undefined,
        targetUserGroupId: targetType === 'group' ? targetId : undefined
      });
    },
    onSuccess: () => {
      toast.success('Permissions copied successfully');
      queryClient.invalidateQueries({ queryKey: ['permissions'] });
      onOpenChange(false);
      onSuccess?.();
    },
    onError: (error: { response?: { data?: { message?: string } } }) => {
      toast.error(error.response?.data?.message || 'Failed to copy permissions');
    }
  });

  const handleCopy = () => {
    copyMutation.mutate();
  };

  const handleReset = () => {
    setSourceType('user');
    setSourceId('');
  };

  const handleClose = () => {
    onOpenChange(false);
    handleReset();
  };

  const sourceList = sourceType === 'user' ? users : groups;
  const filteredList = sourceList.filter(item =>
    item.id !== targetId || sourceType !== targetType
  );

  if (!open) return null;

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-2xl w-full max-w-2xl border border-gray-200 dark:border-gray-700 max-h-[90vh] overflow-hidden flex flex-col">
        {/* Header */}
        <div className="p-6 border-b border-gray-200 dark:border-gray-700 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="p-2 bg-purple-100 dark:bg-purple-900/30 rounded-lg">
              <Copy className="h-5 w-5 text-purple-600 dark:text-purple-400" />
            </div>
            <div>
              <h2 className="text-xl font-bold text-gray-900 dark:text-white">
                Copy Permissions
              </h2>
              <p className="text-sm text-gray-600 dark:text-gray-400 mt-0.5">
                Copy permissions from another {targetType === 'user' ? 'user' : 'group'} to this {targetType}
              </p>
            </div>
          </div>
          <button
            onClick={handleClose}
            className="p-2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg transition-all"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-6 space-y-6">
          {/* Warning */}
          <div className="flex gap-3 p-4 bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-800 rounded-lg">
            <AlertCircle className="h-5 w-5 text-amber-600 dark:text-amber-400 flex-shrink-0 mt-0.5" />
            <div>
              <p className="text-sm font-medium text-amber-900 dark:text-amber-200">
                Warning: This action will replace all current permissions
              </p>
              <p className="text-xs text-amber-700 dark:text-amber-300 mt-1">
                All existing permissions will be removed and replaced with the copied ones.
              </p>
            </div>
          </div>

          {/* Source Type Selection */}
          <div className="space-y-2">
            <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300">
              Copy from
            </label>
            <div className="flex gap-3">
              <label className="flex-1 flex items-center gap-3 p-4 bg-gray-50 dark:bg-gray-700 rounded-lg cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600 transition-all border-2 border-transparent has-[:checked]:border-blue-500 has-[:checked]:bg-blue-50 dark:has-[:checked]:bg-blue-900/20">
                <input
                  type="radio"
                  name="sourceType"
                  value="user"
                  checked={sourceType === 'user'}
                  onChange={(e) => {
                    setSourceType(e.target.value as 'user' | 'group');
                    setSourceId('');
                  }}
                  className="h-4 w-4"
                />
                <span className="text-sm font-medium text-gray-900 dark:text-white">
                  User
                </span>
              </label>
              <label className="flex-1 flex items-center gap-3 p-4 bg-gray-50 dark:bg-gray-700 rounded-lg cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600 transition-all border-2 border-transparent has-[:checked]:border-blue-500 has-[:checked]:bg-blue-50 dark:has-[:checked]:bg-blue-900/20">
                <input
                  type="radio"
                  name="sourceType"
                  value="group"
                  checked={sourceType === 'group'}
                  onChange={(e) => {
                    setSourceType(e.target.value as 'user' | 'group');
                    setSourceId('');
                  }}
                  className="h-4 w-4"
                />
                <span className="text-sm font-medium text-gray-900 dark:text-white">
                  Group
                </span>
              </label>
            </div>
          </div>

          {/* Source Selection */}
          <div className="space-y-2">
            <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300">
              {t('common.selectA')} {sourceType === 'user' ? t('common.user') : t('common.group')}
            </label>
            <select
              value={sourceId}
              onChange={(e) => setSourceId(e.target.value)}
              className="w-full border border-gray-300 dark:border-gray-600 rounded-xl px-4 py-3 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
            >
              <option value="">{t('common.selectA')} {sourceType}</option>
              {filteredList.length === 0 && (
                <option disabled>{t('common.noAvailable', { type: sourceType })}</option>
              )}
              {filteredList.map(item => (
                <option key={item.id} value={item.id.toString()}>
                  {sourceType === 'user' ? (item as { username: string }).username : (item as { name: string }).name}
                </option>
              ))}
            </select>
          </div>

          {/* Permissions Preview */}
          {sourceId && (
            <div className="space-y-2">
              <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300">
                {t('common.permissionsPreview')}
              </label>
              {isLoadingPermissions && (
                <div className="text-sm text-gray-500 dark:text-gray-400 text-center py-4">
                  {t('common.loadingPermissions')}
                </div>
              )}
              {!isLoadingPermissions && sourcePermissions && (
                <div className="border border-gray-200 dark:border-gray-700 rounded-xl bg-gray-50 dark:bg-gray-700/50 max-h-[250px] overflow-y-auto">
                  {sourcePermissions.length === 0 && (
                    <p className="text-sm text-gray-500 dark:text-gray-400 text-center py-8">
                      {t('common.noPermissionsFound')}
                    </p>
                  )}
                  <div className="divide-y divide-gray-200 dark:divide-gray-700">
                    {sourcePermissions.map(perm => (
                      <div key={perm.id} className="p-4 hover:bg-gray-100 dark:hover:bg-gray-600/50 transition-colors">
                        <div className="flex items-center justify-between gap-4">
                          <div className="flex items-center gap-2 min-w-0">
                            <span className="px-2 py-1 bg-purple-100 dark:bg-purple-900/30 text-purple-800 dark:text-purple-300 rounded text-xs font-medium flex-shrink-0">
                              {perm.resourceType === 1 ? t('common.container') : t('common.project')}
                            </span>
                            <span className="font-medium text-gray-900 dark:text-white truncate">
                              {perm.resourceName}
                            </span>
                          </div>
                          <div className="flex flex-wrap gap-1 justify-end">
                            {[
                              { flag: 1, label: 'View' },
                              { flag: 2, label: 'Start' },
                              { flag: 4, label: 'Stop' },
                              { flag: 8, label: 'Restart' },
                              { flag: 16, label: 'Delete' },
                              { flag: 32, label: 'Update' },
                              { flag: 64, label: 'Logs' },
                              { flag: 128, label: 'Execute' },
                            ]
                              .filter(({ flag }) => (perm.permissions & flag) === flag)
                              .map(({ label }) => (
                                <span
                                  key={label}
                                  className="px-2 py-0.5 bg-blue-100 dark:bg-blue-900/30 text-blue-800 dark:text-blue-300 rounded text-xs font-medium"
                                >
                                  {label}
                                </span>
                              ))}
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="p-6 border-t border-gray-200 dark:border-gray-700 flex gap-3">
          <button
            onClick={handleClose}
            disabled={copyMutation.isPending}
            className="flex-1 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 py-3 rounded-xl hover:bg-gray-200 dark:hover:bg-gray-600 transition-all duration-200 font-semibold disabled:opacity-50 disabled:cursor-not-allowed"
          >
            Cancel
          </button>
          <button
            onClick={handleCopy}
            disabled={!sourceId || copyMutation.isPending}
            className="flex-1 bg-gradient-to-r from-blue-600 to-blue-700 text-white py-3 rounded-xl hover:shadow-lg hover:scale-105 transition-all duration-200 font-semibold disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:scale-100"
          >
            {copyMutation.isPending ? 'Copying...' : 'Copy Permissions'}
          </button>
        </div>
      </div>
    </div>
  );
}
