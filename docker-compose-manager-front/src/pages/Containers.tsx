import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { AxiosError } from 'axios';
import { containersApi } from '../api/containers';
import { EntityState, type Container, type OperationUpdateEvent } from '../types';
import { useToast } from '../hooks/useToast';
import { LoadingSpinner } from '../components/common/LoadingSpinner';
import { ErrorDisplay } from '../components/common/ErrorDisplay';
import { Play, Square, RotateCw, Trash2, Container as ContainerIcon } from 'lucide-react';
import { type ApiErrorResponse } from '../utils/errorFormatter';
import { signalRService } from "../services/signalRService";

export default function Containers() {
  const navigate = useNavigate();
  const [showAllContainers, setShowAllContainers] = useState(true);
  // Sorting state: column key & direction
  const [sortKey, setSortKey] = useState<'name' | 'image' | 'state' | 'status'>('name');
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('asc');
  const queryClient = useQueryClient();
  const toast = useToast();

  const { data: containers, isLoading, error } = useQuery({
    queryKey: ['containers', showAllContainers],
    queryFn: () => containersApi.list(showAllContainers),
    refetchInterval: false, // SignalR handle refreshes
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

   // Setup SignalR connection for real-time updates
    useEffect(() => {
      const connectAndListen = async () => {
        try {
          // console.log("Connecting to SignalR operations hub...");
          // Connect to the operations hub
          await signalRService.connect();
          // console.log("Connected to SignalR operations hub successfully");
  
          // Listen for operation updates
          const handleOperationUpdate = (update: OperationUpdateEvent) => {
            // console.log("Operation update received:", update);
            // console.log("Update status:", update.status, "Type:", update.type);
  
            // Only react to completed or failed compose operations
            const statusMatch =
              update.status === "completed" || update.status === "failed";
            const typeMatch =
              update.type && update.type.toLowerCase().includes("container");
  
            // console.log("Status match:", statusMatch, "Type match:", typeMatch);
  
            if (statusMatch && typeMatch) {
              // console.log("✅ Refreshing compose projects list...");
              // Immediately refetch projects to show the new state
              queryClient.invalidateQueries({ queryKey: ["containers"] });
            } else {
              // console.log("❌ Not refreshing - conditions not met");
            }
            if (update.errorMessage) {
              toast.error(`An error happend : "${update.errorMessage}"`);
            }
          };
  
          signalRService.onOperationUpdate(handleOperationUpdate);
  
          // Cleanup on unmount
          return () => {
            console.log("Cleaning up SignalR connection");
            signalRService.offOperationUpdate(handleOperationUpdate);
          };
        } catch (error) {
          console.error("Failed to connect to SignalR:", error);
        }
      };
  
      connectAndListen();
    }, [queryClient, toast]);

  // Memoized sorted containers (must be declared before any early return for hooks order)
  const sortedContainers = useMemo(() => {
    if (!containers) return [];
    const arr = [...containers];
    arr.sort((a: Container, b: Container) => {
      const getVal = (c: Container) => {
        switch (sortKey) {
          case 'name':
            return c.name.startsWith('/') ? c.name.slice(1) : c.name;
          case 'image':
            return c.image || '';
          case 'state':
            return c.state || '';
          case 'status':
            return c.status || '';
          default:
            return '';
        }
      };
      const va = getVal(a)?.toString().toLowerCase();
      const vb = getVal(b)?.toString().toLowerCase();
      if (va < vb) return sortDir === 'asc' ? -1 : 1;
      if (va > vb) return sortDir === 'asc' ? 1 : -1;
      return 0;
    });
    return arr;
  }, [containers, sortKey, sortDir]);

  if (isLoading) return <LoadingSpinner />;
  if (error) return <ErrorDisplay message="Failed to load containers" />;

  const toggleSort = (key: 'name' | 'image' | 'state' | 'status') => {
    if (sortKey === key) {
      setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortKey(key);
      setSortDir('asc');
    }
  };

  const renderSortIndicator = (key: typeof sortKey) => {
    if (sortKey !== key) return null;
    return (
      <span className="inline-block ml-1">
        {sortDir === 'asc' ? '↑' : '↓'}
      </span>
    );
  };

  return (
    <div className="space-y-4">
      {/* Page Header */}
      <div className="mb-2">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-1">Containers</h1>
            <p className="text-base text-gray-600 dark:text-gray-400">
              Manage your Docker containers
            </p>
          </div>
          <label className="flex items-center gap-2 px-3 py-1 text-xs font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors cursor-pointer">
            <input
              type="checkbox"
              checked={showAllContainers}
              onChange={(e) => setShowAllContainers(e.target.checked)}
              className="w-4 h-4 rounded text-blue-600 focus:ring-2 focus:ring-blue-500"
            />
            <span>Show all containers</span>
          </label>
        </div>
      </div>

      {!containers || containers.length === 0 ? (
        <div className="text-center py-12 bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg">
          <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-gray-100 dark:bg-gray-700 mb-3">
            <ContainerIcon className="w-8 h-8 text-gray-400" />
          </div>
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">
            No containers found
          </h3>
          <p className="text-sm text-gray-600 dark:text-gray-400">
            Start containers to see them here
          </p>
        </div>
      ) : (
        <div className="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-xl border border-gray-200 dark:border-gray-700 overflow-visible shadow hover:shadow-lg transition-all duration-300">
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead className="bg-white/50 dark:bg-gray-800/50 border-b border-gray-200 dark:border-gray-700">
                <tr>
                  <th
                    onClick={() => toggleSort('name')}
                    aria-sort={sortKey === 'name' ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none'}
                    className="px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider cursor-pointer select-none"
                  >
                    Name {renderSortIndicator('name')}
                  </th>
                  <th
                    onClick={() => toggleSort('image')}
                    aria-sort={sortKey === 'image' ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none'}
                    className="px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider cursor-pointer select-none"
                  >
                    Image {renderSortIndicator('image')}
                  </th>
                  <th
                    onClick={() => toggleSort('state')}
                    aria-sort={sortKey === 'state' ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none'}
                    className="px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider cursor-pointer select-none"
                  >
                    State {renderSortIndicator('state')}
                  </th>
                  <th
                    onClick={() => toggleSort('status')}
                    aria-sort={sortKey === 'status' ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none'}
                    className="px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider cursor-pointer select-none"
                  >
                    Status {renderSortIndicator('status')}
                  </th>
                  <th className="px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                    Actions
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 dark:divide-gray-700">
                {sortedContainers.map((container: Container) => (
                  <tr key={container.id} className="hover:bg-white dark:hover:bg-gray-800 transition-all">
                    <td className="px-4 py-2 whitespace-nowrap">
                      <button
                        className="text-xs font-medium text-blue-600 dark:text-blue-400 hover:underline focus:outline-none"
                        onClick={() => navigate(`/containers/${container.id}`)}
                        title="Voir les détails du container"
                      >
                        {container.name.startsWith('/') ? container.name.slice(1) : container.name}
                      </button>
                      <div className="text-[10px] text-gray-500 dark:text-gray-400 font-mono">
                        {container.id.substring(0, 12)}
                      </div>
                    </td>
                    <td className="px-4 py-2">
                      <div className="text-xs text-gray-900 dark:text-gray-300">
                        {container.image}
                      </div>
                    </td>
                    <td className="px-4 py-2 whitespace-nowrap">
                      <span className={`px-2 py-0.5 inline-flex text-xs leading-5 font-semibold rounded-full ${getStateColor(container.state)}`}>
                        {container.state}
                      </span>
                    </td>
                    <td className="px-4 py-2">
                      <div className="text-xs text-gray-500 dark:text-gray-400">
                        {container.status}
                      </div>
                    </td>
                    <td className="px-4 py-2 whitespace-nowrap text-xs">
                      <div className="flex items-center gap-1">
                        {container.state == EntityState.Running ?
                        (
                          <>
                            <button
                              onClick={() => stopMutation.mutate({ id: container.id, name: container.name })}
                              className="p-1 text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-300 hover:bg-yellow-50 dark:hover:bg-yellow-900/20 rounded transition-colors cursor-pointer text-xs"
                              title="Stop"
                            >
                              <Square className="w-3 h-3" />
                            </button>
                            <button
                              onClick={() => restartMutation.mutate({ id: container.id, name: container.name })}
                              className="p-1 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors cursor-pointer text-xs"
                              title="Restart"
                            >
                              <RotateCw className="w-3 h-3" />
                            </button>
                          </>
                        )
                        :
                        (
                          <button
                            onClick={() => startMutation.mutate({ id: container.id, name: container.name })}
                            className="p-1 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer text-xs"
                            title="Start"
                          >
                            <Play className="w-3 h-3" />
                          </button>
                        )}
                        <button
                          onClick={() => handleRemove(container)}
                          className="p-1 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition-colors cursor-pointer text-xs"
                          title="Remove"
                        >
                          <Trash2 className="w-3 h-3" />
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
