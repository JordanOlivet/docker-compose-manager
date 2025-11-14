import { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { AxiosError } from 'axios';
import userGroupsApi from '../api/userGroups';
import usersApi from '../api/users';
import permissionsApi from '../api/permissions';
import { useToast } from '../hooks/useToast';
import { LoadingSpinner } from '../components/common/LoadingSpinner';
import { ErrorDisplay } from '../components/common/ErrorDisplay';
import { PermissionSelector } from '../components/PermissionSelector';
import { CopyPermissionsDialog } from '../components/CopyPermissionsDialog';
import type { UserGroup } from '../types';
import { type ApiErrorResponse } from '../utils/errorFormatter';
import type { ResourcePermissionInput } from '../types/permissions';
import { t } from '../i18n';

function UserGroups() {
  const [showModal, setShowModal] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [selectedGroup, setSelectedGroup] = useState<UserGroup | null>(null);
  const [activeTab, setActiveTab] = useState<'info' | 'members' | 'permissions'>('info');
  const [groupName, setGroupName] = useState('');
  const [groupDescription, setGroupDescription] = useState('');
  const [selectedMembers, setSelectedMembers] = useState<number[]>([]);
  const [permissions, setPermissions] = useState<ResourcePermissionInput[]>([]);
  const [showCopyDialog, setShowCopyDialog] = useState(false);

  const queryClient = useQueryClient();
  const toast = useToast();

  const { data: groups, isLoading, error } = useQuery({
    queryKey: ['userGroups'],
    queryFn: userGroupsApi.list,
  });

  const { data: allUsers } = useQuery({
    queryKey: ['users'],
    queryFn: usersApi.list,
  });

  const { data: groupMembers } = useQuery({
    queryKey: ['groupMembers', selectedGroup?.id],
    queryFn: () => userGroupsApi.getMembers(selectedGroup!.id),
    enabled: !!selectedGroup,
  });

  // Fetch group permissions when editing
  const { data: groupPermissions } = useQuery({
    queryKey: ['permissions', 'group', selectedGroup?.id],
    queryFn: () => selectedGroup ? permissionsApi.list({ userGroupId: selectedGroup.id }) : Promise.resolve([]),
    enabled: editMode && !!selectedGroup
  });

  // Update permissions when groupPermissions changes
  useEffect(() => {
    if (groupPermissions) {
      setPermissions(groupPermissions.map(p => ({
        resourceType: p.resourceType,
        resourceName: p.resourceName,
        permissions: p.permissions
      })));
    }
  }, [groupPermissions]);

  const createMutation = useMutation({
    mutationFn: (data: { name: string; description?: string; memberIds?: number[]; permissions?: ResourcePermissionInput[] }) =>
      userGroupsApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['userGroups'] });
      queryClient.invalidateQueries({ queryKey: ['permissions'] });
      toast.success('Group created successfully');
      closeModal();
    },
    onError: (error: AxiosError<ApiErrorResponse>) => {
      toast.error(error.response?.data?.message || 'Failed to create group');
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: number; data: { name: string; description?: string; permissions?: ResourcePermissionInput[] } }) =>
      userGroupsApi.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['userGroups'] });
      queryClient.invalidateQueries({ queryKey: ['permissions'] });
      toast.success('Group updated successfully');
      closeModal();
    },
    onError: (error: AxiosError<ApiErrorResponse>) => {
      toast.error(error.response?.data?.message || 'Failed to update group');
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => userGroupsApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['userGroups'] });
      queryClient.invalidateQueries({ queryKey: ['permissions'] });
      toast.success('Group deleted successfully');
    },
    onError: (error: AxiosError<ApiErrorResponse>) => {
      toast.error(error.response?.data?.message || 'Failed to delete group');
    },
  });

  const addMemberMutation = useMutation({
    mutationFn: ({ groupId, userId }: { groupId: number; userId: number }) =>
      userGroupsApi.addMember(groupId, { userId }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['groupMembers'] });
      queryClient.invalidateQueries({ queryKey: ['userGroups'] });
      toast.success('Member added successfully');
    },
    onError: (error: AxiosError<ApiErrorResponse>) => {
      toast.error(error.response?.data?.message || 'Failed to add member');
    },
  });

  const removeMemberMutation = useMutation({
    mutationFn: ({ groupId, userId }: { groupId: number; userId: number }) =>
      userGroupsApi.removeMember(groupId, userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['groupMembers'] });
      queryClient.invalidateQueries({ queryKey: ['userGroups'] });
      toast.success('Member removed successfully');
    },
    onError: (error: AxiosError<ApiErrorResponse>) => {
      toast.error(error.response?.data?.message || 'Failed to remove member');
    },
  });

  const openCreateModal = () => {
    setEditMode(false);
    setSelectedGroup(null);
    setGroupName('');
    setGroupDescription('');
    setSelectedMembers([]);
    setPermissions([]);
    setActiveTab('info');
    setShowModal(true);
  };

  const openEditModal = (group: UserGroup) => {
    setEditMode(true);
    setSelectedGroup(group);
    setGroupName(group.name);
    setGroupDescription(group.description || '');
    setSelectedMembers(group.memberIds || []);
    setActiveTab('info');
    setShowModal(true);
  };

  const closeModal = () => {
    setShowModal(false);
    setEditMode(false);
    setSelectedGroup(null);
    setGroupName('');
    setGroupDescription('');
    setSelectedMembers([]);
    setPermissions([]);
    setActiveTab('info');
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const groupData = {
      name: groupName,
      description: groupDescription || undefined,
      permissions: permissions.length > 0 ? permissions : undefined
    };

    if (editMode && selectedGroup) {
      updateMutation.mutate({ id: selectedGroup.id, data: groupData });
    } else {
      createMutation.mutate({
        ...groupData,
        memberIds: selectedMembers.length > 0 ? selectedMembers : undefined
      });
    }
  };

  const handleAddMember = (userId: number) => {
    if (!selectedGroup) return;
    addMemberMutation.mutate({ groupId: selectedGroup.id, userId });
  };

  const handleRemoveMember = (userId: number) => {
    if (!selectedGroup) return;
    removeMemberMutation.mutate({ groupId: selectedGroup.id, userId });
  };

  const handleCopySuccess = () => {
    // Refresh permissions after copy
    if (selectedGroup) {
      queryClient.invalidateQueries({ queryKey: ['permissions', 'group', selectedGroup.id] });
    }
  };

  const availableUsers = allUsers?.filter(
    (user) => !groupMembers?.some((member) => member.id === user.id)
  );

  if (isLoading) return <LoadingSpinner />;
  if (error) return <ErrorDisplay message="Failed to load user groups" />;

  return (
    <div className="space-y-8">
      {/* Page Header */}
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-4xl font-bold text-gray-900 dark:text-white mb-3">{t('users.groups')}</h1>
          <p className="text-lg text-gray-600 dark:text-gray-400">
            {t('users.groupsSubtitle')}
          </p>
        </div>
        <button
          onClick={openCreateModal}
          className="flex items-center gap-2 bg-linear-to-r from-blue-600 to-blue-700 text-white px-6 py-3 rounded-xl hover:shadow-lg hover:scale-105 transition-all duration-200 font-medium"
        >
          <span>+</span> {t('common.create')} {t('users.groups')}
        </button>
      </div>

      {/* Groups Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {groups?.map((group) => (
          <div
            key={group.id}
            className="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl shadow-lg border border-gray-100 dark:border-gray-700 p-6 hover:shadow-xl transition-all duration-200"
          >
            <div className="flex justify-between items-start mb-4">
              <h3 className="text-xl font-bold text-gray-900 dark:text-white">{group.name}</h3>
              <span className="px-3 py-1 bg-blue-100 dark:bg-blue-900/30 text-blue-800 dark:text-blue-300 rounded-full text-sm font-medium">
                {group.memberCount} {group.memberCount === 1 ? t('users.members').slice(0, -1) : t('users.members')}
              </span>
            </div>

            {group.description && (
              <p className="text-gray-600 dark:text-gray-400 text-sm mb-4">{group.description}</p>
            )}

            <div className="flex gap-2 mt-4">
              <button
                onClick={() => openEditModal(group)}
                className="flex-1 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors text-sm font-medium"
              >
                {t('common.edit')}
              </button>
              <button
                onClick={() => {
                  if (confirm(`${t('users.deleteUser')} "${group.name}"?`)) {
                    deleteMutation.mutate(group.id);
                  }
                }}
                className="px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 transition-colors text-sm font-medium"
              >
                {t('common.delete')}
              </button>
            </div>
          </div>
        ))}
      </div>

      {groups?.length === 0 && (
        <div className="text-center py-12">
          <p className="text-gray-500 dark:text-gray-400 text-lg">
            {t('users.noUsers')}
          </p>
        </div>
      )}

      {/* Create/Edit Modal */}
      {showModal && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-2xl w-full max-w-4xl border border-gray-200 dark:border-gray-700 max-h-[90vh] overflow-hidden flex flex-col">
            <div className="p-8 border-b border-gray-200 dark:border-gray-700">
              <h2 className="text-2xl font-bold text-gray-900 dark:text-white">
                {editMode ? t('common.edit') : t('common.create')} {t('users.groups')}
              </h2>
            </div>

            {/* Tabs */}
            <div className="border-b border-gray-200 dark:border-gray-700 px-8">
              <div className="flex gap-4">
                <button
                  onClick={() => setActiveTab('info')}
                  className={`py-4 px-2 font-medium text-sm transition-all relative ${
                    activeTab === 'info'
                      ? 'text-blue-600 dark:text-blue-400'
                      : 'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300'
                  }`}
                >
                  {t('users.general')}
                  {activeTab === 'info' && (
                    <div className="absolute bottom-0 left-0 right-0 h-0.5 bg-blue-600 dark:bg-blue-400"></div>
                  )}
                </button>
                <button
                  onClick={() => setActiveTab('members')}
                  className={`py-4 px-2 font-medium text-sm transition-all relative ${
                    activeTab === 'members'
                      ? 'text-blue-600 dark:text-blue-400'
                      : 'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300'
                  }`}
                >
                  {t('users.members')}
                  {(editMode ? (groupMembers?.length ?? 0) : selectedMembers.length) > 0 && (
                    <span className="ml-2 px-2 py-0.5 bg-blue-100 dark:bg-blue-900/30 text-blue-600 dark:text-blue-400 rounded-full text-xs">
                      {editMode ? (groupMembers?.length ?? 0) : selectedMembers.length}
                    </span>
                  )}
                  {activeTab === 'members' && (
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
              {/* Information Tab */}
              {activeTab === 'info' && (
                <div className="p-8 space-y-6">
                  <div>
                    <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">
                      {t('users.groupName')}
                    </label>
                    <input
                      type="text"
                      value={groupName}
                      onChange={(e) => setGroupName(e.target.value)}
                      className="w-full border border-gray-300 dark:border-gray-600 rounded-xl px-4 py-3 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
                      placeholder={t('users.groupNamePlaceholder')}
                      required
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">
                      {t('users.description')}
                    </label>
                    <textarea
                      value={groupDescription}
                      onChange={(e) => setGroupDescription(e.target.value)}
                      className="w-full border border-gray-300 dark:border-gray-600 rounded-xl px-4 py-3 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
                      placeholder={t('users.descriptionPlaceholder')}
                      rows={3}
                    />
                  </div>
                </div>
              )}

              {/* Members Tab */}
              {activeTab === 'members' && (
                <div className="p-8 space-y-6">
                  {editMode ? (
                    // Edit mode: manage members
                    <>
                      <div>
                        <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                          {t('users.members')} ({groupMembers?.length || 0})
                        </h3>
                        <div className="space-y-2 mb-6">
                          {groupMembers?.length === 0 && (
                            <p className="text-sm text-gray-500 dark:text-gray-400">{t('users.noMembers')}</p>
                          )}
                          {groupMembers?.map((member) => (
                            <div
                              key={member.id}
                              className="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-700 rounded-lg"
                            >
                              <div>
                                <span className="font-medium text-gray-900 dark:text-white">
                                  {member.username}
                                </span>
                                <span className="ml-2 text-xs text-gray-500 dark:text-gray-400">
                                  ({member.role})
                                </span>
                              </div>
                              <button
                                type="button"
                                onClick={() => handleRemoveMember(member.id)}
                                className="text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20 px-3 py-1 rounded transition-colors text-sm"
                              >
                                {t('common.delete')}
                              </button>
                            </div>
                          ))}
                        </div>
                      </div>

                      <div>
                        <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                          {t('common.create')} {t('users.members')}
                        </h3>
                        <div className="space-y-2">
                          {availableUsers?.length === 0 && (
                            <p className="text-sm text-gray-500 dark:text-gray-400">
                              {t('users.noUsers')}
                            </p>
                          )}
                          {availableUsers?.map((user) => (
                            <div
                              key={user.id}
                              className="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-700 rounded-lg"
                            >
                              <div>
                                <span className="font-medium text-gray-900 dark:text-white">
                                  {user.username}
                                </span>
                                <span className="ml-2 text-xs text-gray-500 dark:text-gray-400">
                                  ({user.role})
                                </span>
                              </div>
                              <button
                                type="button"
                                onClick={() => handleAddMember(user.id)}
                                className="text-blue-600 dark:text-blue-400 hover:bg-blue-50 dark:hover:bg-blue-900/20 px-3 py-1 rounded transition-colors text-sm"
                              >
                                {t('common.create')}
                              </button>
                            </div>
                          ))}
                        </div>
                      </div>
                    </>
                  ) : (
                    // Create mode: select initial members
                    <div>
                      <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                        {t('users.members')} ({t('common.all')})
                      </h3>
                      <div className="space-y-2">
                        {allUsers?.map((user) => (
                          <label
                            key={user.id}
                            className="flex items-center p-3 bg-gray-50 dark:bg-gray-700 rounded-lg cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-600 transition-colors"
                          >
                            <input
                              type="checkbox"
                              checked={selectedMembers.includes(user.id)}
                              onChange={(e) => {
                                if (e.target.checked) {
                                  setSelectedMembers([...selectedMembers, user.id]);
                                } else {
                                  setSelectedMembers(selectedMembers.filter((id) => id !== user.id));
                                }
                              }}
                              className="mr-3"
                            />
                            <div>
                              <span className="font-medium text-gray-900 dark:text-white">
                                {user.username}
                              </span>
                              <span className="ml-2 text-xs text-gray-500 dark:text-gray-400">
                                ({user.role})
                              </span>
                            </div>
                          </label>
                        ))}
                      </div>
                    </div>
                  )}
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
                    ? `${t('common.edit')} ${t('users.groups')}`
                    : `${t('common.create')} ${t('users.groups')}`}
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
      {selectedGroup && (
        <CopyPermissionsDialog
          open={showCopyDialog}
          onOpenChange={setShowCopyDialog}
          targetType="group"
          targetId={selectedGroup.id}
          onSuccess={handleCopySuccess}
        />
      )}
    </div>
  );
}

export default UserGroups;
