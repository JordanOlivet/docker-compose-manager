import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { AxiosError } from 'axios';
import permissionsApi from '../api/permissions';
import userGroupsApi from '../api/userGroups';
import usersApi from '../api/users';
import { containersApi } from '../api/containers';
import { composeApi } from '../api/compose';
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

export default function Permissions() {
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [resourceType, setResourceType] = useState<PermissionResourceType>(PermissionResourceType.Container);
  const [resourceName, setResourceName] = useState('');
  const [assigneeType, setAssigneeType] = useState<'user' | 'group'>('user');
  const [selectedUserId, setSelectedUserId] = useState<number | undefined>();
  const [selectedGroupId, setSelectedGroupId] = useState<number | undefined>();
  const [selectedPermissions, setSelectedPermissions] = useState<number[]>([]);
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

  const { data: users } = useQuery({
    queryKey: ['users'],
    queryFn: usersApi.list,
  });

  const { data: groups } = useQuery({
    queryKey: ['userGroups'],
    queryFn: userGroupsApi.list,
  });

  const { data: containers } = useQuery({
    queryKey: ['containers'],
    queryFn: () => containersApi.list(),
    enabled: resourceType === PermissionResourceType.Container,
  });

  const { data: projects } = useQuery({
    queryKey: ['composeProjects'],
    queryFn: () => composeApi.listProjects(),
    enabled: resourceType === PermissionResourceType.ComposeProject,
  });

  const createMutation = useMutation({
    mutationFn: permissionsApi.create,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['permissions'] });
      toast.success('Permission created successfully');
      setShowCreateModal(false);
      resetForm();
    },
    onError: (error: AxiosError<ApiErrorResponse>) => {
      toast.error(error.response?.data?.message || 'Failed to create permission');
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

  const resetForm = () => {
    setResourceType(PermissionResourceType.Container);
    setResourceName('');
    setAssigneeType('user');
    setSelectedUserId(undefined);
    setSelectedGroupId(undefined);
    setSelectedPermissions([]);
  };

  const handleCreate = (e: React.FormEvent) => {
    e.preventDefault();

    const permissionsValue = selectedPermissions.reduce((acc, perm) => acc | perm, 0);

    createMutation.mutate({
      resourceType,
      resourceName,
      userId: assigneeType === 'user' ? selectedUserId : undefined,
      userGroupId: assigneeType === 'group' ? selectedGroupId : undefined,
      permissions: permissionsValue,
    });
  };

  const togglePermission = (permission: PermissionFlags) => {
    if (selectedPermissions.includes(permission)) {
      setSelectedPermissions(selectedPermissions.filter((p) => p !== permission));
    } else {
      setSelectedPermissions([...selectedPermissions, permission]);
    }
  };

  const permissionOptions = [
    { flag: PermissionFlags.View, label: 'View', description: 'View resource details' },
    { flag: PermissionFlags.Start, label: 'Start', description: 'Start containers/services' },
    { flag: PermissionFlags.Stop, label: 'Stop', description: 'Stop containers/services' },
    { flag: PermissionFlags.Restart, label: 'Restart', description: 'Restart containers/services' },
    { flag: PermissionFlags.Delete, label: 'Delete', description: 'Remove/delete resources' },
    { flag: PermissionFlags.Update, label: 'Update', description: 'Update/recreate resources' },
    { flag: PermissionFlags.Logs, label: 'Logs', description: 'View logs' },
    { flag: PermissionFlags.Execute, label: 'Execute', description: 'Execute commands in containers' },
  ];

  const availableResources =
    resourceType === PermissionResourceType.Container
      ? containers?.map((c) => c.name)
      : projects?.map((p) => p.name);

  if (isLoading) return <LoadingSpinner />;
  if (error) return <ErrorDisplay message="Failed to load permissions" />;

  return (
    <div className="space-y-8">
      {/* Page Header */}
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-4xl font-bold text-gray-900 dark:text-white mb-3">Permissions</h1>
          <p className="text-lg text-gray-600 dark:text-gray-400">
            Manage resource permissions for users and groups
          </p>
        </div>
        <button
          onClick={() => setShowCreateModal(true)}
          className="flex items-center gap-2 bg-gradient-to-r from-blue-600 to-blue-700 text-white px-6 py-3 rounded-xl hover:shadow-lg hover:scale-105 transition-all duration-200 font-medium"
        >
          <span>+</span> Create Permission
        </button>
      </div>

      {/* Filter */}
      <div className="bg-white dark:bg-gray-800 rounded-xl shadow-lg border border-gray-100 dark:border-gray-700 p-4">
        <div className="flex items-center gap-4">
          <label className="text-sm font-medium text-gray-700 dark:text-gray-300">
            Filter by type:
          </label>
          <select
            value={filterResourceType}
            onChange={(e) => setFilterResourceType(e.target.value as PermissionResourceType | 'all')}
            className="px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 dark:bg-gray-700 dark:text-white"
          >
            <option value="all">All Resources</option>
            <option value={PermissionResourceType.Container}>Containers</option>
            <option value={PermissionResourceType.ComposeProject}>Compose Projects</option>
          </select>
        </div>
      </div>

      {/* Permissions Table */}
      <div className="bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl shadow-lg border border-gray-100 dark:border-gray-700 overflow-hidden">
        <table className="w-full">
          <thead className="bg-white/50 dark:bg-gray-800/50 border-b border-gray-200 dark:border-gray-700">
            <tr>
              <th className="px-6 py-4 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                Resource
              </th>
              <th className="px-6 py-4 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                Type
              </th>
              <th className="px-6 py-4 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                Assignee
              </th>
              <th className="px-6 py-4 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                Permissions
              </th>
              <th className="px-6 py-4 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                Actions
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100 dark:divide-gray-700">
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
                      {permission.userId ? 'User' : 'Group'}
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
                  <button
                    onClick={() => {
                      if (
                        confirm(
                          `Are you sure you want to delete this permission for "${permission.resourceName}"?`
                        )
                      ) {
                        deleteMutation.mutate(permission.id);
                      }
                    }}
                    className="px-3 py-1 bg-red-600 text-white rounded-lg hover:bg-red-700 transition-colors text-sm font-medium"
                  >
                    Delete
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        {permissions?.length === 0 && (
          <div className="text-center py-12">
            <p className="text-gray-500 dark:text-gray-400 text-lg">
              No permissions found. Create one to get started!
            </p>
          </div>
        )}
      </div>

      {/* Create Permission Modal */}
      {showCreateModal && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
          <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-2xl p-8 max-w-2xl w-full max-h-[90vh] overflow-y-auto">
            <h2 className="text-2xl font-bold text-gray-900 dark:text-white mb-6">
              Create Permission
            </h2>
            <form onSubmit={handleCreate} className="space-y-6">
              {/* Resource Type */}
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                  Resource Type *
                </label>
                <select
                  value={resourceType}
                  onChange={(e) => {
                    setResourceType(Number(e.target.value) as PermissionResourceType);
                    setResourceName('');
                  }}
                  className="w-full px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 dark:bg-gray-700 dark:text-white"
                >
                  <option value={PermissionResourceType.Container}>Container</option>
                  <option value={PermissionResourceType.ComposeProject}>Compose Project</option>
                </select>
              </div>

              {/* Resource Name */}
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                  Resource Name *
                </label>
                <select
                  value={resourceName}
                  onChange={(e) => setResourceName(e.target.value)}
                  className="w-full px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 dark:bg-gray-700 dark:text-white"
                  required
                >
                  <option value="">Select a resource</option>
                  {availableResources?.map((name) => (
                    <option key={name} value={name}>
                      {name}
                    </option>
                  ))}
                </select>
              </div>

              {/* Assignee Type */}
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                  Assign to *
                </label>
                <div className="flex gap-4">
                  <label className="flex items-center">
                    <input
                      type="radio"
                      value="user"
                      checked={assigneeType === 'user'}
                      onChange={(e) => setAssigneeType(e.target.value as 'user' | 'group')}
                      className="mr-2"
                    />
                    User
                  </label>
                  <label className="flex items-center">
                    <input
                      type="radio"
                      value="group"
                      checked={assigneeType === 'group'}
                      onChange={(e) => setAssigneeType(e.target.value as 'user' | 'group')}
                      className="mr-2"
                    />
                    Group
                  </label>
                </div>
              </div>

              {/* User or Group Selection */}
              {assigneeType === 'user' ? (
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                    User *
                  </label>
                  <select
                    value={selectedUserId || ''}
                    onChange={(e) => setSelectedUserId(Number(e.target.value))}
                    className="w-full px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 dark:bg-gray-700 dark:text-white"
                    required
                  >
                    <option value="">Select a user</option>
                    {users?.map((user) => (
                      <option key={user.id} value={user.id}>
                        {user.username} ({user.role})
                      </option>
                    ))}
                  </select>
                </div>
              ) : (
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                    Group *
                  </label>
                  <select
                    value={selectedGroupId || ''}
                    onChange={(e) => setSelectedGroupId(Number(e.target.value))}
                    className="w-full px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 dark:bg-gray-700 dark:text-white"
                    required
                  >
                    <option value="">Select a group</option>
                    {groups?.map((group) => (
                      <option key={group.id} value={group.id}>
                        {group.name} ({group.memberCount} members)
                      </option>
                    ))}
                  </select>
                </div>
              )}

              {/* Permissions Checkboxes */}
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">
                  Permissions * (select at least one)
                </label>
                <div className="grid grid-cols-2 gap-3">
                  {permissionOptions.map((option) => (
                    <label
                      key={option.flag}
                      className="flex items-start p-3 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 cursor-pointer"
                    >
                      <input
                        type="checkbox"
                        checked={selectedPermissions.includes(option.flag)}
                        onChange={() => togglePermission(option.flag)}
                        className="mt-1 mr-3"
                      />
                      <div>
                        <div className="font-medium text-gray-900 dark:text-white">
                          {option.label}
                        </div>
                        <div className="text-xs text-gray-500 dark:text-gray-400">
                          {option.description}
                        </div>
                      </div>
                    </label>
                  ))}
                </div>
              </div>

              {/* Quick Actions */}
              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={() => setSelectedPermissions([PermissionFlags.View, PermissionFlags.Logs])}
                  className="px-3 py-1 text-sm bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded hover:bg-gray-300 dark:hover:bg-gray-600"
                >
                  Read Only
                </button>
                <button
                  type="button"
                  onClick={() => setSelectedPermissions([
                    PermissionFlags.View,
                    PermissionFlags.Start,
                    PermissionFlags.Stop,
                    PermissionFlags.Restart,
                    PermissionFlags.Logs,
                  ])}
                  className="px-3 py-1 text-sm bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded hover:bg-gray-300 dark:hover:bg-gray-600"
                >
                  Standard
                </button>
                <button
                  type="button"
                  onClick={() => setSelectedPermissions([
                    PermissionFlags.View,
                    PermissionFlags.Start,
                    PermissionFlags.Stop,
                    PermissionFlags.Restart,
                    PermissionFlags.Delete,
                    PermissionFlags.Update,
                    PermissionFlags.Logs,
                    PermissionFlags.Execute,
                  ])}
                  className="px-3 py-1 text-sm bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded hover:bg-gray-300 dark:hover:bg-gray-600"
                >
                  Full Access
                </button>
              </div>

              <div className="flex gap-4 mt-6">
                <button
                  type="button"
                  onClick={() => {
                    setShowCreateModal(false);
                    resetForm();
                  }}
                  className="flex-1 px-4 py-2 border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={createMutation.isPending || selectedPermissions.length === 0}
                  className="flex-1 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50"
                >
                  {createMutation.isPending ? 'Creating...' : 'Create'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
