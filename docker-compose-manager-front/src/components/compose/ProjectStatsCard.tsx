import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { containersApi } from '@/api/containers';
import { EntityState, type ComposeService, type ContainerStats } from '@/types';
import { Activity, Cpu, HardDrive } from 'lucide-react';
import { ResourceStatsCard, type StatsMetricConfig } from '@/components/common/ResourceStatsCard';
import { t } from '@/i18n';

interface ProjectStatsCardProps {
  services: ComposeService[];
}

interface AggregatedStats {
  cpuPercentage: number;
  memoryUsage: number;
  memoryLimit: number;
  memoryPercentage: number;
  networkRx: number;
  networkTx: number;
  diskRead: number;
  diskWrite: number;
  timestamp: Date;
}

export function ProjectStatsCard({ services }: ProjectStatsCardProps) {
  const [statsHistory, setStatsHistory] = useState<AggregatedStats[]>([]);
  const [currentStats, setCurrentStats] = useState<AggregatedStats | null>(null);

  // Fetch stats for all services every 1 seconds
  const { data: servicesStats } = useQuery({
    queryKey: ['projectStats', services.map(s => s.id).sort().join(',')],
    queryFn: async () => {
      const statsPromises = services
        .filter(s => s.state === EntityState.Running)
        .map(async (service) => {
          try {
            return await containersApi.getStats(service.id);
          } catch (error: unknown) {
            // Don't log 404 errors - container was probably stopped/removed
            const axiosError = error as { response?: { status?: number } };
            if (axiosError?.response?.status !== 404) {
              console.error(`Failed to fetch stats for ${service.name}:`, error);
            }
            return null;
          }
        });

      const stats = await Promise.all(statsPromises);
      return stats.filter((s): s is ContainerStats => s !== null);
    },
    refetchInterval: 1000, // Refresh every 1 seconds
    enabled: services.some(s => s.state === EntityState.Running),
    retry: false, // Don't retry on failure - if container is gone, it's gone
  });

  // Update current stats when new data arrives
  useEffect(() => {
    if (!servicesStats || servicesStats.length === 0) return;

    const aggregated: AggregatedStats = {
      cpuPercentage: 0,
      memoryUsage: 0,
      memoryLimit: 0,
      memoryPercentage: 0,
      networkRx: 0,
      networkTx: 0,
      diskRead: 0,
      diskWrite: 0,
      timestamp: new Date(),
    };

    servicesStats.forEach((stats) => {
      aggregated.cpuPercentage += stats.cpuPercentage;
      aggregated.memoryUsage += stats.memoryUsage;
      aggregated.memoryLimit += stats.memoryLimit;
      aggregated.networkRx += stats.networkRx;
      aggregated.networkTx += stats.networkTx;
      aggregated.diskRead += stats.diskRead;
      aggregated.diskWrite += stats.diskWrite;
    });

    // Calculate average memory percentage
    if (aggregated.memoryLimit > 0) {
      aggregated.memoryPercentage = (aggregated.memoryUsage / aggregated.memoryLimit) * 100;
    }

    setCurrentStats(aggregated);
  }, [servicesStats]);

  // Timer to add data points every 2 seconds (keeps chart moving even if values don't change)
  useEffect(() => {
    if (!currentStats) return;

    const interval = setInterval(() => {
      // Add a new point with current stats and current timestamp
      const newPoint: AggregatedStats = {
        ...currentStats,
        timestamp: new Date(), // Always use current time to keep chart moving
      };

      setStatsHistory((prev) => {
        // Keep only last 5 minutes of data (sliding window)
        const fiveMinutesAgo = new Date(Date.now() - 5 * 60 * 1000);
        const filtered = prev.filter(stat => stat.timestamp >= fiveMinutesAgo);
        return [...filtered, newPoint];
      });
    }, 1000); // Add point every 2 seconds

    return () => clearInterval(interval);
  }, [currentStats]);

  // (Removed unused formatBytes helper after refactor)


  // Determine best unit for memory based on max value in dataset
  const getBestMemoryUnit = (data: AggregatedStats[]): { unit: string; divisor: number } => {
    if (data.length === 0) return { unit: 'MB', divisor: 1024 * 1024 };

    const maxValue = Math.max(...data.map(d => d.memoryUsage));
    const k = 1024;

    if (maxValue >= k * k * k) {
      return { unit: 'GB', divisor: k * k * k };
    } else if (maxValue >= k * k) {
      return { unit: 'MB', divisor: k * k };
    } else if (maxValue >= k) {
      return { unit: 'KB', divisor: k };
    }
    return { unit: 'B', divisor: 1 };
  };

  // Get adaptive memory unit for current data (network handled by grouped card)
  const memoryUnit = getBestMemoryUnit(statsHistory);

  const isActive = services.some(s => s.state === EntityState.Running);
  if (!isActive) {
    return (
      <div className="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg p-6">
        <div className="flex items-center gap-2 mb-4">
          <Activity className="h-5 w-5 text-gray-600 dark:text-gray-400" />
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">{t('common.projectStatistics')}</h3>
        </div>
        <p className="text-sm text-gray-600 dark:text-gray-400">{t('common.noRunningServices')}</p>
      </div>
    );
  }

  const metrics: StatsMetricConfig<AggregatedStats>[] = [
    {
      id: 'cpuPercentage',
      label: 'CPU Usage',
      icon: <Cpu className="h-4 w-4 text-blue-600 dark:text-blue-400" />,
      value: s => s.cpuPercentage,
      format: v => v.toFixed(2) + '%',
      unit: '%',
      color: '#3b82f6',
    },
    {
      id: 'memoryUsage',
      label: 'Memory',
      icon: <HardDrive className="h-4 w-4 text-green-600 dark:text-green-400" />,
      value: s => s.memoryUsage / memoryUnit.divisor,
      headerFormat: (stats) => `${(stats.memoryUsage / memoryUnit.divisor).toFixed(2)} ${memoryUnit.unit} / ${(stats.memoryLimit / memoryUnit.divisor).toFixed(2)} ${memoryUnit.unit}`,
      unit: memoryUnit.unit,
      color: '#10b981',
    },
  ];

  // Grouped network chart (RX/TX raw bytes for adaptive scaling inside ResourceStatsCard)
  const groups = [
    {
      id: 'network',
      label: 'Network (RX / TX)',
      metrics: [
        { id: 'networkRx', label: 'RX', value: (s: AggregatedStats) => s.networkRx, color: '#8b5cf6' },
        { id: 'networkTx', label: 'TX', value: (s: AggregatedStats) => s.networkTx, color: '#f59e0b' },
      ],
    },
    {
			id: 'diskio',
			label: 'Disk IO (Read / Write)',
			metrics: [
				{ id: 'diskRead', label: 'Read', value: (s: AggregatedStats) => s.diskRead, color: '#8b5cf6' },
				{ id: 'diskWrite', label: 'Write', value: (s: AggregatedStats) => s.diskWrite, color: '#ec4899' },
			],
		},
  ];

  return (
    <ResourceStatsCard
      title="Project Statistics"
      isActive={isActive}
      getStats={async () => currentStats}
      metrics={metrics}
      groups={groups}
      fetchIntervalMs={1000}
      chartIntervalMs={1000}
      maxWindowMinutes={5}
      emptyMessage="No running services to display stats."
    />
  );
}
