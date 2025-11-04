import { useState, useEffect } from 'react';
import { Play, Square, RotateCcw, RefreshCw, Eye, Trash2, Hammer, Zap } from 'lucide-react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { composeApi } from '../api';
import { LoadingSpinner, ErrorDisplay, ConfirmDialog, StatusBadge, SplitButton } from '../components/common';
import { ProjectStatus, type ComposeProject } from '../types';
import { signalRService } from '../services/signalRService';

interface ProjectAction {
  project: ComposeProject | null;
  type: 'up' | 'down' | 'restart' | 'stop' | 'start' | 'up-build' | 'up-recreate' | 'up-build-recreate';
}

export const ComposeProjects = () => {
  const queryClient = useQueryClient();
  const [actionDialogOpen, setActionDialogOpen] = useState(false);
  const [currentAction, setCurrentAction] = useState<ProjectAction | null>(null);

  // Fetch projects
  const {
    data: projects = [],
    isLoading,
    error,
    refetch,
  } = useQuery({
    queryKey: ['composeProjects'],
    queryFn: composeApi.listProjects,
    // Removed automatic polling - we use SignalR for real-time updates now
    refetchInterval: false,
  });

  // Up mutation
  const upMutation = useMutation({
    mutationFn: ({ projectName, options }: { projectName: string; options?: { build?: boolean; detach?: boolean; forceRecreate?: boolean } }) =>
      composeApi.upProject(projectName, options || { detach: true }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['composeProjects'] });
      setActionDialogOpen(false);
      setCurrentAction(null);
    },
  });

  // Down mutation
  const downMutation = useMutation({
    mutationFn: (projectName: string) => composeApi.downProject(projectName, {}),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['composeProjects'] });
      setActionDialogOpen(false);
      setCurrentAction(null);
    },
  });

  // Restart mutation
  const restartMutation = useMutation({
    mutationFn: (projectName: string) => composeApi.restartProject(projectName),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['composeProjects'] });
      setActionDialogOpen(false);
      setCurrentAction(null);
    },
  });

  // Stop mutation
  const stopMutation = useMutation({
    mutationFn: (projectName: string) => composeApi.stopProject(projectName),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['composeProjects'] });
      setActionDialogOpen(false);
      setCurrentAction(null);
    },
  });

  // Start mutation
  const startMutation = useMutation({
    mutationFn: (projectName: string) => composeApi.startProject(projectName),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['composeProjects'] });
      setActionDialogOpen(false);
      setCurrentAction(null);
    },
  });

  // Setup SignalR connection for real-time updates
  useEffect(() => {
    const connectAndListen = async () => {
      try {
        console.log('Connecting to SignalR operations hub...');
        // Connect to the operations hub
        await signalRService.connect();
        console.log('Connected to SignalR operations hub successfully');

        // Listen for operation updates
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const handleOperationUpdate = (update: any) => {
          console.log('Operation update received:', update);
          console.log('Update status:', update.status, 'Type:', update.type);

          // Only react to completed or failed compose operations
          const statusMatch = update.status === 'completed' || update.status === 'failed';
          const typeMatch = update.type && update.type.toLowerCase().includes('compose');

          console.log('Status match:', statusMatch, 'Type match:', typeMatch);

          if (statusMatch && typeMatch) {
            console.log('✅ Refreshing compose projects list...');
            // Immediately refetch projects to show the new state
            queryClient.invalidateQueries({ queryKey: ['composeProjects'] });
          } else {
            console.log('❌ Not refreshing - conditions not met');
          }
        };

        signalRService.onOperationUpdate(handleOperationUpdate);

        // Cleanup on unmount
        return () => {
          console.log('Cleaning up SignalR connection');
          signalRService.offOperationUpdate(handleOperationUpdate);
        };
      } catch (error) {
        console.error('Failed to connect to SignalR:', error);
      }
    };

    connectAndListen();
  }, [queryClient]);

  const handleAction = (project: ComposeProject, type: ProjectAction['type']) => {
    setCurrentAction({ project, type });
    setActionDialogOpen(true);
  };

  const confirmAction = () => {
    if (!currentAction?.project) return;

    const projectName = currentAction.project.name;

    switch (currentAction.type) {
      case 'up':
        upMutation.mutate({ projectName, options: { detach: true } });
        break;
      case 'up-build':
        upMutation.mutate({ projectName, options: { detach: true, build: true } });
        break;
      case 'up-recreate':
        upMutation.mutate({ projectName, options: { detach: true, forceRecreate: true } });
        break;
      case 'up-build-recreate':
        upMutation.mutate({ projectName, options: { detach: true, build: true, forceRecreate: true } });
        break;
      case 'down':
        downMutation.mutate(projectName);
        break;
      case 'restart':
        restartMutation.mutate(projectName);
        break;
      case 'stop':
        stopMutation.mutate(projectName);
        break;
      case 'start':
        startMutation.mutate(projectName);
        break;
    }
  };

  const getActionMessage = (action: ProjectAction | null) => {
    if (!action) return '';

    const actionMessages = {
      up: 'start all services',
      'up-build': 'start and build all services',
      'up-recreate': 'recreate and start all services (force recreate)',
      'up-build-recreate': 'rebuild, recreate and start all services (build + force recreate)',
      down: 'stop and remove all containers, networks, and optionally volumes',
      restart: 'restart all services',
      stop: 'stop all services without removing them',
      start: 'start all stopped services',
    };

    return (
      <>
        Are you sure you want to <strong>{actionMessages[action.type]}</strong> for project{' '}
        <strong>{action.project?.name}</strong>?
      </>
    );
  };

  const isActionPending =
    upMutation.isPending ||
    downMutation.isPending ||
    restartMutation.isPending ||
    stopMutation.isPending ||
    startMutation.isPending;

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-96">
        <LoadingSpinner size="lg" text="Loading compose projects..." />
      </div>
    );
  }

  if (error) {
    return (
      <ErrorDisplay
        title="Failed to load compose projects"
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
            <h1 className="text-4xl font-bold text-gray-900 dark:text-white mb-3">Compose Projects</h1>
            <p className="text-lg text-gray-600 dark:text-gray-400">
              Manage your Docker Compose projects and services
            </p>
          </div>
          <button
            onClick={() => refetch()}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
          >
            <RefreshCw className="w-4 h-4" />
            Refresh
          </button>
        </div>
      </div>

      {/* Project Count */}
      <div className="mb-4">
        <p className="text-sm text-gray-600 dark:text-gray-400">
          {projects.length} {projects.length === 1 ? 'project' : 'projects'} found
        </p>
      </div>

      {/* Projects List */}
      {projects.length === 0 ? (
        <div className="text-center py-12 bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg">
          <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-gray-100 dark:bg-gray-700 mb-3">
            <Square className="w-8 h-8 text-gray-400" />
          </div>
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">No projects found</h3>
          <p className="text-sm text-gray-600 dark:text-gray-400">
            Create compose files and start them to see projects here
          </p>
        </div>
      ) : (
        <div className="space-y-4">
          {projects.map((project) => (
            <div key={project.name} className="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl border border-gray-200 dark:border-gray-700 overflow-visible shadow-lg hover:shadow-2xl transition-all duration-300">
              {/* Project Header */}
              <div className="bg-white dark:bg-gray-800 px-6 py-4 rounded-2xl border-gray-200 dark:border-gray-700">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-4">
                    <h3 className="text-lg font-semibold text-gray-900 dark:text-white">{project.name}</h3>
                    <StatusBadge status={project.status} />
                  </div>
                  <div className="flex gap-2">
                    {project.status === ProjectStatus.Down || project.status === ProjectStatus.Stopped || project.status === ProjectStatus.Partial ? (
                      <SplitButton
                        label="Start"
                        icon={<Play className="w-4 h-4" />}
                        onClick={() => handleAction(project, 'up')}
                        variant="primary"
                        menuItems={[
                          {
                            label: 'Start + Build',
                            icon: <Hammer className="w-4 h-4" />,
                            onClick: () => handleAction(project, 'up-build'),
                          },
                          {
                            label: 'Force Recreate',
                            icon: <Zap className="w-4 h-4" />,
                            onClick: () => handleAction(project, 'up-recreate'),
                          },
                          {
                            label: 'Build + Force Recreate',
                            icon: <RefreshCw className="w-4 h-4" />,
                            onClick: () => handleAction(project, 'up-build-recreate'),
                          },
                        ]}
                      />
                    ) : null}
                    {project.status === ProjectStatus.Running || project.status === ProjectStatus.Partial ? (
                      <>
                        <button
                          onClick={() => handleAction(project, 'restart')}
                          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-900/30 rounded-md hover:bg-blue-100 dark:hover:bg-blue-900/50 transition-colors"
                          title="Restart project"
                        >
                          <RotateCcw className="w-4 h-4" />
                          Restart
                        </button>
                        <button
                          onClick={() => handleAction(project, 'stop')}
                          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-yellow-600 dark:text-yellow-400 bg-yellow-50 dark:bg-yellow-900/30 rounded-md hover:bg-yellow-100 dark:hover:bg-yellow-900/50 transition-colors"
                          title="Stop project"
                        >
                          <Square className="w-4 h-4" />
                          Stop
                        </button>
                        <button
                          onClick={() => handleAction(project, 'down')}
                          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-red-600 dark:text-red-400 bg-red-50 dark:bg-red-900/30 rounded-md hover:bg-red-100 dark:hover:bg-red-900/50 transition-colors"
                          title="Down project"
                        >
                          <Trash2 className="w-4 h-4" />
                          Down
                        </button>
                      </>
                    ) : null}
                    <button
                      onClick={() => {
                        // TODO: Implement logs viewer
                        alert('Logs viewer coming soon');
                      }}
                      className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-gray-700 dark:text-gray-300 bg-gray-100 dark:bg-gray-700 rounded-md hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors"
                      title="View logs"
                    >
                      <Eye className="w-4 h-4" />
                      Logs
                    </button>
                  </div>
                </div>
                <div className="mt-2 text-sm text-gray-600 dark:text-gray-400">
                  {project.workingDirectory && <span>Directory: {project.workingDirectory}</span>}
                </div>
              </div>

              {/* Services List */}
              {project.services && project.services.length > 0 && (
                <div className="p-6">
                  <h4 className="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-3">
                    Services ({project.services.length})
                  </h4>
                  <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                    {project.services.map((service, index) => (
                      <div
                        key={`${service.name}-${index}`}
                        className="p-4 bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 hover:shadow-md transition-all duration-200"
                      >
                        <div className="flex items-start justify-between mb-2">
                          <h5 className="font-medium text-gray-900 dark:text-white">{service.name}</h5>
                          {service.status && <StatusBadge status={service.status} size="sm" />}
                        </div>
                        {service.image && (
                          <p className="text-xs text-gray-600 dark:text-gray-400 mb-1">
                            <span className="font-medium">Image:</span> {service.image}
                          </p>
                        )}
                        {service.ports && service.ports.length > 0 && (
                          <p className="text-xs text-gray-600 dark:text-gray-400">
                            <span className="font-medium">Ports:</span> {service.ports.join(', ')}
                          </p>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {/* Action Confirmation Dialog */}
      <ConfirmDialog
        isOpen={actionDialogOpen}
        onClose={() => {
          setActionDialogOpen(false);
          setCurrentAction(null);
        }}
        onConfirm={confirmAction}
        title={`${currentAction?.type ? currentAction.type.charAt(0).toUpperCase() + currentAction.type.slice(1) : 'Confirm'} Project`}
        message={getActionMessage(currentAction)}
        confirmText={currentAction?.type ? currentAction.type.charAt(0).toUpperCase() + currentAction.type.slice(1) : 'Confirm'}
        cancelText="Cancel"
        variant={currentAction?.type === 'down' ? 'danger' : 'warning'}
        isLoading={isActionPending}
      />
    </div>
  );
};
