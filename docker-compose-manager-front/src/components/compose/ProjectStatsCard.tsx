import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { containersApi } from '@/api/containers';
import { EntityState, type ComposeService, type ContainerStats } from '@/types';
import { Activity, Cpu, HardDrive, Network } from 'lucide-react';

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
          } catch (error: any) {
            // Don't log 404 errors - container was probably stopped/removed
            if (error?.response?.status !== 404) {
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
      timestamp: new Date(),
    };

    servicesStats.forEach((stats) => {
      aggregated.cpuPercentage += stats.cpuPercentage;
      aggregated.memoryUsage += stats.memoryUsage;
      aggregated.memoryLimit += stats.memoryLimit;
      aggregated.networkRx += stats.networkRx;
      aggregated.networkTx += stats.networkTx;
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

  // Format bytes to human-readable
  const formatBytes = (bytes: number): string => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${(bytes / Math.pow(k, i)).toFixed(2)} ${sizes[i]}`;
  };

  // Format network speed (bytes/s)
  const formatSpeed = (bytesPerSecond: number): string => {
    return `${formatBytes(bytesPerSecond)}/s`;
  };

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

  // Determine best unit for network speed based on max value in dataset
  const getBestNetworkUnit = (data: AggregatedStats[]): { unit: string; divisor: number } => {
    if (data.length === 0) return { unit: 'KB/s', divisor: 1024 };

    const maxValue = Math.max(
      ...data.map(d => Math.max(d.networkRx, d.networkTx))
    );
    const k = 1024;

    if (maxValue >= k * k * k) {
      return { unit: 'GB/s', divisor: k * k * k };
    } else if (maxValue >= k * k) {
      return { unit: 'MB/s', divisor: k * k };
    } else if (maxValue >= k) {
      return { unit: 'KB/s', divisor: k };
    }
    return { unit: 'B/s', divisor: 1 };
  };

  // Get adaptive units for current data
  const memoryUnit = getBestMemoryUnit(statsHistory);
  const networkUnit = getBestNetworkUnit(statsHistory);

  if (services.length === 0 || !services.some(s => s.state === EntityState.Running)) {
    return (
      <div className="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg p-6">
        <div className="flex items-center gap-2 mb-4">
          <Activity className="h-5 w-5 text-gray-600 dark:text-gray-400" />
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">Project Statistics</h3>
        </div>
        <p className="text-sm text-gray-600 dark:text-gray-400">No running services to display stats.</p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* CPU Stats */}
      <div className="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg hover:shadow-2xl transition-all duration-300 overflow-hidden">
        <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Cpu className="h-4 w-4 text-blue-600 dark:text-blue-400" />
              <h4 className="text-lg font-semibold text-gray-900 dark:text-white">CPU Usage</h4>
            </div>
            {currentStats && (
              <span className="font-mono text-sm text-gray-700 dark:text-gray-300 bg-gray-100 dark:bg-gray-700 px-3 py-1 rounded-lg">
                {currentStats.cpuPercentage.toFixed(2)}%
              </span>
            )}
          </div>
        </div>
        <div className="p-6">
          <ResponsiveContainer width="100%" height={160}>
            <LineChart data={statsHistory}>
              <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
              <XAxis
                dataKey="timestamp"
                tickFormatter={(time) => new Date(time).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
                tick={{ fontSize: 11, fill: '#6b7280' }}
                stroke="#d1d5db"
                domain={['dataMin', 'dataMax']}
              />
              <YAxis
                domain={[0, 'auto']}
                tick={{ fontSize: 11, fill: '#6b7280' }}
                stroke="#d1d5db"
                tickFormatter={(value) => `${value.toFixed(1)}%`}
              />
              <Tooltip
                labelFormatter={(time) => new Date(time).toLocaleTimeString()}
                formatter={(value: number) => [`${value.toFixed(2)}%`, 'CPU']}
                contentStyle={{
                  backgroundColor: 'rgba(255, 255, 255, 0.95)',
                  border: '1px solid #e5e7eb',
                  borderRadius: '8px',
                }}
              />
              <Line
                type="monotone"
                dataKey="cpuPercentage"
                stroke="#3b82f6"
                strokeWidth={2}
                dot={false}
                isAnimationActive={false}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </div>

      {/* Memory Stats */}
      <div className="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg hover:shadow-2xl transition-all duration-300 overflow-hidden">
        <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <HardDrive className="h-4 w-4 text-green-600 dark:text-green-400" />
              <h4 className="text-lg font-semibold text-gray-900 dark:text-white">Memory Usage</h4>
            </div>
            {currentStats && (
              <span className="font-mono text-xs text-gray-700 dark:text-gray-300 bg-gray-100 dark:bg-gray-700 px-3 py-1 rounded-lg">
                {formatBytes(currentStats.memoryUsage)} / {formatBytes(currentStats.memoryLimit)}
                {' '}({currentStats.memoryPercentage.toFixed(2)}%)
              </span>
            )}
          </div>
        </div>
        <div className="p-6">
          <ResponsiveContainer width="100%" height={160}>
            <LineChart data={statsHistory.map(d => ({
              ...d,
              memoryUsageScaled: d.memoryUsage / memoryUnit.divisor
            }))}>
              <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
              <XAxis
                dataKey="timestamp"
                tickFormatter={(time) => new Date(time).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
                tick={{ fontSize: 11, fill: '#6b7280' }}
                stroke="#d1d5db"
                domain={['dataMin', 'dataMax']}
              />
              <YAxis
                domain={[0, 'auto']}
                tick={{ fontSize: 11, fill: '#6b7280' }}
                stroke="#d1d5db"
                tickFormatter={(value) => `${value.toFixed(1)} ${memoryUnit.unit}`}
              />
              <Tooltip
                labelFormatter={(time) => new Date(time).toLocaleTimeString()}
                formatter={(value: number) => [`${value.toFixed(2)} ${memoryUnit.unit}`, 'Memory']}
                contentStyle={{
                  backgroundColor: 'rgba(255, 255, 255, 0.95)',
                  border: '1px solid #e5e7eb',
                  borderRadius: '8px',
                }}
              />
              <Line
                type="monotone"
                dataKey="memoryUsageScaled"
                stroke="#10b981"
                strokeWidth={2}
                dot={false}
                isAnimationActive={false}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </div>

      {/* Network Stats */}
      <div className="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg hover:shadow-2xl transition-all duration-300 overflow-hidden">
        <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Network className="h-4 w-4 text-purple-600 dark:text-purple-400" />
              <h4 className="text-lg font-semibold text-gray-900 dark:text-white">Network I/O</h4>
            </div>
            {currentStats && (
              <span className="font-mono text-xs text-gray-700 dark:text-gray-300 bg-gray-100 dark:bg-gray-700 px-3 py-1 rounded-lg">
                ↓ {formatSpeed(currentStats.networkRx)} ↑ {formatSpeed(currentStats.networkTx)}
              </span>
            )}
          </div>
        </div>
        <div className="p-6">
          <ResponsiveContainer width="100%" height={160}>
            <LineChart data={statsHistory.map(d => ({
              ...d,
              networkRxScaled: d.networkRx / networkUnit.divisor,
              networkTxScaled: d.networkTx / networkUnit.divisor
            }))}>
              <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
              <XAxis
                dataKey="timestamp"
                tickFormatter={(time) => new Date(time).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
                tick={{ fontSize: 11, fill: '#6b7280' }}
                stroke="#d1d5db"
                domain={['dataMin', 'dataMax']}
              />
              <YAxis
                domain={[0, 'auto']}
                tick={{ fontSize: 11, fill: '#6b7280' }}
                stroke="#d1d5db"
                tickFormatter={(value) => `${value.toFixed(1)} ${networkUnit.unit}`}
              />
              <Tooltip
                labelFormatter={(time) => new Date(time).toLocaleTimeString()}
                formatter={(value: number, name: string) => [
                  `${value.toFixed(2)} ${networkUnit.unit}`,
                  name === 'networkRxScaled' ? 'Download' : 'Upload',
                ]}
                contentStyle={{
                  backgroundColor: 'rgba(255, 255, 255, 0.95)',
                  border: '1px solid #e5e7eb',
                  borderRadius: '8px',
                }}
              />
              <Line
                type="monotone"
                dataKey="networkRxScaled"
                stroke="#8b5cf6"
                strokeWidth={2}
                dot={false}
                isAnimationActive={false}
                name="networkRxScaled"
              />
              <Line
                type="monotone"
                dataKey="networkTxScaled"
                stroke="#f59e0b"
                strokeWidth={2}
                dot={false}
                isAnimationActive={false}
                name="networkTxScaled"
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </div>
    </div>
  );
}
