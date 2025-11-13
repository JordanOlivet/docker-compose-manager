import { useState, useMemo } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { AxiosError } from 'axios';
import configApi, { type ComposePath } from '../api/config';
import { composeApi } from '../api/compose';
import { useToast } from '../hooks/useToast';
import { LoadingSpinner } from '../components/common/LoadingSpinner';
import { FolderPicker } from '../components/common/FolderPicker';
import { type ApiErrorResponse } from '../utils/errorFormatter';

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

  // Retrieve discovered Docker Compose projects (includes those outside configured paths)
  const { data: projects } = useQuery({
    queryKey: ['composeProjects'],
    queryFn: composeApi.listProjects,
  });

  // Path normalization for comparison (Windows + trailing slash removal)
  const normalizePath = (p: string) => p.replace(/\\/g, '/').replace(/\/+/g, '/').toLowerCase().replace(/\/$/, '');

  // Extract external projects (name + path) detected outside configured paths
  const externalProjects = useMemo((): { path: string; name: string }[] => {
    if (!projects || !paths) return [];
    const configured = paths.map(p => normalizePath(p.path));
    const map = new Map<string, { path: string; name: string }>();

    for (const proj of projects) {
      if (!proj.path) continue;
      const projNorm = normalizePath(proj.path);
      const isInside = configured.some(cfg => projNorm.startsWith(cfg) && (projNorm.length === cfg.length || projNorm[cfg.length] === '/'));
      if (!isInside) {
        // Use the project name returned by the API, otherwise fallback
        map.set(proj.path, { path: proj.path, name: proj.name || 'Projet sans nom' });
      }
    }
    return Array.from(map.values()).sort((a, b) => a.path.localeCompare(b.path));
  }, [projects, paths]);

  const addPathMutation = useMutation({
    mutationFn: (data: { path: string; isReadOnly: boolean }) => configApi.addPath(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['composePaths'] });
      toast.success('Path added successfully');
      setShowAddPath(false);
      setNewPath('');
      setIsReadOnly(false);
    },
    onError: (error: AxiosError<ApiErrorResponse>) => {
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

  if (isLoading) return (
    <div className="flex items-center justify-center h-96">
      <LoadingSpinner size="lg" text="Loading settings..." />
    </div>
  );

  return (
    <div className="space-y-8">
      {/* Page Header */}
      <div className="mb-8">
        <h1 className="text-4xl font-bold text-gray-900 dark:text-white mb-3">Settings</h1>
        <p className="text-lg text-gray-600 dark:text-gray-400">
          Configure your Docker Compose management system
        </p>
      </div>

      {/* Compose File Paths Section */}
      <div className="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl shadow-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
        <div className="flex justify-between items-center p-6 border-b border-gray-200 dark:border-gray-700 bg-white/50 dark:bg-gray-800/50">
          <div className="flex items-center gap-3">
            <div className="p-2 bg-blue-100 dark:bg-blue-900/30 rounded-lg">
              <svg className="w-5 h-5 text-blue-600 dark:text-blue-400" fill="currentColor" viewBox="0 0 20 20">
                <path d="M2 6a2 2 0 012-2h5l2 2h5a2 2 0 012 2v6a2 2 0 01-2 2H4a2 2 0 01-2-2V6z" />
              </svg>
            </div>
            <h2 className="text-xl font-bold text-gray-900 dark:text-white">Compose File Paths</h2>
          </div>
          <button
            onClick={() => setShowAddPath(true)}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-blue-600 dark:bg-blue-700 rounded-lg hover:bg-blue-700 dark:hover:bg-blue-600 transition-colors shadow-lg hover:shadow-xl"
          >
            <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M10 3a1 1 0 011 1v5h5a1 1 0 110 2h-5v5a1 1 0 11-2 0v-5H4a1 1 0 110-2h5V4a1 1 0 011-1z" clipRule="evenodd" />
            </svg>
            Add Path
          </button>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full divide-y divide-gray-200 dark:divide-gray-700">
            <thead className="bg-white/50 dark:bg-gray-800/50">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">Path</th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">Status</th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">Access</th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">Actions</th>
              </tr>
            </thead>
            <tbody className="bg-white dark:bg-gray-800 divide-y divide-gray-200 dark:divide-gray-700">
              {paths?.map((path: ComposePath) => (
                <tr key={path.id} className="hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors">
                  <td className="px-6 py-4 font-mono text-sm text-gray-900 dark:text-white">{path.path}</td>
                  <td className="px-6 py-4">
                    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                      path.isEnabled
                        ? 'bg-green-100 dark:bg-green-900/30 text-green-800 dark:text-green-300'
                        : 'bg-gray-100 dark:bg-gray-700 text-gray-800 dark:text-gray-300'
                    }`}>
                      {path.isEnabled ? 'Enabled' : 'Disabled'}
                    </span>
                  </td>
                  <td className="px-6 py-4">
                    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                      path.isReadOnly
                        ? 'bg-yellow-100 dark:bg-yellow-900/30 text-yellow-800 dark:text-yellow-300'
                        : 'bg-blue-100 dark:bg-blue-900/30 text-blue-800 dark:text-blue-300'
                    }`}>
                      {path.isReadOnly ? 'Read-Only' : 'Read-Write'}
                    </span>
                  </td>
                  <td className="px-6 py-4">
                    <div className="flex gap-2">
                      <button
                        onClick={() => toggleEnabledMutation.mutate({ id: path.id, enabled: path.isEnabled })}
                        className="text-sm font-medium text-blue-600 dark:text-blue-400 hover:underline"
                      >
                        {path.isEnabled ? 'Disable' : 'Enable'}
                      </button>
                      <button
                        onClick={() => { if (confirm(`Delete path ${path.path}?`)) deletePathMutation.mutate(path.id); }}
                        className="text-sm font-medium text-red-600 dark:text-red-400 hover:underline"
                      >
                        Delete
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Encart de warning pour les projets externes détectés */}
        {externalProjects.length > 0 && (
          <div className="p-6 space-y-4 border-t border-gray-200 dark:border-gray-700 bg-white/40 dark:bg-gray-800/40">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white flex items-center gap-2">
              <svg className="w-5 h-5 text-yellow-500" fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l6.342 11.28A2 2 0 0116.342 17H3.658a2 2 0 01-1.743-2.62l6.342-11.28zM11 13a1 1 0 10-2 0 1 1 0 002 0zm-1-2a.75.75 0 01-.75-.75v-3.5a.75.75 0 011.5 0v3.5A.75.75 0 0110 11z" clipRule="evenodd" />
              </svg>
              Projets détectés hors des chemins configurés
            </h3>
            {externalProjects.map((proj) => (
              <div
                key={proj.path}
                className="w-full flex flex-col md:flex-row md:items-center justify-between gap-4 p-4 border border-yellow-300 dark:border-yellow-600 rounded-xl bg-yellow-50 dark:bg-yellow-900/20 shadow-sm"
              >
                <div className="flex-1">
                  <p className="text-sm text-yellow-800 dark:text-yellow-200">
                    Chemin: <span className="font-mono">{proj.path}</span>
                  </p>
                  <p className="text-xs mt-1 text-yellow-700 dark:text-yellow-300">
                    <span className="font-medium">Projet:</span> <span className="font-semibold">{proj.name}</span>
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <button
                    onClick={() => addPathMutation.mutate({ path: proj.path, isReadOnly: false })}
                    className="px-4 py-2 text-sm font-medium rounded-lg bg-yellow-500 hover:bg-yellow-600 text-white shadow-md hover:shadow-lg transition-colors"
                  >
                    Ajouter ce path
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Add Path Modal */}
      {showAddPath && (
        <div className="fixed inset-0 bg-black/50 dark:bg-black/70 backdrop-blur-sm flex items-center justify-center z-50">
          <div className="bg-white dark:bg-gray-800 p-6 rounded-2xl w-full max-w-md shadow-2xl border border-gray-200 dark:border-gray-700">
            <h2 className="text-2xl font-bold text-gray-900 dark:text-white mb-6">Add Compose Path</h2>
            <form onSubmit={handleAddPath} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Directory Path</label>
                <div className="flex gap-2">
                  <input
                    type="text"
                    value={newPath}
                    onChange={(e) => setNewPath(e.target.value)}
                    placeholder="/path/to/compose/files"
                    className="flex-1 border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 dark:focus:ring-blue-400 focus:border-blue-500"
                    required
                  />
                  <button
                    type="button"
                    onClick={() => setShowFolderPicker(true)}
                    className="px-3 py-2 bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-300 dark:hover:bg-gray-600 transition-colors flex items-center gap-1"
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
                  className="w-4 h-4 text-blue-600 bg-gray-100 dark:bg-gray-700 border-gray-300 dark:border-gray-600 rounded focus:ring-blue-500 dark:focus:ring-blue-400 focus:ring-2"
                />
                <label className="ml-2 text-sm text-gray-700 dark:text-gray-300">Read-only access</label>
              </div>
              <div className="flex space-x-2 pt-4">
                <button
                  type="submit"
                  className="flex-1 bg-blue-600 dark:bg-blue-700 text-white py-2 rounded-lg hover:bg-blue-700 dark:hover:bg-blue-600 transition-colors font-medium shadow-lg hover:shadow-xl"
                >
                  Add Path
                </button>
                <button
                  type="button"
                  onClick={() => setShowAddPath(false)}
                  className="flex-1 bg-gray-300 dark:bg-gray-600 text-gray-700 dark:text-gray-300 py-2 rounded-lg hover:bg-gray-400 dark:hover:bg-gray-500 transition-colors font-medium"
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
