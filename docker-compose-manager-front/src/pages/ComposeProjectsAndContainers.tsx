import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { containersApi } from "../api/containers";
import type { Container } from "../types";
import { useToast } from "../hooks/useToast";
import {
  Play,
  Square,
  RotateCw,
  Trash2,
  Container as ContainerIcon,
  RotateCcw,
  RefreshCw,
  Eye,
  Hammer,
  Zap,
} from "lucide-react";
import {} from "lucide-react";
import { ProjectStatus, type ComposeProject } from "../types";
import { signalRService } from "../services/signalRService";
import { composeApi } from "../api";
import {
  LoadingSpinner,
  ErrorDisplay,
  ConfirmDialog,
  StatusBadge,
  SplitButton,
} from "../components/common";

interface ProjectAction {
  project: ComposeProject | null;
  type:
    | "up"
    | "down"
    | "restart"
    | "stop"
    | "start"
    | "up-build"
    | "up-recreate"
    | "up-build-recreate";
}

export default function ComposeProjectsAndContainers() {
  const queryClient = useQueryClient();
  const toast = useToast();
  const [actionDialogOpen, setActionDialogOpen] = useState(false);
  const [currentAction, setCurrentAction] = useState<ProjectAction | null>(
    null
  );

  const {
    data: containers,
    isLoading,
    error,
  } = useQuery({
    queryKey: ["containers"],
    queryFn: () => containersApi.list(),
    refetchInterval: 5000, // Refresh every 5 seconds
  });

  const startContainerMutation = useMutation({
    mutationFn: (id: string) => containersApi.start(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["containers"] });
      toast.success("Container started successfully");
    },
    onError: (error: any) => {
      toast.error(error.response?.data?.message || "Failed to start container");
    },
  });

  const stopContainerMutation = useMutation({
    mutationFn: (id: string) => containersApi.stop(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["containers"] });
      toast.success("Container stopped successfully");
    },
    onError: (error: any) => {
      toast.error(error.response?.data?.message || "Failed to stop container");
    },
  });

  const restartContainerMutation = useMutation({
    mutationFn: (id: string) => containersApi.restart(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["containers"] });
      toast.success("Container restarted successfully");
    },
    onError: (error: any) => {
      toast.error(
        error.response?.data?.message || "Failed to restart container"
      );
    },
  });

  const removeContainerMutation = useMutation({
    mutationFn: ({ id, force }: { id: string; force: boolean }) =>
      containersApi.remove(id, force),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["containers"] });
      toast.success("Container removed successfully");
    },
    onError: (error: any) => {
      toast.error(
        error.response?.data?.message || "Failed to remove container"
      );
    },
  });

  const getStateColor = (state: string) => {
    switch (state.toLowerCase()) {
      case "running":
        return "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200";
      case "exited":
        return "bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200";
      case "paused":
        return "bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200";
      case "created":
        return "bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200";
      default:
        return "bg-gray-100 text-gray-800 dark:bg-gray-900 dark:text-gray-200";
    }
  };

  const handleRemove = (container: Container) => {
    const isRunning = container.state.toLowerCase() === "running";
    const message = isRunning
      ? `Container ${container.name} is running. Force remove it?`
      : `Remove container ${container.name}?`;

    if (confirm(message)) {
      removeContainerMutation.mutate({ id: container.id, force: isRunning });
    }
  };

  // Fetch projects
  const {
    data: projects = [],
    isLoading: isComposeLoading,
    error: composeError,
    refetch,
  } = useQuery({
    queryKey: ["composeProjects"],
    queryFn: composeApi.listProjects,
    // Removed automatic polling - we use SignalR for real-time updates now
    refetchInterval: false,
  });

  // Up mutation
  const upComposeMutation = useMutation({
    mutationFn: ({
      projectName,
      options,
    }: {
      projectName: string;
      options?: { build?: boolean; detach?: boolean; forceRecreate?: boolean };
    }) => composeApi.upProject(projectName, options || { detach: true }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["composeProjects"] });
      setActionDialogOpen(false);
      setCurrentAction(null);
    },
  });

  // Down mutation
  const downComposeMutation = useMutation({
    mutationFn: (projectName: string) =>
      composeApi.downProject(projectName, {}),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["composeProjects"] });
      setActionDialogOpen(false);
      setCurrentAction(null);
    },
  });

  // Restart mutation
  const restartComposeMutation = useMutation({
    mutationFn: (projectName: string) => composeApi.restartProject(projectName),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["composeProjects"] });
      setActionDialogOpen(false);
      setCurrentAction(null);
    },
  });

  // Stop mutation
  const stopComposeMutation = useMutation({
    mutationFn: (projectName: string) => composeApi.stopProject(projectName),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["composeProjects"] });
      setActionDialogOpen(false);
      setCurrentAction(null);
    },
  });

  // Start mutation
  const startComposeMutation = useMutation({
    mutationFn: (projectName: string) => composeApi.startProject(projectName),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["composeProjects"] });
      setActionDialogOpen(false);
      setCurrentAction(null);
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

  const handleAction = (
    project: ComposeProject,
    type: ProjectAction["type"]
  ) => {
    setCurrentAction({ project, type });
    setActionDialogOpen(true);
  };

  const confirmAction = () => {
    if (!currentAction?.project) return;

    const projectName = currentAction.project.name;

    switch (currentAction.type) {
      case "up":
        upComposeMutation.mutate({ projectName, options: { detach: true } });
        break;
      case "up-build":
        upComposeMutation.mutate({
          projectName,
          options: { detach: true, build: true },
        });
        break;
      case "up-recreate":
        upComposeMutation.mutate({
          projectName,
          options: { detach: true, forceRecreate: true },
        });
        break;
      case "up-build-recreate":
        upComposeMutation.mutate({
          projectName,
          options: { detach: true, build: true, forceRecreate: true },
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
      case "start":
        startComposeMutation.mutate(projectName);
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

  const isActionPending =
    upComposeMutation.isPending ||
    downComposeMutation.isPending ||
    restartComposeMutation.isPending ||
    stopComposeMutation.isPending ||
    startComposeMutation.isPending;

  if (isComposeLoading) {
    return (
      <div className="flex items-center justify-center h-96">
        <LoadingSpinner size="lg" text="Loading compose projects..." />
      </div>
    );
  }

  if (composeError) {
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

  if (isLoading) return <LoadingSpinner />;
  if (error) return <ErrorDisplay message="Failed to load containers" />;

  return (
    <div>
      <div className="space-y-8">
        {/* Page Header */}
        <div className="flex justify-between items-center mb-8">
          <div>
            <h1 className="text-4xl font-bold text-gray-900 dark:text-white mb-3">
              Compose Projects and Containers
            </h1>
            <p className="text-lg text-gray-600 dark:text-gray-400">
              Manage your Docker compose projects and containers
            </p>
          </div>
        </div>

        {!containers || containers.length === 0 ? (
          <div className="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl shadow-lg border border-gray-100 dark:border-gray-700 p-12 text-center">
            <div className="inline-flex items-center justify-center w-20 h-20 rounded-full bg-gray-100 dark:bg-gray-800 mb-4">
              <ContainerIcon className="w-10 h-10 text-gray-400" />
            </div>
            <p className="text-lg text-gray-600 dark:text-gray-400">
              No containers found
            </p>
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
                    <tr
                      key={container.id}
                      className="hover:bg-white dark:hover:bg-gray-800 transition-all"
                    >
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
                        <span
                          className={`px-3 py-1.5 inline-flex text-xs leading-5 font-semibold rounded-full ${getStateColor(
                            container.state
                          )}`}
                        >
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
                          {container.state.toLowerCase() === "running" ? (
                            <>
                              <button
                                onClick={() =>
                                  stopContainerMutation.mutate(container.id)
                                }
                                className="p-1.5 text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-300 hover:bg-yellow-50 dark:hover:bg-yellow-900/20 rounded transition-colors"
                                title="Stop"
                              >
                                <Square className="w-4 h-4" />
                              </button>
                              <button
                                onClick={() =>
                                  restartContainerMutation.mutate(container.id)
                                }
                                className="p-1.5 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors"
                                title="Restart"
                              >
                                <RotateCw className="w-4 h-4" />
                              </button>
                            </>
                          ) : (
                            <button
                              onClick={() =>
                                startContainerMutation.mutate(container.id)
                              }
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

      <div className="space-y-8">
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
                      <StatusBadge status={project.status} />
                    </div>
                    <div className="flex gap-2">
                      {project.status === ProjectStatus.Down ||
                      project.status === ProjectStatus.Stopped ||
                      project.status === ProjectStatus.Partial ? (
                        <>
                          <button
                            onClick={() => handleAction(project, "up")}
                            className="p-1.5 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors"
                            title="Start"
                          >
                            <Play className="w-4 h-4" />
                          </button>
                          <button
                            onClick={() => handleAction(project, "up-recreate")}
                            className="p-1.5 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors"
                            title="Start and Force Recreate"
                          >
                            <Zap className="w-4 h-4" />
                          </button>
                        </>
                      ) : null}
                      {project.status === ProjectStatus.Running ||
                      project.status === ProjectStatus.Partial ? (
                        <>
                          <button
                            onClick={() => handleAction(project, "restart")}
                            className="p-1.5 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors"
                            title="Restart"
                          >
                            <RotateCw className="w-4 h-4" />
                          </button>
                          <button
                            onClick={() => handleAction(project, "stop")}
                            className="p-1.5 text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-300 hover:bg-yellow-50 dark:hover:bg-yellow-900/20 rounded transition-colors"
                            title="Stop"
                          >
                            <Square className="w-4 h-4" />
                          </button>

                          <button
                            onClick={() => handleAction(project, "down")}
                            className="p-1.5 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition-colors"
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
                        className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-gray-700 dark:text-gray-300 bg-gray-100 dark:bg-gray-700 rounded-md hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors"
                        title="View logs"
                      >
                        <Eye className="w-4 h-4" />
                        Logs
                      </button>
                    </div>
                  </div>
                  <div className="mt-2 text-sm text-gray-600 dark:text-gray-400">
                    {project.workingDirectory && (
                      <span>Directory: {project.workingDirectory}</span>
                    )}
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
                            <h5 className="font-medium text-gray-900 dark:text-white">
                              {service.name}
                            </h5>
                            {service.status && (
                              <StatusBadge status={service.status} size="sm" />
                            )}
                          </div>
                          {service.image && (
                            <p className="text-xs text-gray-600 dark:text-gray-400 mb-1">
                              <span className="font-medium">Image:</span>{" "}
                              {service.image}
                            </p>
                          )}
                          {service.ports && service.ports.length > 0 && (
                            <p className="text-xs text-gray-600 dark:text-gray-400">
                              <span className="font-medium">Ports:</span>{" "}
                              {service.ports.join(", ")}
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
    </div>
  );
}
