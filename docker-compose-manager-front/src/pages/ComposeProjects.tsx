import { useState } from 'react';
import { Play, Square, RotateCcw, RefreshCw, Eye, Trash2 } from 'lucide-react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { composeApi } from '../api';
import { LoadingSpinner, ErrorDisplay, ConfirmDialog, StatusBadge } from '../components/common';
import type { ComposeProject } from '../types';

interface ProjectAction {
  project: ComposeProject | null;
  type: 'up' | 'down' | 'restart' | 'stop' | 'start';
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
    refetchInterval: 10000, // Refetch every 10 seconds
  });

  // Up mutation
  const upMutation = useMutation({
    mutationFn: (projectName: string) => composeApi.upProject(projectName, { detach: true }),
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

  const handleAction = (project: ComposeProject, type: ProjectAction['type']) => {
    setCurrentAction({ project, type });
    setActionDialogOpen(true);
  };

  const confirmAction = () => {
    if (!currentAction?.project) return;

    const projectName = currentAction.project.name;

    switch (currentAction.type) {
      case 'up':
        upMutation.mutate(projectName);
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
      up: 'start and build (if needed) all services',
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
    <div>
      {/* Header */}
      <div className="mb-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-gray-900">Compose Projects</h1>
            <p className="text-sm text-gray-600 mt-1">
              Manage your Docker Compose projects and services
            </p>
          </div>
          <button
            onClick={() => refetch()}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors"
          >
            <RefreshCw className="w-4 h-4" />
            Refresh
          </button>
        </div>
      </div>

      {/* Project Count */}
      <div className="mb-4">
        <p className="text-sm text-gray-600">
          {projects.length} {projects.length === 1 ? 'project' : 'projects'} found
        </p>
      </div>

      {/* Projects List */}
      {projects.length === 0 ? (
        <div className="text-center py-12 bg-white rounded-lg border border-gray-200">
          <Square className="w-12 h-12 text-gray-400 mx-auto mb-4" />
          <h3 className="text-lg font-semibold text-gray-900 mb-2">No projects found</h3>
          <p className="text-sm text-gray-600">
            Create compose files and start them to see projects here
          </p>
        </div>
      ) : (
        <div className="space-y-4">
          {projects.map((project) => (
            <div key={project.name} className="bg-white rounded-lg border border-gray-200 overflow-hidden">
              {/* Project Header */}
              <div className="bg-gray-50 px-6 py-4 border-b border-gray-200">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-4">
                    <h3 className="text-lg font-semibold text-gray-900">{project.name}</h3>
                    <StatusBadge status={project.status} />
                  </div>
                  <div className="flex gap-2">
                    {project.status === 'Stopped' || project.status === 'Partial' ? (
                      <button
                        onClick={() => handleAction(project, 'up')}
                        className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-white bg-green-600 rounded-md hover:bg-green-700 transition-colors"
                        title="Start project"
                      >
                        <Play className="w-4 h-4" />
                        Start
                      </button>
                    ) : null}
                    {project.status === 'Running' || project.status === 'Partial' ? (
                      <>
                        <button
                          onClick={() => handleAction(project, 'restart')}
                          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-blue-600 bg-blue-50 rounded-md hover:bg-blue-100 transition-colors"
                          title="Restart project"
                        >
                          <RotateCcw className="w-4 h-4" />
                          Restart
                        </button>
                        <button
                          onClick={() => handleAction(project, 'stop')}
                          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-yellow-600 bg-yellow-50 rounded-md hover:bg-yellow-100 transition-colors"
                          title="Stop project"
                        >
                          <Square className="w-4 h-4" />
                          Stop
                        </button>
                        <button
                          onClick={() => handleAction(project, 'down')}
                          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-red-600 bg-red-50 rounded-md hover:bg-red-100 transition-colors"
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
                      className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-gray-700 bg-gray-100 rounded-md hover:bg-gray-200 transition-colors"
                      title="View logs"
                    >
                      <Eye className="w-4 h-4" />
                      Logs
                    </button>
                  </div>
                </div>
                <div className="mt-2 text-sm text-gray-600">
                  {project.workingDirectory && <span>Directory: {project.workingDirectory}</span>}
                </div>
              </div>

              {/* Services List */}
              {project.services && project.services.length > 0 && (
                <div className="p-6">
                  <h4 className="text-sm font-semibold text-gray-700 mb-3">
                    Services ({project.services.length})
                  </h4>
                  <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                    {project.services.map((service, index) => (
                      <div
                        key={`${service.name}-${index}`}
                        className="p-4 bg-gray-50 rounded-lg border border-gray-200"
                      >
                        <div className="flex items-start justify-between mb-2">
                          <h5 className="font-medium text-gray-900">{service.name}</h5>
                          {service.status && <StatusBadge status={service.status} size="sm" />}
                        </div>
                        {service.image && (
                          <p className="text-xs text-gray-600 mb-1">
                            <span className="font-medium">Image:</span> {service.image}
                          </p>
                        )}
                        {service.ports && service.ports.length > 0 && (
                          <p className="text-xs text-gray-600">
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
