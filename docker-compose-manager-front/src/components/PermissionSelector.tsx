import { useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import type {
  ResourcePermissionInput,
} from '@/types/permissions';
import {
  PermissionFlags,
  PermissionResourceType,
  getPermissionLabels,
  getResourceTypeLabel
} from '@/types/permissions';
import { useQuery } from '@tanstack/react-query';
import { containersApi } from '@/api/containers';
import { composeApi } from '@/api/compose';
import { useTranslation } from 'react-i18next';

interface PermissionSelectorProps {
  permissions: ResourcePermissionInput[];
  onChange: (permissions: ResourcePermissionInput[]) => void;
  onCopyClick?: () => void;
  showCopyButton?: boolean;
}

export function PermissionSelector({
  permissions,
  onChange,
  onCopyClick,
  showCopyButton = true
}: PermissionSelectorProps) {
  const { t } = useTranslation();
  const [isAdding, setIsAdding] = useState(false);
  const [newPermission, setNewPermission] = useState<ResourcePermissionInput>({
    resourceType: PermissionResourceType.Container,
    resourceName: '',
    permissions: PermissionFlags.View
  });

  // Fetch containers and projects for the resource selector
  const { data: containers = [] } = useQuery({
    queryKey: ['containers'],
    queryFn: () => containersApi.list()
  });

  const { data: projects = [] } = useQuery({
    queryKey: ['compose', 'projects'],
    queryFn: () => composeApi.listProjects()
  });

  const availableResources = newPermission.resourceType === PermissionResourceType.Container
    ? (containers as Array<{ name: string }>).map((c) => c.name)
    : (projects as Array<{ name: string }>).map((p) => p.name);

  const handleAddPermission = () => {
    if (!newPermission.resourceName) {
      return;
    }

    // Check for duplicate
    const exists = permissions.some(
      p => p.resourceType === newPermission.resourceType &&
           p.resourceName === newPermission.resourceName
    );

    if (exists) {
      alert('Permission for this resource already exists');
      return;
    }

    onChange([...permissions, { ...newPermission }]);

    // Reset form
    setNewPermission({
      resourceType: PermissionResourceType.Container,
      resourceName: '',
      permissions: PermissionFlags.View
    });
    setIsAdding(false);
  };

  const handleRemovePermission = (index: number) => {
    onChange(permissions.filter((_, i) => i !== index));
  };

  const handleUpdatePermission = (index: number, flags: PermissionFlags) => {
    const updated = [...permissions];
    updated[index] = { ...updated[index], permissions: flags };
    onChange(updated);
  };

  const toggleFlag = (currentFlags: PermissionFlags, flag: PermissionFlags): PermissionFlags => {
    return (currentFlags & flag) ? (currentFlags & ~flag) : (currentFlags | flag);
  };

  const setPreset = (flags: PermissionFlags, preset: 'readonly' | 'standard' | 'full'): PermissionFlags => {
    switch (preset) {
      case 'readonly':
        return PermissionFlags.View | PermissionFlags.Logs;
      case 'standard':
        return PermissionFlags.View | PermissionFlags.Start | PermissionFlags.Stop |
               PermissionFlags.Restart | PermissionFlags.Logs;
      case 'full':
        return PermissionFlags.View | PermissionFlags.Start | PermissionFlags.Stop |
               PermissionFlags.Restart | PermissionFlags.Delete | PermissionFlags.Update |
               PermissionFlags.Logs | PermissionFlags.Execute;
      default:
        return flags;
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex justify-between items-center">
        <label className="text-base font-semibold text-gray-900 dark:text-white">{t('permissions.resourcePermissions')}</label>
        <div className="flex gap-2">
          {showCopyButton && onCopyClick && (
            <button
              type="button"
              onClick={onCopyClick}
              className="px-4 py-2 bg-purple-600 text-white rounded-lg hover:bg-purple-700 hover:scale-105 transition-all duration-200 text-sm font-medium"
            >
              {t('permissions.copyFrom')}
            </button>
          )}
          {!isAdding && (
            <button
              type="button"
              onClick={() => setIsAdding(true)}
              className="flex items-center gap-2 bg-linear-to-r from-blue-600 to-blue-700 text-white px-4 py-2 rounded-lg hover:shadow-lg hover:scale-105 transition-all duration-200 text-sm font-medium"
            >
              <Plus className="h-4 w-4" />
              {t('permissions.addPermission')}
            </button>
          )}
        </div>
      </div>

      {/* Existing permissions list */}
      <div className="space-y-3">
        {permissions.length === 0 && !isAdding && (
          <p className="text-sm text-gray-500 dark:text-gray-400 text-center py-8 bg-gray-50 dark:bg-gray-700/50 rounded-lg">
            {t('permissions.noPermissionsAssigned')}
          </p>
        )}

        {permissions.map((perm, index) => (
          <div key={index} className="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-xl shadow border border-gray-200 dark:border-gray-700 p-5">
            <div className="flex justify-between items-start gap-4">
              <div className="flex-1 space-y-3">
                <div className="flex items-center gap-2">
                  <span className="px-3 py-1 bg-purple-100 dark:bg-purple-900/30 text-purple-800 dark:text-purple-300 rounded-full text-xs font-medium">
                    {getResourceTypeLabel(perm.resourceType)}
                  </span>
                  <span className="font-medium text-gray-900 dark:text-white">{perm.resourceName}</span>
                </div>

                <div className="space-y-3">
                  <div className="flex gap-2">
                    <button
                      type="button"
                      onClick={() => handleUpdatePermission(index, setPreset(perm.permissions, 'readonly'))}
                      className="px-3 py-1.5 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors text-sm font-medium"
                    >
                      Read Only
                    </button>
                    <button
                      type="button"
                      onClick={() => handleUpdatePermission(index, setPreset(perm.permissions, 'standard'))}
                      className="px-3 py-1.5 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors text-sm font-medium"
                    >
                      Standard
                    </button>
                    <button
                      type="button"
                      onClick={() => handleUpdatePermission(index, setPreset(perm.permissions, 'full'))}
                      className="px-3 py-1.5 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors text-sm font-medium"
                    >
                      Full Access
                    </button>
                  </div>

                  <div className="grid grid-cols-4 gap-2">
                    {[
                      { flag: PermissionFlags.View, label: 'View' },
                      { flag: PermissionFlags.Start, label: 'Start' },
                      { flag: PermissionFlags.Stop, label: 'Stop' },
                      { flag: PermissionFlags.Restart, label: 'Restart' },
                      { flag: PermissionFlags.Delete, label: 'Delete' },
                      { flag: PermissionFlags.Update, label: 'Update' },
                      { flag: PermissionFlags.Logs, label: 'Logs' },
                      { flag: PermissionFlags.Execute, label: 'Execute' },
                    ].map(({ flag, label }) => (
                      <label
                        key={flag}
                        className="flex items-center gap-2 p-2 bg-gray-50 dark:bg-gray-700 rounded-lg cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600 transition-colors"
                      >
                        <input
                          type="checkbox"
                          checked={(perm.permissions & flag) === flag}
                          onChange={() => handleUpdatePermission(index, toggleFlag(perm.permissions, flag))}
                          className="h-4 w-4"
                        />
                        <span className="text-sm font-medium text-gray-900 dark:text-white">
                          {label}
                        </span>
                      </label>
                    ))}
                  </div>

                  <div className="flex flex-wrap gap-1">
                    {getPermissionLabels(perm.permissions).map(label => (
                      <span
                        key={label}
                        className="px-2 py-1 bg-blue-100 dark:bg-blue-900/30 text-blue-800 dark:text-blue-300 rounded text-xs font-medium"
                      >
                        {label}
                      </span>
                    ))}
                  </div>
                </div>
              </div>

              <button
                type="button"
                onClick={() => handleRemovePermission(index)}
                className="p-2 text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20 rounded-lg transition-all"
              >
                <Trash2 className="h-5 w-5" />
              </button>
            </div>
          </div>
        ))}
      </div>

      {/* Add new permission form */}
      {isAdding && (
        <div className="border-2 border-dashed border-gray-300 dark:border-gray-600 bg-gray-50 dark:bg-gray-800/50 rounded-xl p-5 space-y-4">
          <div className="space-y-2">
            <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300">
              Resource Type
            </label>
            <select
              value={newPermission.resourceType.toString()}
              onChange={(e) =>
                setNewPermission({
                  ...newPermission,
                  resourceType: parseInt(e.target.value) as PermissionResourceType,
                  resourceName: ''
                })
              }
              className="w-full border border-gray-300 dark:border-gray-600 rounded-xl px-4 py-3 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
            >
              <option value={PermissionResourceType.Container.toString()}>
                Container
              </option>
              <option value={PermissionResourceType.ComposeProject.toString()}>
                Compose Project
              </option>
            </select>
          </div>

          <div className="space-y-2">
            <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300">
              {t('permissions.resourceName')}
            </label>
            <select
              value={newPermission.resourceName}
              onChange={(e) => setNewPermission({ ...newPermission, resourceName: e.target.value })}
              className="w-full border border-gray-300 dark:border-gray-600 rounded-xl px-4 py-3 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
            >
              <option value="">{t('permissions.selectResource')}</option>
              {availableResources.length === 0 && (
                <option disabled>{t('permissions.noResourcesAvailable')}</option>
              )}
              {availableResources.map(name => (
                <option key={name} value={name}>
                  {name}
                </option>
              ))}
            </select>
          </div>

          <div className="space-y-2">
            <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300">
              {t('permissions.permissions')}
            </label>
            <div className="flex gap-2 mb-3">
              <button
                type="button"
                onClick={() => setNewPermission({ ...newPermission, permissions: setPreset(newPermission.permissions, 'readonly') })}
                className="px-3 py-1.5 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors text-sm font-medium"
              >
                {t('permissions.readOnly')}
              </button>
              <button
                type="button"
                onClick={() => setNewPermission({ ...newPermission, permissions: setPreset(newPermission.permissions, 'standard') })}
                className="px-3 py-1.5 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors text-sm font-medium"
              >
                {t('permissions.standard')}
              </button>
              <button
                type="button"
                onClick={() => setNewPermission({ ...newPermission, permissions: setPreset(newPermission.permissions, 'full') })}
                className="px-3 py-1.5 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors text-sm font-medium"
              >
                Full Access
              </button>
            </div>

            <div className="grid grid-cols-4 gap-2">
              {[
                { flag: PermissionFlags.View, label: 'View' },
                { flag: PermissionFlags.Start, label: 'Start' },
                { flag: PermissionFlags.Stop, label: 'Stop' },
                { flag: PermissionFlags.Restart, label: 'Restart' },
                { flag: PermissionFlags.Delete, label: 'Delete' },
                { flag: PermissionFlags.Update, label: 'Update' },
                { flag: PermissionFlags.Logs, label: 'Logs' },
                { flag: PermissionFlags.Execute, label: 'Execute' },
              ].map(({ flag, label }) => (
                <label
                  key={flag}
                  className="flex items-center gap-2 p-2 bg-white dark:bg-gray-700 rounded-lg cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600 transition-colors"
                >
                  <input
                    type="checkbox"
                    checked={(newPermission.permissions & flag) === flag}
                    onChange={() =>
                      setNewPermission({
                        ...newPermission,
                        permissions: toggleFlag(newPermission.permissions, flag)
                      })
                    }
                    className="h-4 w-4"
                  />
                  <span className="text-sm font-medium text-gray-900 dark:text-white">
                    {label}
                  </span>
                </label>
              ))}
            </div>
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <button
              type="button"
              onClick={() => {
                setIsAdding(false);
                setNewPermission({
                  resourceType: PermissionResourceType.Container,
                  resourceName: '',
                  permissions: PermissionFlags.View
                });
              }}
              className="px-5 py-2.5 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-all font-medium"
            >
              {t('common.cancel')}
            </button>
            <button
              type="button"
              onClick={handleAddPermission}
              disabled={!newPermission.resourceName}
              className="px-5 py-2.5 bg-linear-to-r from-blue-600 to-blue-700 text-white rounded-lg hover:shadow-lg hover:scale-105 transition-all duration-200 font-medium disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:scale-100"
            >
              {t('common.add')}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
