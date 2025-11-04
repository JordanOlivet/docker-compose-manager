import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, FileText, Trash2, Edit, RefreshCw, Folder } from 'lucide-react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { composeApi } from '../api';
import { LoadingSpinner, ErrorDisplay, ConfirmDialog, StatusBadge } from '../components/common';
import type { ComposeFile } from '../types';

export const ComposeFiles = () => {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [fileToDelete, setFileToDelete] = useState<ComposeFile | null>(null);

  // Fetch compose files
  const {
    data: files = [],
    isLoading,
    error,
    refetch,
  } = useQuery({
    queryKey: ['composeFiles'],
    queryFn: composeApi.listFiles,
    refetchInterval: 30000, // Refetch every 30 seconds
  });

  // Delete mutation
  const deleteMutation = useMutation({
    mutationFn: (id: number) => composeApi.deleteFile(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['composeFiles'] });
      setDeleteDialogOpen(false);
      setFileToDelete(null);
    },
  });

  const handleDelete = (file: ComposeFile) => {
    setFileToDelete(file);
    setDeleteDialogOpen(true);
  };

  const confirmDelete = () => {
    if (fileToDelete) {
      deleteMutation.mutate(fileToDelete.id);
    }
  };

  const handleEdit = (file: ComposeFile) => {
    navigate(`/compose/files/${file.id}/edit`);
  };

  const handleCreate = () => {
    navigate('/compose/files/create');
  };

  // Group files by directory
  const filesByDirectory = files.reduce((acc, file) => {
    const dir = file.directory || '/';
    if (!acc[dir]) {
      acc[dir] = [];
    }
    acc[dir].push(file);
    return acc;
  }, {} as Record<string, ComposeFile[]>);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-96">
        <LoadingSpinner size="lg" text="Loading compose files..." />
      </div>
    );
  }

  if (error) {
    return (
      <ErrorDisplay
        title="Failed to load compose files"
        message={error instanceof Error ? error.message : 'An unexpected error occurred'}
        onRetry={() => refetch()}
      />
    );
  }

  return (
    <div className="space-y-8">
      {/* Page Header */}
      <div className="mb-8">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-4xl font-bold text-gray-900 dark:text-white mb-3">Compose Files</h1>
            <p className="text-lg text-gray-600 dark:text-gray-400">
              Manage your Docker Compose configuration files
            </p>
          </div>
          <div className="flex gap-3">
            <button
              onClick={() => refetch()}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
            >
              <RefreshCw className="w-4 h-4" />
              Refresh
            </button>
            <button
              onClick={handleCreate}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-blue-600 dark:bg-blue-700 rounded-lg hover:bg-blue-700 dark:hover:bg-blue-600 transition-colors shadow-lg hover:shadow-xl"
            >
              <Plus className="w-4 h-4" />
              New File
            </button>
          </div>
        </div>
      </div>

      {/* File Count */}
      <div className="mb-4">
        <p className="text-sm text-gray-600 dark:text-gray-400">
          {files.length} {files.length === 1 ? 'file' : 'files'} found in{' '}
          {Object.keys(filesByDirectory).length}{' '}
          {Object.keys(filesByDirectory).length === 1 ? 'directory' : 'directories'}
        </p>
      </div>

      {/* Files by Directory */}
      {files.length === 0 ? (
        <div className="text-center py-12 bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg">
          <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-gray-100 dark:bg-gray-700 mb-3">
            <FileText className="w-8 h-8 text-gray-400" />
          </div>
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">No compose files found</h3>
          <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
            Get started by creating a new compose file or configuring compose paths
          </p>
          <button
            onClick={handleCreate}
            className="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-blue-600 dark:bg-blue-700 rounded-lg hover:bg-blue-700 dark:hover:bg-blue-600 transition-colors shadow-lg hover:shadow-xl"
          >
            <Plus className="w-4 h-4" />
            Create First File
          </button>
        </div>
      ) : (
        <div className="space-y-6">
          {Object.entries(filesByDirectory).map(([directory, dirFiles]) => (
            <div key={directory} className="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl border border-gray-200 dark:border-gray-700 overflow-hidden shadow-lg">
              {/* Directory Header */}
              <div className="bg-white/50 dark:bg-gray-800/50 px-4 py-3 border-b border-gray-200 dark:border-gray-700">
                <div className="flex items-center gap-2">
                  <div className="p-2 bg-blue-100 dark:bg-blue-900/30 rounded-lg">
                    <Folder className="w-4 h-4 text-blue-600 dark:text-blue-400" />
                  </div>
                  <span className="text-sm font-medium text-gray-900 dark:text-white">{directory}</span>
                  <span className="text-xs px-2 py-0.5 rounded-full bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 font-medium">
                    {dirFiles.length}
                  </span>
                </div>
              </div>

              {/* Files Table */}
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
                  <thead className="bg-white/50 dark:bg-gray-800/50">
                    <tr>
                      <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
                        Name
                      </th>
                      <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
                        Size
                      </th>
                      <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
                        Modified
                      </th>
                      <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
                        Source
                      </th>
                      <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
                        Actions
                      </th>
                    </tr>
                  </thead>
                  <tbody className="bg-white dark:bg-gray-800 divide-y divide-gray-200 dark:divide-gray-700">
                    {dirFiles.map((file) => (
                      <tr key={file.id} className="hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors">
                        <td className="px-4 py-3 whitespace-nowrap">
                          <div className="flex items-center gap-2">
                            <FileText className="w-4 h-4 text-blue-600 dark:text-blue-400" />
                            <span className="text-sm font-medium text-gray-900 dark:text-white">{file.fileName}</span>
                          </div>
                        </td>
                        <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-600 dark:text-gray-400">
                          {(file.size / 1024).toFixed(2)} KB
                        </td>
                        <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-600 dark:text-gray-400">
                          {new Date(file.lastModified).toLocaleString()}
                        </td>
                        <td className="px-4 py-3 whitespace-nowrap">
                          <StatusBadge
                            status={file.isDiscovered ? 'Discovered' : 'Manual'}
                            size="sm"
                            showIcon={false}
                          />
                        </td>
                        <td className="px-4 py-3 whitespace-nowrap text-right text-sm font-medium">
                          <div className="flex gap-2 justify-end">
                            <button
                              onClick={() => handleEdit(file)}
                              className="p-2 text-blue-600 dark:text-blue-400 hover:bg-blue-50 dark:hover:bg-blue-900/30 rounded-md transition-colors"
                              title="Edit file"
                            >
                              <Edit className="w-4 h-4" />
                            </button>
                            <button
                              onClick={() => handleDelete(file)}
                              className="p-2 text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/30 rounded-md transition-colors"
                              title="Delete file"
                            >
                              <Trash2 className="w-4 h-4" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Delete Confirmation Dialog */}
      <ConfirmDialog
        isOpen={deleteDialogOpen}
        onClose={() => {
          setDeleteDialogOpen(false);
          setFileToDelete(null);
        }}
        onConfirm={confirmDelete}
        title="Delete Compose File"
        message={
          <>
            Are you sure you want to delete <strong>{fileToDelete?.fileName}</strong>?
            <br />
            This action cannot be undone.
          </>
        }
        confirmText="Delete"
        cancelText="Cancel"
        variant="danger"
        isLoading={deleteMutation.isPending}
      />
    </div>
  );
};
