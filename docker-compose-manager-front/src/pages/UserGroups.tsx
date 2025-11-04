import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import userGroupsApi from '../api/userGroups';
import usersApi from '../api/users';
import { useToast } from '../hooks/useToast';
import { LoadingSpinner } from '../components/common/LoadingSpinner';
import { ErrorDisplay } from '../components/common/ErrorDisplay';
import type { UserGroup, User } from '../types';

export default function UserGroups() {
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showEditModal, setShowEditModal] = useState(false);
  const [showMembersModal, setShowMembersModal] = useState(false);
  const [selectedGroup, setSelectedGroup] = useState<UserGroup | null>(null);
  const [groupName, setGroupName] = useState('');
  const [groupDescription, setGroupDescription] = useState('');
  const [selectedMembers, setSelectedMembers] = useState<number[]>([]);

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

  const createMutation = useMutation({
    mutationFn: (data: { name: string; description?: string; memberIds?: number[] }) =>
      userGroupsApi.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['userGroups'] });
      toast.success('Group created successfully');
      setShowCreateModal(false);
      resetForm();
    },
    onError: (error: any) => {
      toast.error(error.response?.data?.message || 'Failed to create group');
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: number; data: { name: string; description?: string } }) =>
      userGroupsApi.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['userGroups'] });
      toast.success('Group updated successfully');
      setShowEditModal(false);
      resetForm();
    },
    onError: (error: any) => {
      toast.error(error.response?.data?.message || 'Failed to update group');
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => userGroupsApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['userGroups'] });
      toast.success('Group deleted successfully');
    },
    onError: (error: any) => {
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
    onError: (error: any) => {
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
    onError: (error: any) => {
      toast.error(error.response?.data?.message || 'Failed to remove member');
    },
  });

  const resetForm = () => {
    setGroupName('');
    setGroupDescription('');
    setSelectedMembers([]);
    setSelectedGroup(null);
  };

  const handleCreate = (e: React.FormEvent) => {
    e.preventDefault();
    createMutation.mutate({
      name: groupName,
      description: groupDescription || undefined,
      memberIds: selectedMembers.length > 0 ? selectedMembers : undefined,
    });
  };

  const handleUpdate = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedGroup) return;
    updateMutation.mutate({
      id: selectedGroup.id,
      data: {
        name: groupName,
        description: groupDescription || undefined,
      },
    });
  };

  const handleEdit = (group: UserGroup) => {
    setSelectedGroup(group);
    setGroupName(group.name);
    setGroupDescription(group.description || '');
    setShowEditModal(true);
  };

  const handleViewMembers = (group: UserGroup) => {
    setSelectedGroup(group);
    setShowMembersModal(true);
  };

  const handleAddMember = (userId: number) => {
    if (!selectedGroup) return;
    addMemberMutation.mutate({ groupId: selectedGroup.id, userId });
  };

  const handleRemoveMember = (userId: number) => {
    if (!selectedGroup) return;
    removeMemberMutation.mutate({ groupId: selectedGroup.id, userId });
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
          <h1 className="text-4xl font-bold text-gray-900 dark:text-white mb-3">User Groups</h1>
          <p className="text-lg text-gray-600 dark:text-gray-400">
            Manage user groups for easier permission assignment
          </p>
        </div>
        <button
          onClick={() => setShowCreateModal(true)}
          className="flex items-center gap-2 bg-gradient-to-r from-blue-600 to-blue-700 text-white px-6 py-3 rounded-xl hover:shadow-lg hover:scale-105 transition-all duration-200 font-medium"
        >
          <span>+</span> Create Group
        </button>
      </div>

      {/* Groups Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {groups?.map((group) => (
          <div
            key={group.id}
            className="bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl shadow-lg border border-gray-100 dark:border-gray-700 p-6 hover:shadow-xl transition-all duration-200"
          >
            <div className="flex justify-between items-start mb-4">
              <h3 className="text-xl font-bold text-gray-900 dark:text-white">{group.name}</h3>
              <span className="px-3 py-1 bg-blue-100 dark:bg-blue-900/30 text-blue-800 dark:text-blue-300 rounded-full text-sm font-medium">
                {group.memberCount} {group.memberCount === 1 ? 'member' : 'members'}
              </span>
            </div>

            {group.description && (
              <p className="text-gray-600 dark:text-gray-400 text-sm mb-4">{group.description}</p>
            )}

            <div className="flex gap-2 mt-4">
              <button
                onClick={() => handleViewMembers(group)}
                className="flex-1 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors text-sm font-medium"
              >
                Members
              </button>
              <button
                onClick={() => handleEdit(group)}
                className="flex-1 px-4 py-2 bg-gray-600 text-white rounded-lg hover:bg-gray-700 transition-colors text-sm font-medium"
              >
                Edit
              </button>
              <button
                onClick={() => {
                  if (confirm(`Are you sure you want to delete the group "${group.name}"?`)) {
                    deleteMutation.mutate(group.id);
                  }
                }}
                className="px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 transition-colors text-sm font-medium"
              >
                Delete
              </button>
            </div>
          </div>
        ))}
      </div>

      {groups?.length === 0 && (
        <div className="text-center py-12">
          <p className="text-gray-500 dark:text-gray-400 text-lg">
            No user groups found. Create one to get started!
          </p>
        </div>
      )}

      {/* Create Group Modal */}
      {showCreateModal && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-2xl p-8 max-w-md w-full m-4">
            <h2 className="text-2xl font-bold text-gray-900 dark:text-white mb-6">Create Group</h2>
            <form onSubmit={handleCreate} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                  Group Name *
                </label>
                <input
                  type="text"
                  value={groupName}
                  onChange={(e) => setGroupName(e.target.value)}
                  className="w-full px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 dark:bg-gray-700 dark:text-white"
                  required
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                  Description
                </label>
                <textarea
                  value={groupDescription}
                  onChange={(e) => setGroupDescription(e.target.value)}
                  className="w-full px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 dark:bg-gray-700 dark:text-white"
                  rows={3}
                />
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
                  disabled={createMutation.isPending}
                  className="flex-1 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50"
                >
                  {createMutation.isPending ? 'Creating...' : 'Create'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Edit Group Modal */}
      {showEditModal && selectedGroup && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-2xl p-8 max-w-md w-full m-4">
            <h2 className="text-2xl font-bold text-gray-900 dark:text-white mb-6">Edit Group</h2>
            <form onSubmit={handleUpdate} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                  Group Name *
                </label>
                <input
                  type="text"
                  value={groupName}
                  onChange={(e) => setGroupName(e.target.value)}
                  className="w-full px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 dark:bg-gray-700 dark:text-white"
                  required
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                  Description
                </label>
                <textarea
                  value={groupDescription}
                  onChange={(e) => setGroupDescription(e.target.value)}
                  className="w-full px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 dark:bg-gray-700 dark:text-white"
                  rows={3}
                />
              </div>
              <div className="flex gap-4 mt-6">
                <button
                  type="button"
                  onClick={() => {
                    setShowEditModal(false);
                    resetForm();
                  }}
                  className="flex-1 px-4 py-2 border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={updateMutation.isPending}
                  className="flex-1 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50"
                >
                  {updateMutation.isPending ? 'Updating...' : 'Update'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Members Modal */}
      {showMembersModal && selectedGroup && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-2xl p-8 max-w-2xl w-full m-4 max-h-[80vh] overflow-y-auto">
            <div className="flex justify-between items-center mb-6">
              <h2 className="text-2xl font-bold text-gray-900 dark:text-white">
                Members of {selectedGroup.name}
              </h2>
              <button
                onClick={() => {
                  setShowMembersModal(false);
                  setSelectedGroup(null);
                }}
                className="text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
              >
                ✕
              </button>
            </div>

            {/* Current Members */}
            <div className="mb-6">
              <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-3">
                Current Members ({groupMembers?.length || 0})
              </h3>
              <div className="space-y-2">
                {groupMembers?.map((member: User) => (
                  <div
                    key={member.id}
                    className="flex justify-between items-center p-3 bg-gray-50 dark:bg-gray-700 rounded-lg"
                  >
                    <div>
                      <span className="font-medium text-gray-900 dark:text-white">
                        {member.username}
                      </span>
                      <span className="ml-2 text-sm text-gray-500 dark:text-gray-400">
                        ({member.role})
                      </span>
                    </div>
                    <button
                      onClick={() => handleRemoveMember(member.id)}
                      className="px-3 py-1 bg-red-600 text-white rounded-lg hover:bg-red-700 transition-colors text-sm"
                    >
                      Remove
                    </button>
                  </div>
                ))}
                {groupMembers?.length === 0 && (
                  <p className="text-gray-500 dark:text-gray-400 text-center py-4">
                    No members in this group yet
                  </p>
                )}
              </div>
            </div>

            {/* Available Users */}
            {availableUsers && availableUsers.length > 0 && (
              <div>
                <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-3">
                  Add Members
                </h3>
                <div className="space-y-2">
                  {availableUsers.map((user: User) => (
                    <div
                      key={user.id}
                      className="flex justify-between items-center p-3 bg-gray-50 dark:bg-gray-700 rounded-lg"
                    >
                      <div>
                        <span className="font-medium text-gray-900 dark:text-white">
                          {user.username}
                        </span>
                        <span className="ml-2 text-sm text-gray-500 dark:text-gray-400">
                          ({user.role})
                        </span>
                      </div>
                      <button
                        onClick={() => handleAddMember(user.id)}
                        className="px-3 py-1 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors text-sm"
                      >
                        Add
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
