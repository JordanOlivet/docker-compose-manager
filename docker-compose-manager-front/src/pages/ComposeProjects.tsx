import { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Play,
  Square,
  RefreshCw,
  Trash2,
  Zap,
  RotateCw,
} from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { composeApi } from "../api";
import {
  LoadingSpinner,
  ErrorDisplay,
  ConfirmDialog,
  StateBadge,
} from "../components/common";
import {
  EntityState,
  type ComposeProject,
  type ComposeService,
} from "../types";
import { useSignalROperations } from "../hooks/useSignalROperations";
import { useComposeMutations } from "../hooks/useComposeMutations";
import { useContainerMutations } from "../hooks/useContainerMutations";

interface ProjectAction {
  project: ComposeProject | null;
  type: "up" | "down" | "restart" | "stop" | "up-recreate";
}

export const ComposeProjects = () => {
  const navigate = useNavigate();
  const [actionDialogOpen, setActionDialogOpen] = useState(false);
  const [currentAction, setCurrentAction] = useState<ProjectAction | null>(
    null
  );

  // State for collapsed/expanded projects
  const [openProjects, setOpenProjects] = useState<Record<string, boolean>>({});

  // Setup SignalR for automatic updates
  useSignalROperations({
    queryKeys: 'composeProjects',
    operationTypeFilter: 'compose',
    showErrorToasts: true,
    showSuccessToasts: true, // Show toast when operation completes
  });

  // Setup compose and container mutations
  const { upProject, downProject, restartProject, stopProject, isAnyPending: isComposeActionPending } =
    useComposeMutations();
  const { startContainer, stopContainer, restartContainer, removeContainer } =
    useContainerMutations();

  // Fetch compose projects
  const {
    data: projects = [],
    isLoading,
    error,
    refetch,
  } = useQuery({
    queryKey: ["composeProjects"],
    queryFn: composeApi.listProjects,
    // Removed automatic polling - we use SignalR for real-time updates now
    refetchInterval: false,
  });

  const confirmAction = () => {
    if (!currentAction?.project) return;

    const projectName = currentAction.project.name;

    switch (currentAction.type) {
      case "up":
        upProject(projectName, { detach: true });
        break;
      case "up-recreate":
        upProject(projectName, { detach: true, forceRecreate: true });
        break;
      case "down":
        downProject(projectName);
        break;
      case "restart":
        restartProject(projectName);
        break;
      case "stop":
        stopProject(projectName);
        break;
    }
  };

  const getActionMessage = (action: ProjectAction | null) => {
    if (!action) return "";

    const actionMessages = {
      up: "start all services",
      "up-build": "start and build all services",
      "up-recreate": "recreate and start all services (force recreate)",
      "up-build-recreate":
        "rebuild, recreate and start all services (build + force recreate)",
      down: "stop and remove all containers, networks, and optionally volumes",
      restart: "restart all services",
      stop: "stop all services without removing them",
      start: "start all stopped services",
    };

    return (
      <>
        Are you sure you want to <strong>{actionMessages[action.type]}</strong>{" "}
        for project <strong>{action.project?.name}</strong>?
      </>
    );
  };

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
      ? `Compose project ${project.name} is running. Force remove it?`
      : `Remove compose project ${project.name}?`;

    if (confirm(message)) {
      downProject(project.name);
    }
  };

  const handleRemove = (service: ComposeService) => {
    const isRunning = service.state == EntityState.Running;
    const message = isRunning
      ? `Container ${service.name} is running. Force remove it?`
      : `Remove container ${service.name}?`;

    if (confirm(message)) {
      removeContainer(service.id, service.name, isRunning);
    }
  };

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
        message={
          error instanceof Error
            ? error.message
            : "An unexpected error occurred"
        }
        onRetry={() => refetch()}
      />
    );
  }

  const navigateToProject = (projectName: string) => {
    navigate(`/compose/projects/${encodeURIComponent(projectName)}`);
  };

  // Toggle collapse/expand for a project
  const toggleProjectOpen = (projectName: string) => {
    setOpenProjects((prev) => ({
      ...prev,
      [projectName]: !prev[projectName],
    }));
  };

  return (
    <div className="space-y-4">
      {/* Page Header */}
      <div className="mb-2">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-gray-900 dark:text-white mb-1">
              Compose Projects
            </h1>
            <p className="text-base text-gray-600 dark:text-gray-400">
              Manage your Docker Compose projects and services
            </p>
          </div>
          <button
            onClick={() => refetch()}
            className="flex items-center gap-2 px-3 py-1 text-xs font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
          >
            <RefreshCw className="w-3 h-3" />
            Refresh
          </button>
        </div>
      </div>

      {/* Project Count */}
      <div className="mb-2">
        <p className="text-xs text-gray-600 dark:text-gray-400">
          {projects.length} {projects.length === 1 ? "project" : "projects"} found
        </p>
      </div>

      {/* Projects List */}
      {projects.length === 0 ? (
        <div className="text-center py-12 bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg">
          <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-gray-100 dark:bg-gray-700 mb-3">
            <Square className="w-8 h-8 text-gray-400" />
          </div>
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">
            No projects found
          </h3>
          <p className="text-sm text-gray-600 dark:text-gray-400">
            Create compose files and start them to see projects here
          </p>
        </div>
      ) : (
        <div className="space-y-2">
          {projects.map((project) => {
            // For accessibility, allow keyboard toggle too
            const isOpen = openProjects[project.name] ?? false;
            return (
              <div
                key={project.name}
                className="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-xl border border-gray-200 dark:border-gray-700 overflow-visible shadow hover:shadow-lg transition-all duration-300"
              >
                {/* Project Header (clickable except on interactive elements) */}
                <div
                  className="bg-white dark:bg-gray-800 px-4 py-2 rounded-xl border-gray-200 dark:border-gray-700 select-none cursor-pointer group relative"
                  onClick={(e) => {
                    // Only toggle if not clicking on a button, link, svg, or inside a button
                    const target = e.target as HTMLElement;
                    if (
                      target.closest('button, a, svg, [role="button"], input, select, textarea')
                    ) {
                      return;
                    }
                    toggleProjectOpen(project.name);
                  }}
                  tabIndex={0}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      toggleProjectOpen(project.name);
                    }
                  }}
                  aria-expanded={isOpen}
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      {/* Collapse/expand chevron */}
                      <span
                        className={`inline-block transition-transform duration-150 ease-in-out ${isOpen ? 'rotate-90' : 'rotate-0'} text-gray-900 dark:text-white group-hover:text-blue-600 group-hover:dark:text-blue-400`}
                        aria-hidden="true"
                      >
                        <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M6 12L10 8L6 4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/></svg>
                      </span>
                      <h3
                        className="text-base font-semibold text-gray-900 dark:text-white hover:text-blue-600 dark:hover:text-blue-400 transition-colors cursor-pointer"
                        onClick={(e) => {
                          e.stopPropagation();
                          navigateToProject(project.name);
                        }}
                      >
                        {project.name}
                      </h3>
                      <StateBadge
                        className={`${getStateColor(project.state)} text-xs px-2 py-0.5`}
                        status={project.state}
                        size="sm"
                      />
                    </div>
                    <div className="flex gap-1">
                      {project.state === EntityState.Down ||
                      project.state === EntityState.Stopped ||
                      project.state === EntityState.Exited ||
                      project.state === EntityState.Degraded ||
                      project.state === EntityState.Created ? (
                        <>
                          <button
                            onClick={() => upProject(project.name, { detach: true })}
                            className="p-1 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer text-xs"
                            title="Start"
                          >
                            <Play className="w-4 h-4" />
                          </button>
                          <button
                            onClick={() => upProject(project.name, { detach: true, forceRecreate: true })}
                            className="p-1.5 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer"
                            title="Start and Force Recreate"
                          >
                            <Zap className="w-3 h-3" />
                          </button>
                        </>
                      ) : null}
                      {project.state === EntityState.Running ||
                      project.state === EntityState.Degraded ? (
                        <>
                          <button
                            onClick={() => restartProject(project.name)}
                            className="p-1 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors cursor-pointer text-xs"
                            title="Restart"
                          >
                            <RotateCw className="w-4 h-4" />
                          </button>
                          <button
                            onClick={() => stopProject(project.name)}
                            className="p-1 text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-300 hover:bg-yellow-50 dark:hover:bg-yellow-900/20 rounded transition-colors cursor-pointer text-xs"
                            title="Stop"
                          >
                            <Square className="w-4 h-4" />
                          </button>
                        </>
                      ) : null}
                      {project.state !== EntityState.Down ? (
                        <button
                          onClick={() => handleRemoveComposeProject(project)}
                          className="p-1 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition-colors cursor-pointer text-xs"
                          title="Remove"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      ) : null}
                    </div>
                  </div>
                  <div className="mt-1 text-xs text-gray-600 dark:text-gray-400">
                    {project.path && <span>Directory: {project.path}</span>}
                  </div>
                </div>

                {/* Services List with collapse/expand and transition */}
                <div
                  className={`transition-[max-height,opacity] duration-200 ease-in-out ${isOpen && project.services && project.services.length > 0 ? 'max-h-[1000px] opacity-100' : 'max-h-0 opacity-0'} overflow-hidden`}
                  style={{ willChange: 'max-height, opacity' }}
                  aria-hidden={!isOpen}
                >
                  {project.services && project.services.length > 0 ? (
                    <div className="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-xl shadow border border-gray-100 dark:border-gray-700 overflow-hidden">
                      <div className="overflow-x-auto">
                        <table className="w-full">
                          <thead className="bg-white/50 dark:bg-gray-800/50 border-b border-gray-200 dark:border-gray-700">
                            <tr>
                              <th className="px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                                Name
                              </th>
                              <th className="px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                                Image
                              </th>
                              <th className="px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                                State
                              </th>
                              <th className="px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                                Status
                              </th>
                              <th className="px-4 py-2 text-left text-xs font-bold text-gray-700 dark:text-gray-300 uppercase tracking-wider">
                                Actions
                              </th>
                            </tr>
                          </thead>

                          <tbody className="divide-y divide-gray-100 dark:divide-gray-700">
                            {project.services.map((service: ComposeService) => (
                              <tr
                                key={service.id}
                                className="hover:bg-white dark:hover:bg-gray-800 transition-all"
                              >
                                <td className="px-4 py-2 whitespace-nowrap">
                                  <div className="text-xs font-medium text-gray-900 dark:text-white">
                                    {service.name}
                                  </div>
                                  <div className="text-[10px] text-gray-500 dark:text-gray-400 font-mono">
                                    {service.id}
                                  </div>
                                </td>
                                <td className="px-4 py-2">
                                  <div className="text-xs text-gray-900 dark:text-gray-300">
                                    {service.image}
                                  </div>
                                </td>
                                <td className="px-4 py-2 whitespace-nowrap">
                                  <StateBadge
                                    className={`${getStateColor(service.state)} text-xs px-2 py-0.5`}
                                    status={service.state}
                                    size="sm"
                                  />
                                </td>
                                <td className="px-4 py-2">
                                  <div className="text-xs text-gray-500 dark:text-gray-400">
                                    {service.status}
                                  </div>
                                </td>
                                <td className="px-4 py-2 whitespace-nowrap text-xs">
                                  <div className="flex items-center gap-1">
                                    {service.state == EntityState.Running ? (
                                      <>
                                      <button
                                          onClick={() => restartContainer(service.id, service.name)}
                                          className="p-1 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors cursor-pointer text-xs"
                                          title="Restart"
                                        >
                                          <RotateCw className="w-3 h-3" />
                                        </button>
                                        <button
                                          onClick={() => stopContainer(service.id, service.name)}
                                          className="p-1 text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-300 hover:bg-yellow-50 dark:hover:bg-yellow-900/20 rounded transition-colors cursor-pointer text-xs"
                                          title="Stop"
                                        >
                                          <Square className="w-3 h-3" />
                                        </button>
                                      </>
                                    ) : (
                                      <button
                                        onClick={() => startContainer(service.id, service.name)}
                                        className="p-1 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer text-xs"
                                        title="Start"
                                      >
                                        <Play className="w-3 h-3" />
                                      </button>
                                    )}
                                    <button
                                      onClick={() => handleRemove(service)}
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
                  ) : null}
                </div>
              </div>
            );
          })}
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
        title={`${
          currentAction?.type
            ? currentAction.type.charAt(0).toUpperCase() +
              currentAction.type.slice(1)
            : "Confirm"
        } Project`}
        message={getActionMessage(currentAction)}
        confirmText={
          currentAction?.type
            ? currentAction.type.charAt(0).toUpperCase() +
              currentAction.type.slice(1)
            : "Confirm"
        }
        cancelText="Cancel"
        variant={currentAction?.type === "down" ? "danger" : "warning"}
        isLoading={isComposeActionPending}
      />
    </div>
  );
};
