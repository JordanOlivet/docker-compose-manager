import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { AxiosError } from 'axios';
import { containersApi } from '../api/containers';
import { EntityState, type Container } from '../types';
import { useToast } from '../hooks/useToast';
import { LoadingSpinner } from '../components/common/LoadingSpinner';
import { ErrorDisplay } from '../components/common/ErrorDisplay';
import { Play, Square, RotateCw, Trash2, Container as ContainerIcon } from 'lucide-react';
import { type ApiErrorResponse } from '../utils/errorFormatter';

export default function Containers() {
  const [showAllContainers, setShowAllContainers] = useState(true);
  const queryClient = useQueryClient();
  const toast = useToast();

  const { data: containers, isLoading, error } = useQuery({
    queryKey: ['containers', showAllContainers],
    queryFn: () => containersApi.list(showAllContainers),
    refetchInterval: 5000, // Refresh every 5 seconds
  });

  const startMutation = useMutation({
    mutationFn: ({ id }: { id: string; name: string }) => containersApi.start(id),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['containers'] });
      toast.success(`Container "${variables.name}" started successfully`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(error.response?.data?.message || `Failed to start container "${variables.name}"`);
    },
  });

  const stopMutation = useMutation({
    mutationFn: ({ id }: { id: string; name: string }) => containersApi.stop(id),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['containers'] });
      toast.success(`Container "${variables.name}" stopped successfully`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(error.response?.data?.message || `Failed to stop container "${variables.name}"`);
    },
  });

  const restartMutation = useMutation({
    mutationFn: ({ id }: { id: string; name: string }) => containersApi.restart(id),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['containers'] });
      toast.success(`Container "${variables.name}" restarted successfully`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(error.response?.data?.message || `Failed to restart container "${variables.name}"`);
    },
  });

  const removeMutation = useMutation({
    mutationFn: ({ id, force }: { id: string; name: string; force: boolean }) => containersApi.remove(id, force),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['containers'] });
      toast.success(`Container "${variables.name}" removed successfully`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(error.response?.data?.message || `Failed to remove container "${variables.name}"`);
    },
  });

  const getStateColor = (state: EntityState) => {
    switch (state) {
      case EntityState.Running:
        return 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200';
      case EntityState.Exited:
        return 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200';
      // case EntityState.Paused:
      //   return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200';
      // case EntityState.Created:
      //   return 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200';
      default:
        return 'bg-gray-100 text-gray-800 dark:bg-gray-900 dark:text-gray-200';
    }
  };

  const handleRemove = (container: Container) => {
    const isRunning = container.state == EntityState.Running;
    const message = isRunning
      ? `Container ${container.name} is running. Force remove it?`
      : `Remove container ${container.name}?`;

    if (confirm(message)) {
      removeMutation.mutate({ id: container.id, name: container.name, force: isRunning });
    }
  };

  if (isLoading) return <LoadingSpinner />;
  if (error) return <ErrorDisplay message="Failed to load containers" />;

  return (
    <div className="space-y-8">
      {/* Page Header */}
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-4xl font-bold text-gray-900 dark:text-white mb-3">Containers</h1>
          <p className="text-lg text-gray-600 dark:text-gray-400">
            Manage your Docker containers
          </p>
        </div>
        <label className="flex items-center gap-3 px-4 py-3 bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-200 dark:border-gray-700 cursor-pointer hover:shadow-md transition-all">
          <input
            type="checkbox"
            checked={showAllContainers}
            onChange={(e) => setShowAllContainers(e.target.checked)}
            className="w-4 h-4 rounded text-blue-600 focus:ring-2 focus:ring-blue-500"
          />
          <span className="text-sm font-medium text-gray-700 dark:text-gray-300">Show all containers</span>
        </label>
      </div>

      {!containers || containers.length === 0 ? (
        <div className="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl shadow-lg border border-gray-100 dark:border-gray-700 p-12 text-center">
          <div className="inline-flex items-center justify-center w-20 h-20 rounded-full bg-gray-100 dark:bg-gray-800 mb-4">
            <ContainerIcon className="w-10 h-10 text-gray-400" />
          </div>
          <p className="text-lg text-gray-600 dark:text-gray-400">No containers found</p>
        </div>
      ) : (
        <div className="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl shadow-lg border border-gray-100 dark:border-gray-700 overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead className="bg-white/50 dark:bg-gray-800/50 border-b border-gray-200 dark:border-gray-700">
                <tr>
                  <th className="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                    Name
                  </th>
                  <th className="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                    Image
                  </th>
                  <th className="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                    State
                  </th>
                  <th className="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                    Status
                  </th>
                  <th className="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                    Actions
                  </th>
                </tr>
              </thead>
              
              <tbody className="divide-y divide-gray-100 dark:divide-gray-700">
                {containers.map((container: Container) => (
                  <tr key={container.id} className="hover:bg-white dark:hover:bg-gray-800 transition-all">
                    <td className="px-8 py-5 whitespace-nowrap">
                      <div className="text-sm font-medium text-gray-900 dark:text-white">
                        {container.name}
                      </div>
                      <div className="text-xs text-gray-500 dark:text-gray-400 font-mono">
                        {container.id.substring(0, 12)}
                      </div>
                    </td>
                    <td className="px-8 py-5">
                      <div className="text-sm text-gray-900 dark:text-gray-300">
                        {container.image}
                      </div>
                    </td>
                    <td className="px-8 py-5 whitespace-nowrap">
                      <span className={`px-3 py-1.5 inline-flex text-xs leading-5 font-semibold rounded-full ${getStateColor(container.state)}`}>
                        {container.state}
                      </span>
                    </td>
                    <td className="px-8 py-5">
                      <div className="text-sm text-gray-500 dark:text-gray-400">
                        {container.status}
                      </div>
                    </td>
                    <td className="px-8 py-5 whitespace-nowrap text-sm">
                      <div className="flex items-center gap-3">
                        {container.state == EntityState.Running ?
                        (
                          <>
                            <button
                              onClick={() => stopMutation.mutate({ id: container.id, name: container.name })}
                              className="p-1.5 text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-300 hover:bg-yellow-50 dark:hover:bg-yellow-900/20 rounded transition-colors"
                              title="Stop"
                            >
                              <Square className="w-4 h-4" />
                            </button>
                            <button
                              onClick={() => restartMutation.mutate({ id: container.id, name: container.name })}
                              className="p-1.5 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors"
                              title="Restart"
                            >
                              <RotateCw className="w-4 h-4" />
                            </button>
                          </>
                        )
                        :
                        (
                          <button
                            onClick={() => startMutation.mutate({ id: container.id, name: container.name })}
                            className="p-1.5 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors"
                            title="Start"
                          >
                            <Play className="w-4 h-4" />
                          </button>
                        )}
                        <button
                          onClick={() => handleRemove(container)}
                          className="p-1.5 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition-colors"
                          title="Remove"
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
      )}
    </div>
  );
}
