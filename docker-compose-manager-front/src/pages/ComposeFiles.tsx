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
    <div>
      {/* Header */}
      <div className="mb-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-gray-900">Compose Files</h1>
            <p className="text-sm text-gray-600 mt-1">
              Manage your Docker Compose configuration files
            </p>
          </div>
          <div className="flex gap-3">
            <button
              onClick={() => refetch()}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors"
            >
              <RefreshCw className="w-4 h-4" />
              Refresh
            </button>
            <button
              onClick={handleCreate}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700 transition-colors"
            >
              <Plus className="w-4 h-4" />
              New File
            </button>
          </div>
        </div>
      </div>

      {/* File Count */}
      <div className="mb-4">
        <p className="text-sm text-gray-600">
          {files.length} {files.length === 1 ? 'file' : 'files'} found in{' '}
          {Object.keys(filesByDirectory).length}{' '}
          {Object.keys(filesByDirectory).length === 1 ? 'directory' : 'directories'}
        </p>
      </div>

      {/* Files by Directory */}
      {files.length === 0 ? (
        <div className="text-center py-12 bg-white rounded-lg border border-gray-200">
          <FileText className="w-12 h-12 text-gray-400 mx-auto mb-4" />
          <h3 className="text-lg font-semibold text-gray-900 mb-2">No compose files found</h3>
          <p className="text-sm text-gray-600 mb-4">
            Get started by creating a new compose file or configuring compose paths
          </p>
          <button
            onClick={handleCreate}
            className="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700 transition-colors"
          >
            <Plus className="w-4 h-4" />
            Create First File
          </button>
        </div>
      ) : (
        <div className="space-y-6">
          {Object.entries(filesByDirectory).map(([directory, dirFiles]) => (
            <div key={directory} className="bg-white rounded-lg border border-gray-200 overflow-hidden">
              {/* Directory Header */}
              <div className="bg-gray-50 px-4 py-3 border-b border-gray-200">
                <div className="flex items-center gap-2">
                  <Folder className="w-4 h-4 text-gray-600" />
                  <span className="text-sm font-medium text-gray-900">{directory}</span>
                  <span className="text-xs text-gray-500">({dirFiles.length})</span>
                </div>
              </div>

              {/* Files Table */}
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-200">
                  <thead className="bg-gray-50">
                    <tr>
                      <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Name
                      </th>
                      <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Size
                      </th>
                      <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Modified
                      </th>
                      <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Source
                      </th>
                      <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Actions
                      </th>
                    </tr>
                  </thead>
                  <tbody className="bg-white divide-y divide-gray-200">
                    {dirFiles.map((file) => (
                      <tr key={file.id} className="hover:bg-gray-50 transition-colors">
                        <td className="px-4 py-3 whitespace-nowrap">
                          <div className="flex items-center gap-2">
                            <FileText className="w-4 h-4 text-blue-600" />
                            <span className="text-sm font-medium text-gray-900">{file.name}</span>
                          </div>
                        </td>
                        <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-600">
                          {(file.size / 1024).toFixed(2)} KB
                        </td>
                        <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-600">
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
                              className="p-2 text-blue-600 hover:bg-blue-50 rounded-md transition-colors"
                              title="Edit file"
                            >
                              <Edit className="w-4 h-4" />
                            </button>
                            <button
                              onClick={() => handleDelete(file)}
                              className="p-2 text-red-600 hover:bg-red-50 rounded-md transition-colors"
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
            Are you sure you want to delete <strong>{fileToDelete?.name}</strong>?
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
