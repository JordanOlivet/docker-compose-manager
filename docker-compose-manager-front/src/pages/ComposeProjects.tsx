import { useState, useEffect } from "react";
import {
  Play,
  Square,
  RefreshCw,
  Eye,
  Trash2,
  Zap,
  RotateCw,
} from "lucide-react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { AxiosError } from "axios";
import { composeApi, containersApi } from "../api";
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
import { signalRService } from "../services/signalRService";
import { useToast } from "../hooks/useToast";
import { type ApiErrorResponse } from "../utils/errorFormatter";

interface ProjectAction {
  project: ComposeProject | null;
  type: "up" | "down" | "restart" | "stop" | "up-recreate";
}

export const ComposeProjects = () => {
  const queryClient = useQueryClient();
  const [actionDialogOpen, setActionDialogOpen] = useState(false);
  const [currentAction, setCurrentAction] = useState<ProjectAction | null>(
    null
  );
  const toast = useToast();

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

  // Up compose mutation
  const upComposeMutation = useMutation({
    mutationFn: ({
      projectName,
      options,
    }: {
      projectName: string;
      options?: { build?: boolean; detach?: boolean; forceRecreate?: boolean };
    }) => composeApi.upProject(projectName, options || { detach: true }),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["composeProjects"] });
      const recreateText = variables.options?.forceRecreate
        ? " (recreated)"
        : "";
      toast.success(
        `Compose project "${variables.projectName}" started successfully${recreateText}`
      );
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to start compose project "${variables.projectName}"`
      );
    },
  });

  // Down compose mutation
  const downComposeMutation = useMutation({
    mutationFn: (projectName: string) =>
      composeApi.downProject(projectName, {}),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["composeProjects"] });
      toast.success(
        `Compose project "${variables}" stopped and removed successfully`
      );
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to down compose project "${variables}"`
      );
    },
  });

  // Restart compose mutation
  const restartComposeMutation = useMutation({
    mutationFn: (projectName: string) => composeApi.restartProject(projectName),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["composeProjects"] });
      toast.success(`Compose project "${variables}" restarted successfully`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to restart compose project "${variables}"`
      );
    },
  });

  // Stop compose mutation
  const stopComposeMutation = useMutation({
    mutationFn: (projectName: string) => composeApi.stopProject(projectName),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["composeProjects"] });
      toast.success(`Compose project "${variables}" stopped successfully`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to stop compose project "${variables}"`
      );
    },
  });

  // Start container mutation
  const startContainerMutation = useMutation({
    mutationFn: ({ id }: { id: string; name: string }) =>
      containersApi.start(id),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["composeProjects"] });
      toast.success(`Container "${variables.name}" started successfully`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to start container "${variables.name}"`
      );
    },
  });

  // Stop container mutation
  const stopContainerMutation = useMutation({
    mutationFn: ({ id }: { id: string; name: string }) =>
      containersApi.stop(id),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["composeProjects"] });
      toast.success(`Container "${variables.name}" stopped successfully`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to stop container "${variables.name}"`
      );
    },
  });

  // Restart container mutation
  const restartContainerMutation = useMutation({
    mutationFn: ({ id }: { id: string; name: string }) =>
      containersApi.restart(id),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["composeProjects"] });
      toast.success(`Container "${variables.name}" restarted successfully`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to restart container "${variables.name}"`
      );
    },
  });

  // Remove container mutation
  const removeContainerMutation = useMutation({
    mutationFn: ({ id, force }: { id: string; name: string; force: boolean }) =>
      containersApi.remove(id, force),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["composeProjects"] });
      toast.success(`Container "${variables.name}" removed successfully`);
    },
    onError: (error: AxiosError<ApiErrorResponse>, variables) => {
      toast.error(
        error.response?.data?.message ||
          `Failed to remove container "${variables.name}"`
      );
    },
  });

  // Setup SignalR connection for real-time updates
  useEffect(() => {
    const connectAndListen = async () => {
      try {
        console.log("Connecting to SignalR operations hub...");
        // Connect to the operations hub
        await signalRService.connect();
        console.log("Connected to SignalR operations hub successfully");

        // Listen for operation updates
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const handleOperationUpdate = (update: any) => {
          console.log("Operation update received:", update);
          console.log("Update status:", update.status, "Type:", update.type);

          // Only react to completed or failed compose operations
          const statusMatch =
            update.status === "completed" || update.status === "failed";
          const typeMatch =
            update.type && update.type.toLowerCase().includes("compose");

          console.log("Status match:", statusMatch, "Type match:", typeMatch);

          if (statusMatch && typeMatch) {
            console.log("✅ Refreshing compose projects list...");
            // Immediately refetch projects to show the new state
            queryClient.invalidateQueries({ queryKey: ["composeProjects"] });
          } else {
            console.log("❌ Not refreshing - conditions not met");
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
  }, [queryClient]);

  const confirmAction = () => {
    if (!currentAction?.project) return;

    const projectName = currentAction.project.name;

    switch (currentAction.type) {
      case "up":
        upComposeMutation.mutate({ projectName, options: { detach: true } });
        break;
      case "up-recreate":
        upComposeMutation.mutate({
          projectName,
          options: { detach: true, forceRecreate: true },
        });
        break;
      case "down":
        downComposeMutation.mutate(projectName);
        break;
      case "restart":
        restartComposeMutation.mutate(projectName);
        break;
      case "stop":
        stopComposeMutation.mutate(projectName);
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
      downComposeMutation.mutate(project.name);
    }
  };

  const handleRemove = (service: ComposeService) => {
    const isRunning = service.state == EntityState.Running;
    const message = isRunning
      ? `Container ${service.name} is running. Force remove it?`
      : `Remove container ${service.name}?`;

    if (confirm(message)) {
      removeContainerMutation.mutate({
        id: service.id,
        name: service.name,
        force: isRunning,
      });
    }
  };

  const isActionPending =
    upComposeMutation.isPending ||
    downComposeMutation.isPending ||
    restartComposeMutation.isPending ||
    stopComposeMutation.isPending;

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

  return (
    <div className="space-y-8">
      {/* Page Header */}
      <div className="mb-8">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-4xl font-bold text-gray-900 dark:text-white mb-3">
              Compose Projects
            </h1>
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
          {projects.length} {projects.length === 1 ? "project" : "projects"}{" "}
          found
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
        <div className="space-y-4">
          {projects.map((project) => (
            <div
              key={project.name}
              className="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl border border-gray-200 dark:border-gray-700 overflow-visible shadow-lg hover:shadow-2xl transition-all duration-300"
            >
              {/* Project Header */}
              <div className="bg-white dark:bg-gray-800 px-6 py-4 rounded-2xl border-gray-200 dark:border-gray-700">
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
                    project.state === EntityState.Degraded ? (
                      <>
                        <button
                          onClick={() =>
                            upComposeMutation.mutate({
                              projectName: project.name,
                              options: { detach: true },
                            })
                          }
                          className="p-1.5 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer"
                          title="Start"
                        >
                          <Play className="w-4 h-4" />
                        </button>
                        <button
                          onClick={() =>
                            upComposeMutation.mutate({
                              projectName: project.name,
                              options: { detach: true, forceRecreate: true },
                            })
                          }
                          className="p-1.5 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer"
                          title="Start and Force Recreate"
                        >
                          <Zap className="w-4 h-4" />
                        </button>
                      </>
                    ) : null}
                    {project.state === EntityState.Running ||
                    project.state === EntityState.Degraded ? (
                      <>
                        <button
                          onClick={() =>
                            restartComposeMutation.mutate(project.name)
                          }
                          className="p-1.5 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors cursor-pointer"
                          title="Restart"
                        >
                          <RotateCw className="w-4 h-4" />
                        </button>
                        <button
                          onClick={() =>
                            stopComposeMutation.mutate(project.name)
                          }
                          className="p-1.5 text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-300 hover:bg-yellow-50 dark:hover:bg-yellow-900/20 rounded transition-colors cursor-pointer"
                          title="Stop"
                        >
                          <Square className="w-4 h-4" />
                        </button>
                      </>
                    ) : null}
                    {project.state !== EntityState.Down
                    ? (
                      <>
                        <button
                      onClick={() => handleRemoveComposeProject(project)}
                      className="p-1.5 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition-colors cursor-pointer"
                      title="Remove"
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                      </>
                    ) : null}
                    <button
                      onClick={() => {
                        // TODO: Implement logs viewer
                        alert("Logs viewer coming soon");
                      }}
                      className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-gray-700 dark:text-gray-300 bg-gray-100 dark:bg-gray-700 rounded-md hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors cursor-pointer"
                      title="View logs"
                    >
                      <Eye className="w-4 h-4" />
                      Logs
                    </button>
                  </div>
                </div>
                <div className="mt-2 text-sm text-gray-600 dark:text-gray-400">
                  {project.path && <span>Directory: {project.path}</span>}
                </div>
              </div>

              {/* Services List */}
              {!project.services || project.services.length === 0 ? (
                <div />
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
                                        stopContainerMutation.mutate({
                                          id: service.id,
                                          name: service.name,
                                        })
                                      }
                                      className="p-1.5 text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-300 hover:bg-yellow-50 dark:hover:bg-yellow-900/20 rounded transition-colors cursor-pointer"
                                      title="Stop"
                                    >
                                      <Square className="w-4 h-4" />
                                    </button>
                                    <button
                                      onClick={() =>
                                        restartContainerMutation.mutate({
                                          id: service.id,
                                          name: service.name,
                                        })
                                      }
                                      className="p-1.5 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors cursor-pointer"
                                      title="Restart"
                                    >
                                      <RotateCw className="w-4 h-4" />
                                    </button>
                                  </>
                                ) : (
                                  <button
                                    onClick={() =>
                                      startContainerMutation.mutate({
                                        id: service.id,
                                        name: service.name,
                                      })
                                    }
                                    className="p-1.5 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer"
                                    title="Start"
                                  >
                                    <Play className="w-4 h-4" />
                                  </button>
                                )}
                                <button
                                  onClick={() => handleRemove(service)}
                                  className="p-1.5 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition-colors cursor-pointer"
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
        isLoading={isActionPending}
      />
    </div>
  );
};
