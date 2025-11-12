import { useEffect, useState } from 'react';
import { ResponsiveContainer, LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip } from 'recharts';
import { Cpu, HardDrive, Network } from 'lucide-react';

// Generic metric descriptor
export interface StatsMetricConfig<T> {
  id: string;                // unique key
  label: string;             // display label
  icon?: React.ReactNode;    // optional icon
  // Extract numeric value(s) from the raw stats object
  value: (stats: T) => number;            // primary value (e.g. CPU %)
  format?: (val: number) => string;       // custom formatter for header badge
  headerFormat?: (stats: T, metric: StatsMetricConfig<T>) => React.ReactNode; // richer header using entire stats object
  // For chart scaling / adaptation
  unit?: string;                         // unit suffix for axis/tooltip
  scale?: (val: number) => number;       // transform value before plotting
  color?: string;                        // stroke color
  category?: 'cpu' | 'memory' | 'network' | 'diskio'; // optional semantic category for auto adaptation
}

// Group of metrics displayed on the same chart
export interface StatsGroupConfig<T> {
  id: string;
  label: string;
  icon?: React.ReactNode;
  metrics: StatsMetricConfig<T>[]; // multiple lines per chart
  headerFormat?: (stats: T) => React.ReactNode; // custom header summary
}

interface ResourceStatsCardProps<T> {
  title?: string;
  fetchIntervalMs?: number;       // polling interval for incoming stats points
  chartIntervalMs?: number;       // interval to push a point (default match fetch)
  maxWindowMinutes?: number;      // sliding window length
  isActive: boolean;              // whether to poll/draw
  getStats: () => Promise<T | null>; // provider returning latest stats
  metrics?: StatsMetricConfig<T>[];   // single-metric-per-card mode
  groups?: StatsGroupConfig<T>[];     // multi-metric-per-card mode
  emptyMessage?: string;          // message when inactive
  headerExtras?: React.ReactNode; // right side header element
}

/**
 * Generic ResourceStatsCard that can visualize a set of numeric metrics over time.
 * The compose project card and container card can both be implemented using this base.
 */
export function ResourceStatsCard<T>({
  title = 'Statistics',
  fetchIntervalMs = 1000,
  chartIntervalMs = 1000,
  maxWindowMinutes = 5,
  isActive,
  getStats,
  metrics,
  groups,
  emptyMessage = 'No active data to display',
  headerExtras,
}: ResourceStatsCardProps<T>) {
  const [history, setHistory] = useState<Array<{ timestamp: Date; raw: T }>>([]);
  const [current, setCurrent] = useState<T | null>(null);
  const [hiddenMetrics, setHiddenMetrics] = useState<Record<string, boolean>>({});

  // Poll for latest stats
  useEffect(() => {
    if (!isActive) return;
    let cancelled = false;
    const poll = async () => {
      try {
        const stats = await getStats();
        if (!cancelled && stats) setCurrent(stats);
      } catch {
        /* swallow */
      }
    };
    poll();
    const interval = setInterval(poll, fetchIntervalMs);
    return () => { cancelled = true; clearInterval(interval); };
  }, [isActive, fetchIntervalMs, getStats]);

  // Append chart points
  useEffect(() => {
    if (!isActive || !current) return;
    const interval = setInterval(() => {
      setHistory(prev => {
        const cutoff = Date.now() - maxWindowMinutes * 60_000;
        const filtered = prev.filter(p => p.timestamp.getTime() >= cutoff);
        return [...filtered, { timestamp: new Date(), raw: current }];
      });
    }, chartIntervalMs);
    return () => clearInterval(interval);
  }, [isActive, current, chartIntervalMs, maxWindowMinutes]);

  if (!isActive) {
    return (
      <div className="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg p-6">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">{title}</h3>
        <p className="text-sm text-gray-600 dark:text-gray-400">{emptyMessage}</p>
      </div>
    );
  }

  // Build grouped charts (do not early-return so single metrics can coexist)
  const groupBlocks = (groups && groups.length > 0) ? groups.map(group => {
    const rawSeries = history.map(point => {
      const base: Record<string, number> = {};
      group.metrics.forEach(m => { base[m.id] = m.value(point.raw); });
      return { timestamp: point.timestamp, ...base } as Record<string, unknown>;
    });
    // Adaptive units for network & block IO (bytes -> KB/MB/GB)
    let divisor = 1;
    let unit: string | undefined;
    if (group.id === 'network' || group.id === 'diskio') {
      const maxVal = Math.max(1, ...rawSeries.map(r => group.metrics.map(m => (r as Record<string, unknown>)[m.id] as number)).flat());
      const KB = 1024, MB = KB * 1024, GB = MB * 1024;
      if (maxVal >= GB) { divisor = GB; unit = 'GB'; }
      else if (maxVal >= MB) { divisor = MB; unit = 'MB'; }
      else if (maxVal >= KB) { divisor = KB; unit = 'KB'; }
      else { divisor = 1; unit = 'B'; }
    }
    const groupData = rawSeries.map(row => {
      const scaled: Record<string, unknown> = { timestamp: row.timestamp };
      group.metrics.forEach(m => {
        const raw = (row as Record<string, unknown>)[m.id] as number;
        scaled[m.id] = (raw / divisor);
      });
      return scaled;
    });
    return (
      <div key={group.id} className="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg overflow-hidden">
        <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700 flex items-center justify-between">
          <div className="flex items-center gap-2">
            {group.icon || (group.id === 'network' ? <Network className="h-4 w-4 text-blue-600 dark:text-blue-400" /> : <HardDrive className="h-4 w-4 text-blue-600 dark:text-blue-400" />)}
            <h4 className="text-lg font-semibold text-gray-900 dark:text-white">{group.label}</h4>
          </div>
          {current && (
            <div className="flex gap-2 flex-wrap">
              {group.metrics.map(m => (
                <span
                  key={m.id}
                  className="font-mono text-xs px-2 py-1 rounded-lg shadow-sm"
                  style={{
                    backgroundColor: m.color || '#3b82f6',
                    color: '#ffffff',
                  }}
                >
                  {(() => {
                    const raw = m.value(current);
                    const displayUnit = unit || m.unit || '';
                    const scaled = unit ? raw / divisor : raw;
                    if (m.format) {
                      return m.format(scaled);
                    }
                    if (typeof scaled === 'number' && !isNaN(scaled)) {
                      return scaled.toFixed(2) + ' ' + displayUnit;
                    }
                    return 'N/A';
                  })()}
                </span>
              ))}
            </div>
          )}
        </div>
        <div className="p-6">
          <ResponsiveContainer width="100%" height={180}>
            <LineChart data={groupData}>
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
                tickFormatter={(value) => unit ? `${(value as number).toFixed(1)} ${unit}` : (value as number).toFixed(1)}
              />
              <Tooltip
                labelFormatter={(time) => new Date(time).toLocaleTimeString()}
                contentStyle={{
                  backgroundColor: 'rgba(255, 255, 255, 0.95)',
                  border: '1px solid #e5e7eb',
                  borderRadius: '8px',
                }}
                formatter={(value: number, name: string) => {
                  const metricDef = group.metrics.find(m => m.id === name);
                  if (!metricDef) return [value, name];
                  const displayUnit = unit || metricDef.unit || '';
                  return [`${(value as number).toFixed(2)} ${displayUnit}`, metricDef.label];
                }}
              />
              {group.metrics.map(m => (
                hiddenMetrics[m.id] ? null : (
                  <Line
                    key={m.id}
                    type="monotone"
                    dataKey={m.id}
                    stroke={m.color || '#3b82f6'}
                    strokeWidth={2}
                    dot={false}
                    isAnimationActive={false}
                  />
                )
              ))}
            </LineChart>
          </ResponsiveContainer>
          <div className="mt-2 flex flex-wrap gap-2">
            {group.metrics.map(m => (
              <button
                key={m.id}
                type="button"
                onClick={() => setHiddenMetrics(h => ({ ...h, [m.id]: !h[m.id] }))}
                className={`text-xs font-mono px-2 py-1 rounded border flex items-center gap-1 transition-opacity ${hiddenMetrics[m.id] ? 'opacity-50' : 'opacity-100'}`}
                style={{
                  backgroundColor: (m.color || '#3b82f6') + '22',
                  borderColor: m.color || '#3b82f6',
                  color: m.color || '#3b82f6',
                }}
                title={hiddenMetrics[m.id] ? 'Afficher' : 'Masquer'}
              >
                <span className="w-3 h-3 rounded-sm" style={{ backgroundColor: m.color || '#3b82f6' }} />
                {m.label}
              </button>
            ))}
          </div>
        </div>
      </div>
    );
  }) : [];

  // Build single metric cards
  const metricBlocks = (metrics && metrics.length > 0) ? (() => {
    const chartData: Array<Record<string, unknown>> = history.map(point => {
      const base: Record<string, unknown> = { timestamp: point.timestamp };
      metrics.forEach(m => { base[m.id] = m.scale ? m.scale(m.value(point.raw)) : m.value(point.raw); });
      return base;
    });
    return metrics.map(metric => (
      <div key={metric.id} className="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg overflow-hidden">
        <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700 flex items-center justify-between">
          <div className="flex items-center gap-2">
            {metric.icon || <Cpu className="h-4 w-4 text-blue-600 dark:text-blue-400" />}
            <h4 className="text-lg font-semibold text-gray-900 dark:text-white">{metric.label}</h4>
          </div>
          {current && (
            <span
              className="font-mono text-xs px-3 py-1 rounded-lg shadow-sm"
              style={{ backgroundColor: metric.color || '#3b82f6', color: '#ffffff' }}
            >
              {metric.headerFormat
                ? metric.headerFormat(current, metric)
                : metric.format
                  ? metric.format(metric.value(current))
                  : metric.value(current).toFixed(2) + (metric.unit ? metric.unit : '')}
            </span>
          )}
        </div>
        <div className="p-6">
          <ResponsiveContainer width="100%" height={160}>
            <LineChart data={chartData}>
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
                tickFormatter={(value) => metric.unit ? `${value.toFixed(1)} ${metric.unit}` : value.toFixed(1)}
              />
              <Tooltip
                labelFormatter={(time) => new Date(time).toLocaleTimeString()}
                formatter={(value: number) => [
                  metric.unit ? `${(value as number).toFixed(2)} ${metric.unit}` : (value as number).toFixed(2),
                  metric.label,
                ]}
                contentStyle={{
                  backgroundColor: 'rgba(255, 255, 255, 0.95)',
                  border: '1px solid #e5e7eb',
                  borderRadius: '8px',
                }}
              />
              <Line
                type="monotone"
                dataKey={metric.id}
                stroke={metric.color || '#3b82f6'}
                strokeWidth={2}
                dot={false}
                isAnimationActive={false}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </div>
    ));
  })() : [];

  if (!metricBlocks.length && !groupBlocks.length) return null;
  return (
    <div className="space-y-4">
      {metricBlocks}
      {groupBlocks}
      {headerExtras}
    </div>
  );
}

// Helper formatters
// Local helper (not exported to avoid fast refresh warning about non-component exports)
// (Reserved helper placeholder removed to satisfy lint: if needed reintroduce where used)
// Example of using formatBytes inside metric.format if needed by parent.