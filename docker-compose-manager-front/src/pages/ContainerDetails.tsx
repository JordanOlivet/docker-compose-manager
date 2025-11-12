import { useParams, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Play, Square, RotateCw, Trash2, RefreshCw } from "lucide-react";
import { containersApi } from "../api/containers";
import { useContainerMutations } from "../hooks/useContainerMutations";
import { StateBadge } from "../components/common/StateBadge";
import { LoadingSpinner } from "../components/common/LoadingSpinner";
import { ErrorDisplay } from "../components/common/ErrorDisplay";
import { EntityState, type ContainerDetails, type ContainerStats } from "../types";
// Removed local polling state (handled by ResourceStatsCard) -> no React hooks needed here
import { ComposeLogs } from "../components/compose/ComposeLogs";
import { InfoCard, type InfoSection } from "../components/common/InfoCard";
import { ResourceStatsCard, type StatsMetricConfig, type StatsGroupConfig } from "../components/common/ResourceStatsCard";
import { Variable, Network as NetworkIcon, HardDrive, Tag } from "lucide-react";

export default function ContainerDetails() {
	const { containerId } = useParams<{ containerId: string }>();
	const navigate = useNavigate();

	// Mutations (actions)
	const { startContainer, stopContainer, restartContainer, removeContainer } = useContainerMutations(["containers", "containerDetails"]);

	// Container details
	const {
		data: container,
		isLoading,
		error,
		refetch,
	} = useQuery({
		queryKey: ["containerDetails", containerId],
		queryFn: () => {
			if (!containerId) throw new Error("Container ID is required");
			return containersApi.get(containerId);
		},
		enabled: !!containerId,
		refetchInterval: false,
	});


	// Define metrics for container stats (generic via ResourceStatsCard)
	// Single metrics (no memory percentage needed)
	const singleMetrics: StatsMetricConfig<ContainerStats>[] = [
		{ id: 'cpu', label: 'CPU %', value: s => s.cpuPercentage, unit: '%', color: '#ef4444', format: v => v.toFixed(2) + '%' },
		{ id: 'memUsage', label: 'RAM', value: s => s.memoryUsage / 1024 / 1024, unit: 'MB', color: '#3b82f6', headerFormat: (stats) => `${(stats.memoryUsage/1024/1024).toFixed(2)} MB / ${(stats.memoryLimit/1024/1024).toFixed(2)} MB` },
	];

	// Grouped charts for Network (RX/TX) and Disk IO (Read/Write)
	const groups: StatsGroupConfig<ContainerStats>[] = [
		{
			id: 'network',
			label: 'Network (RX / TX)',
			metrics: [
				{ id: 'netRx', label: 'RX', value: s => s.networkRx, color: '#10b981' },
				{ id: 'netTx', label: 'TX', value: s => s.networkTx, color: '#f59e0b' },
			],
		},
		{
			id: 'diskio',
			label: 'Disk IO (Read / Write)',
			metrics: [
				{ id: 'diskRead', label: 'Read', value: s => s.diskRead, color: '#8b5cf6' },
				{ id: 'diskWrite', label: 'Write', value: s => s.diskWrite, color: '#ec4899' },
			],
		},
	];

	// Logs handled by generic ComposeLogs component now

	const getStateColor = (state: EntityState) => {
		switch (state) {
			case EntityState.Running:
				return "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200";
			case EntityState.Exited:
				return "bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200";
			default:
				return "bg-gray-100 text-gray-800 dark:bg-gray-900 dark:text-gray-200";
		}
	};

	const handleRemove = (container: ContainerDetails) => {
		const isRunning = container.state === EntityState.Running;
		const message = isRunning
			? `Container ${container.name} is running. Force remove it?`
			: `Remove container ${container.name}?`;
		if (confirm(message)) {
			removeContainer(container.id, container.name, isRunning);
		}
	};

	if (isLoading) {
		return <LoadingSpinner size="lg" text="Loading container details..." />;
	}
	if (error) {
		return <ErrorDisplay title="Failed to load container details" message={error instanceof Error ? error.message : "An unexpected error occurred"} onRetry={() => refetch()} />;
	}
	if (!container) {
		return <ErrorDisplay title="Container not found" message="The requested container could not be found" onRetry={() => navigate("/containers")} />;
	}

	return (
		<div className="space-y-8">
			{/* Header */}
			<div className="mb-8">
				<div className="flex items-center justify-between">
					<div className="flex items-center gap-4">
						<button
							onClick={() => navigate("/containers")}
							className="p-2 text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg transition-colors"
							title="Back to containers"
						>
							<ArrowLeft className="w-5 h-5" />
						</button>
						<div>
							<h1 className="text-4xl font-bold text-gray-900 dark:text-white mb-3">{container.name}</h1>
							<p className="text-lg text-gray-600 dark:text-gray-400">Container details and actions</p>
						</div>
					</div>
					<button
						onClick={() => refetch()}
						className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
					>
						<RefreshCw className="w-4 h-4" /> Refresh
					</button>
				</div>
			</div>

			{/* Container Info Card */}
			<div className="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg px-6 py-4 flex flex-col gap-6">
				{/* Header sur deux lignes, style ComposeDetails */}
				<div className="flex flex-col gap-1 w-full">
					{/* Ligne 1 : nom + state + actions */}
					<div className="flex items-center justify-between w-full gap-4">
						<div className="flex items-center gap-4 min-w-0 flex-1">
							<h2 className="text-2xl font-bold text-gray-900 dark:text-white truncate max-w-xs">{container.name}</h2>
							<StateBadge className={getStateColor(container.state)} status={container.state} size="md" />
						</div>
						<div className="flex gap-2 shrink-0">
							{container.state === EntityState.Running ? (
								<>
								<button
										onClick={() => restartContainer(container.id, container.name)}
										className="p-1.5 text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded transition-colors cursor-pointer"
										title="Restart"
									>
										<RotateCw className="w-4 h-4" />
									</button>
									<button
										onClick={() => stopContainer(container.id, container.name)}
										className="p-1.5 text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-300 hover:bg-yellow-50 dark:hover:bg-yellow-900/20 rounded transition-colors cursor-pointer"
										title="Stop"
									>
										<Square className="w-4 h-4" />
									</button>
								</>
							) : (
								<button
									onClick={() => startContainer(container.id, container.name)}
									className="p-1.5 text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 hover:bg-green-50 dark:hover:bg-green-900/20 rounded transition-colors cursor-pointer"
									title="Start"
								>
									<Play className="w-4 h-4" />
								</button>
							)}
							<button
								onClick={() => handleRemove(container)}
								className="p-1.5 text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition-colors cursor-pointer"
								title="Remove"
							>
								<Trash2 className="w-4 h-4" />
							</button>
						</div>
					</div>
					{/* Ligne 2 : infos secondaires */}
					<div className="flex flex-wrap items-center gap-6 mt-1 text-sm text-gray-600 dark:text-gray-400">
						<span className="font-mono">ID: {container.id.substring(0, 12)}</span>
						<span>Image: <span className="font-mono text-gray-900 dark:text-white">{container.image}</span></span>
						<span>Status: <span className="text-gray-900 dark:text-white">{container.status}</span></span>
						<span>Created: <span className="text-gray-900 dark:text-white">{new Date(container.created).toLocaleString()}</span></span>
					</div>
				</div>
			</div>

			{/* Details Section */}
			<div className="grid grid-cols-1 md:grid-cols-2 gap-6">
				{/* Left: Technical details using generic InfoCard */}
				<div>
					<InfoCard
						title="Technical Details"
						sections={buildContainerSections(container)}
					/>
				</div>
				{/* Right: Stats (generic charts) */}
				<div>
					<ResourceStatsCard
						title="Live Resource Stats"
						isActive={container.state === EntityState.Running}
						getStats={() => (containerId ? containersApi.getStats(containerId) : Promise.resolve(null))}
						metrics={singleMetrics}
						groups={groups}
						emptyMessage="Container is not running or stats unavailable"
					/>
				</div>
			</div>

					{/* Logs Section (generic component) */}
					<div className="w-full h-[400px] resize-y overflow-auto min-h-[300px] max-h-[800px]">
						<ComposeLogs containerId={container.id} containerName={container.name} />
					</div>
		</div>
	);
}



// Build generic sections for container technical details using InfoCard.
function buildContainerSections(container: ContainerDetails): InfoSection[] {
	const sections: InfoSection[] = [];

	if (container.env && Object.keys(container.env).length > 0) {
		sections.push({
			id: 'env',
			title: 'Environment Variables',
			icon: <Variable className="h-4 w-4 text-indigo-600 dark:text-indigo-400" />,
			count: Object.keys(container.env).length,
			initiallyOpen: true,
			content: (
				<div className="bg-gray-50 dark:bg-gray-900/50 rounded p-2 text-xs font-mono space-y-1 max-h-48 overflow-y-auto">
					{Object.entries(container.env).map(([key, value]) => (
						<div key={key} className="flex gap-2">
							<span className="text-blue-600 dark:text-blue-400 font-semibold">{key}:</span>
							<span className="text-gray-700 dark:text-gray-300 break-all">{value}</span>
						</div>
					))}
				</div>
			),
		});
	}

	if (container.ports && Object.keys(container.ports).length > 0) {
		sections.push({
			id: 'ports',
			title: 'Ports',
			icon: <Tag className="h-4 w-4 text-pink-600 dark:text-pink-400" />,
			count: Object.keys(container.ports).length,
			content: (
				<div className="flex flex-col gap-1 text-xs font-mono">
					{Object.entries(container.ports).map(([k, v]) => (
						<span key={k} className="inline-block bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 px-2 py-1 rounded">{k} → {v}</span>
					))}
				</div>
			),
		});
	}

	if (container.mounts && container.mounts.length > 0) {
		sections.push({
			id: 'mounts',
			title: 'Mounts',
			icon: <HardDrive className="h-4 w-4 text-orange-600 dark:text-orange-400" />,
			count: container.mounts.length,
			content: (
				<ul className="text-xs font-mono space-y-1">
					{container.mounts.map((m, i) => (
						<li key={i} className="text-gray-700 dark:text-gray-300">
							{m.source} : <span className="italic">{m.destination}</span> {m.readOnly ? '(ro)' : ''}
						</li>
					))}
				</ul>
			),
		});
	}

	if (container.networks && container.networks.length > 0) {
		sections.push({
			id: 'networks',
			title: 'Networks',
			icon: <NetworkIcon className="h-4 w-4 text-green-600 dark:text-green-400" />,
			count: container.networks.length,
			content: (
				<ul className="text-xs font-mono flex flex-wrap gap-1">
					{container.networks.map(n => (
						<li key={n} className="bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 px-2 py-1 rounded">{n}</li>
					))}
				</ul>
			),
		});
	}

	if (container.labels && Object.keys(container.labels).length > 0) {
		sections.push({
			id: 'labels',
			title: 'Labels',
			icon: <Tag className="h-4 w-4 text-yellow-600 dark:text-yellow-400" />,
			count: Object.keys(container.labels).length,
			content: (
				<div className="bg-gray-50 dark:bg-gray-900/50 rounded p-2 text-xs font-mono space-y-1 max-h-48 overflow-y-auto">
					{Object.entries(container.labels).map(([k, v]) => (
						<div key={k} className="flex gap-2">
							<span className="text-blue-600 dark:text-blue-400">{k}:</span>
							<span className="text-gray-700 dark:text-gray-300">{v}</span>
						</div>
					))}
				</div>
			),
		});
	}

	return sections;
}
