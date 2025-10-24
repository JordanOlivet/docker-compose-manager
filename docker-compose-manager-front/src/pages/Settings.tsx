import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import configApi, { type ComposePath } from '../api/config';
import { useToast } from '../hooks/useToast';
import { LoadingSpinner } from '../components/common/LoadingSpinner';
import { FolderPicker } from '../components/common/FolderPicker';

export default function Settings() {
  const [showAddPath, setShowAddPath] = useState(false);
  const [showFolderPicker, setShowFolderPicker] = useState(false);
  const [newPath, setNewPath] = useState('');
  const [isReadOnly, setIsReadOnly] = useState(false);

  const queryClient = useQueryClient();
  const toast = useToast();

  const { data: paths, isLoading } = useQuery({
    queryKey: ['composePaths'],
    queryFn: configApi.getPaths,
  });

  const addPathMutation = useMutation({
    mutationFn: (data: { path: string; isReadOnly: boolean }) => configApi.addPath(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['composePaths'] });
      toast.success('Path added successfully');
      setShowAddPath(false);
      setNewPath('');
      setIsReadOnly(false);
    },
    onError: (error: any) => {
      toast.error(error.response?.data?.message || 'Failed to add path');
    },
  });

  const deletePathMutation = useMutation({
    mutationFn: (id: number) => configApi.deletePath(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['composePaths'] });
      toast.success('Path deleted successfully');
    },
  });

  const toggleEnabledMutation = useMutation({
    mutationFn: ({ id, enabled }: { id: number; enabled: boolean }) =>
      configApi.updatePath(id, { isEnabled: !enabled }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['composePaths'] });
      toast.success('Path status updated');
    },
  });

  const handleAddPath = (e: React.FormEvent) => {
    e.preventDefault();
    addPathMutation.mutate({ path: newPath, isReadOnly });
  };

  if (isLoading) return <LoadingSpinner />;

  return (
    <div>
      <h1 className="text-2xl font-bold mb-6">Settings</h1>

      <div className="bg-white rounded-lg shadow p-6 mb-6">
        <div className="flex justify-between items-center mb-4">
          <h2 className="text-xl font-semibold">Compose File Paths</h2>
          <button
            onClick={() => setShowAddPath(true)}
            className="bg-blue-500 text-white px-4 py-2 rounded hover:bg-blue-600"
          >
            Add Path
          </button>
        </div>

        <table className="w-full">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Path</th>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Access</th>
              <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {paths?.map((path: ComposePath) => (
              <tr key={path.id}>
                <td className="px-4 py-3 font-mono text-sm">{path.path}</td>
                <td className="px-4 py-3">
                  <span className={`px-2 py-1 rounded text-xs ${path.isEnabled ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'}`}>
                    {path.isEnabled ? 'Enabled' : 'Disabled'}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <span className={`px-2 py-1 rounded text-xs ${path.isReadOnly ? 'bg-yellow-100 text-yellow-800' : 'bg-blue-100 text-blue-800'}`}>
                    {path.isReadOnly ? 'Read-Only' : 'Read-Write'}
                  </span>
                </td>
                <td className="px-4 py-3 space-x-2">
                  <button
                    onClick={() => toggleEnabledMutation.mutate({ id: path.id, enabled: path.isEnabled })}
                    className="text-blue-600 hover:underline"
                  >
                    {path.isEnabled ? 'Disable' : 'Enable'}
                  </button>
                  <button
                    onClick={() => { if (confirm(`Delete path ${path.path}?`)) deletePathMutation.mutate(path.id); }}
                    className="text-red-600 hover:underline"
                  >
                    Delete
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showAddPath && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white p-6 rounded-lg w-96">
            <h2 className="text-xl font-bold mb-4">Add Compose Path</h2>
            <form onSubmit={handleAddPath} className="space-y-4">
              <div>
                <label className="block text-sm font-medium mb-2">Directory Path</label>
                <div className="flex gap-2">
                  <input
                    type="text"
                    value={newPath}
                    onChange={(e) => setNewPath(e.target.value)}
                    placeholder="/path/to/compose/files"
                    className="flex-1 border rounded px-3 py-2"
                    required
                  />
                  <button
                    type="button"
                    onClick={() => setShowFolderPicker(true)}
                    className="px-3 py-2 bg-gray-200 rounded hover:bg-gray-300 flex items-center gap-1"
                  >
                    <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 20 20">
                      <path d="M2 6a2 2 0 012-2h5l2 2h5a2 2 0 012 2v6a2 2 0 01-2 2H4a2 2 0 01-2-2V6z" />
                    </svg>
                    Browse
                  </button>
                </div>
              </div>
              <div className="flex items-center">
                <input
                  type="checkbox"
                  checked={isReadOnly}
                  onChange={(e) => setIsReadOnly(e.target.checked)}
                  className="mr-2"
                />
                <label className="text-sm">Read-only access</label>
              </div>
              <div className="flex space-x-2">
                <button type="submit" className="flex-1 bg-blue-500 text-white py-2 rounded hover:bg-blue-600">
                  Add Path
                </button>
                <button
                  type="button"
                  onClick={() => setShowAddPath(false)}
                  className="flex-1 bg-gray-300 py-2 rounded hover:bg-gray-400"
                >
                  Cancel
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showFolderPicker && (
        <FolderPicker
          initialPath={newPath}
          onSelect={(path) => {
            setNewPath(path);
            setShowFolderPicker(false);
          }}
          onCancel={() => setShowFolderPicker(false)}
        />
      )}
    </div>
  );
}
