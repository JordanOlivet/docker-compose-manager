import { useParams, useNavigate } from "react-router-dom";
import {
  Play,
  Square,
  Trash2,
  Zap,
  RotateCw,
  ArrowLeft,
  RefreshCw,
} from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { composeApi } from "../api";
import { LoadingSpinner, ErrorDisplay, StateBadge } from "../components/common";
import { useTranslation } from 'react-i18next';
import {
  EntityState,
  type ComposeProject,
  type ComposeService,
} from "../types";
import { useSignalROperations } from "../hooks/useSignalROperations";
import { useComposeMutations } from "../hooks/useComposeMutations";
import { useContainerMutations } from "../hooks/useContainerMutations";
import { ProjectStatsCard } from "../components/compose/ProjectStatsCard";
import { ProjectInfoSection } from "../components/compose/ProjectInfoSection";
import { ComposeLogs } from "../components/compose/ComposeLogs";

function ComposeDetails() {
  const { t } = useTranslation();
  const { projectName } = useParams<{ projectName: string }>();
  const navigate = useNavigate();

  // Setup SignalR for automatic updates
  useSignalROperations({
    queryKeys: 'composeProjectDetails',
    operationTypeFilter: 'compose',
    showErrorToasts: true,
    showSuccessToasts: true, // Show toast when operation completes
  });

  // Setup compose and container mutations
  const { upProject, downProject, restartProject, stopProject } =
    useComposeMutations();
  const { startContainer, stopContainer, restartContainer, removeContainer } =
    useContainerMutations(['composeProjectDetails']);

  // Fetch compose project
  const {
    data: project,
    isLoading,
    error,
    refetch,
  } = useQuery({
    queryKey: ["composeProjectDetails", projectName],
    queryFn: () => {
      if (!projectName) throw new Error("Project name is required");
      return composeApi.getProjectDetails(projectName);
    },
    enabled: !!projectName,
    // Removed automatic polling - we use SignalR for real-time updates now
    refetchInterval: false,
  });

  const getStateColor = (state: EntityState) => {
    switch (state) {
      case EntityState.Running:
      case EntityState.Restarting:
        return "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200";
      case EntityState.Exited:
      case EntityState.Down:
      case EntityState.Stopped:
        return "bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200";
      // case EntityState.Paused:
      //   return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200';
      // case EntityState.Created:
      //   return 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200';
      case EntityState.Degraded:
      default:
        return "bg-gray-100 text-gray-800 dark:bg-gray-900 dark:text-gray-200";
    }
  };

  const handleRemoveComposeProject = (project: ComposeProject) => {
    const isRunning = project.state == EntityState.Running;
    const message = isRunning
      ? t('compose.confirmRemoveRunningWithName').replace('{name}', project.name)
      : t('compose.confirmRemoveWithName').replace('{name}', project.name);

    if (confirm(message)) {
      downProject(project.name);
    }
  };

  const handleRemove = (service: ComposeService) => {
    const isRunning = service.state == EntityState.Running;
    const message = isRunning
      ? t('containers.confirmRemoveRunningWithName').replace('{name}', service.name)
      : t('containers.confirmRemoveWithName').replace('{name}', service.name);

    if (confirm(message)) {
      removeContainer(service.id, service.name, isRunning);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-96">
        <LoadingSpinner size="lg" text={t('compose.loadingDetails')} />
      </div>
    );
  }

  if (error) {
    return (
      <ErrorDisplay
        title={t('compose.failedToLoadProject')}
        message={
          error instanceof Error
            ? error.message
            : "An unexpected error occurred"
        }
        onRetry={() => refetch()}
      />
    );
  }

  if (!project) {
    return (
      <ErrorDisplay
        title={t('compose.projectNotFound')}
        message={t('compose.projectNotFoundMessage')}
        onRetry={() => navigate("/compose/projects")}
      />
    );
  }

  return (
    <div className="space-y-8">
      {/* Page Header */}
      <div className="mb-8">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-4">
            <button
              onClick={() => navigate("/compose/projects")}
              className="p-2 text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg transition-colors"
              title="Back to projects"
            >
              <ArrowLeft className="w-5 h-5" />
            </button>
            <div>
              <h1 className="text-4xl font-bold text-gray-900 dark:text-white mb-3">
                {project.name}
              </h1>
              <p className="text-lg text-gray-600 dark:text-gray-400">{t('compose.projectDetails')}</p>
            </div>
          </div>
          <button
            onClick={() => refetch()}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
              title={t('compose.backToProjects')}
            >
            <RefreshCw className="w-4 h-4" />
            {t('common.refresh')}
          </button>
        </div>
      </div>

      {/* Row 1: Services List */}
      <div className="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl border border-gray-200 dark:border-gray-700 overflow-visible shadow-lg hover:shadow-2xl transition-all duration-300">
        {/* Project Header */}
        <div className="bg-white dark:bg-gray-800 px-6 py-4 rounded-t-2xl border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-4">
              <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
                {project.name}
              </h3>
              <StateBadge
                className={`${getStateColor(project.state)}`}
                status={project.state}
              />
            </div>
            <div className="flex gap-2">
              {project.state === EntityState.Down ||
              project.state === EntityState.Stopped ||
              project.state === EntityState.Exited ||
              project.state === EntityState.Degraded ||
              project.state === EntityState.Created ? (
                <>
                  <button
                    onClick={() =>
                      upProject(project.name, { detach: true })
                    }
                    className="p-1.5 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer"
                    title={t('containers.start')}
                  >
                    <Play className="w-4 h-4" />
                  </button>
                  <button
                    onClick={() =>
                      upProject(project.name, { detach: true, forceRecreate: true })
                    }
                    className="p-1.5 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer"
                    title={t('compose.forceRecreate')}
                  >
                    <Zap className="w-4 h-4" />
                  </button>
                </>
              ) : null}
              {project.state === EntityState.Running ||
              project.state === EntityState.Degraded ? (
                <>
                  <button
                    onClick={() => restartProject(project.name)}
                    className="p-1.5 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors cursor-pointer"
                    title={t('containers.restart')}
                  >
                    <RotateCw className="w-4 h-4" />
                  </button>
                  <button
                    onClick={() => stopProject(project.name)}
                    className="p-1.5 text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-300 hover:bg-yellow-50 dark:hover:bg-yellow-900/20 rounded transition-colors cursor-pointer"
                    title={t('containers.stop')}
                  >
                    <Square className="w-4 h-4" />
                  </button>
                </>
              ) : null}
              {project.state !== EntityState.Down ? (
                <>
                  <button
                    onClick={() => handleRemoveComposeProject(project)}
                    className="p-1.5 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition-colors cursor-pointer"
                    title={t('containers.remove')}
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                </>
              ) : null}
            </div>
          </div>
          <div className="mt-2 text-sm text-gray-600 dark:text-gray-400">
            {project.path && <span>{t('compose.directoryPath')}: {project.path}</span>}
          </div>
        </div>

        {/* Services Table */}
        {!project.services || project.services.length === 0 ? (
          <div />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead className="bg-white/50 dark:bg-gray-800/50 border-b border-gray-200 dark:border-gray-700">
                <tr>
                  <th className="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                    {t('containers.name')}
                  </th>
                  <th className="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                    {t('containers.image')}
                  </th>
                  <th className="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                    {t('containers.state')}
                  </th>
                  <th className="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                    {t('containers.status')}
                  </th>
                  <th className="px-8 py-5 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                    {t('users.actions')}
                  </th>
                </tr>
              </thead>

              <tbody className="divide-y divide-gray-100 dark:divide-gray-700">
                {project.services.map((service: ComposeService) => (
                  <tr
                    key={service.id}
                    className="hover:bg-white dark:hover:bg-gray-800 transition-all"
                  >
                    <td className="px-8 py-5 whitespace-nowrap">
                      <div className="text-sm font-medium text-gray-900 dark:text-white">
                        {service.name}
                      </div>
                      <div className="text-xs text-gray-500 dark:text-gray-400 font-mono">
                        {service.id}
                      </div>
                    </td>
                    <td className="px-8 py-5">
                      <div className="text-sm text-gray-900 dark:text-gray-300">
                        {service.image}
                      </div>
                    </td>
                    <td className="px-8 py-5 whitespace-nowrap">
                      <StateBadge
                        className={`${getStateColor(service.state)}`}
                        status={service.state}
                        size="sm"
                      />
                    </td>
                    <td className="px-8 py-5">
                      <div className="text-sm text-gray-500 dark:text-gray-400">
                        {service.status}
                      </div>
                    </td>
                    <td className="px-8 py-5 whitespace-nowrap text-sm">
                      <div className="flex items-center gap-3">
                        {service.state == EntityState.Running ? (
                          <>
                           <button
                              onClick={() =>
                                restartContainer(service.id, service.name)
                              }
                              className="p-1.5 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors cursor-pointer"
                              title={t('containers.restart')}
                            >
                              <RotateCw className="w-4 h-4" />
                            </button>
                            <button
                              onClick={() =>
                                stopContainer(service.id, service.name)
                              }
                              className="p-1.5 text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-300 hover:bg-yellow-50 dark:hover:bg-yellow-900/20 rounded transition-colors cursor-pointer"
                              title={t('containers.stop')}
                            >
                              <Square className="w-4 h-4" />
                            </button>
                          </>
                        ) : (
                          <button
                            onClick={() =>
                              startContainer(service.id, service.name)
                            }
                            className="p-1.5 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer"
                            title={t('containers.start')}
                          >
                            <Play className="w-4 h-4" />
                          </button>
                        )}
                        <button
                          onClick={() => handleRemove(service)}
                          className="p-1.5 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition-colors cursor-pointer"
                          title={t('containers.remove')}
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
        )}
      </div>

      {/* Row 2: Compose File Details (left) and Stats (right) */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Left: Compose File Details */}
        <div>
          <ProjectInfoSection
            projectName={project.name}
            projectPath={project.composeFiles && project.composeFiles.length > 0 ? project.composeFiles[0] : undefined}
          />
        </div>

        {/* Right: Stats Charts */}
        <div>
          <ProjectStatsCard services={project.services} />
        </div>
      </div>

      {/* Row 3: Logs (full width, resizable) */}
      <div className="w-full">
        {project.path && (
          <div className="h-[500px] resize-y overflow-auto min-h-[500px] max-h-[1000px]">
            <ComposeLogs projectPath={project.path} projectName={project.name} />
          </div>
        )}
      </div>
    </div>
  );
}

export default ComposeDetails;
