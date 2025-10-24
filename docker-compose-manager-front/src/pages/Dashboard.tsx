import { useQuery } from '@tanstack/react-query';
import {
  Container,
  FileText,
  Users,
  Activity as ActivityIcon,
  CheckCircle,
  XCircle,
  AlertCircle,
  Package,
  PlayCircle,
  StopCircle,
  Database,
  HardDrive
} from 'lucide-react';
import dashboardApi from '../api/dashboard';

export function Dashboard() {
  // Fetch dashboard data
  const { data: stats, isLoading: statsLoading } = useQuery({
    queryKey: ['dashboard', 'stats'],
    queryFn: () => dashboardApi.getStats(),
    refetchInterval: 30000, // Refresh every 30s
  });

  const { data: activity, isLoading: activityLoading } = useQuery({
    queryKey: ['dashboard', 'activity'],
    queryFn: () => dashboardApi.getActivity(10),
    refetchInterval: 10000, // Refresh every 10s
  });

  const { data: health } = useQuery({
    queryKey: ['dashboard', 'health'],
    queryFn: () => dashboardApi.getHealth(),
    refetchInterval: 60000, // Refresh every minute
  });

  return (
    <div className="space-y-8">
      {/* Page Header */}
      <div className="mb-8">
        <h1 className="text-4xl font-bold text-gray-900 dark:text-white mb-3">Dashboard</h1>
        <p className="text-lg text-gray-600 dark:text-gray-400">
          Monitor your Docker environment at a glance
        </p>
      </div>

      {/* Health Status */}
      {health && (
        <div className="bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl shadow-lg border border-gray-100 dark:border-gray-700 overflow-hidden">
          <div className="flex items-center justify-between p-6 border-b border-gray-100 dark:border-gray-700 bg-white/50 dark:bg-gray-800/50">
            <h2 className="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-3">
              <div className="p-2 bg-blue-100 dark:bg-blue-900/30 rounded-lg">
                <ActivityIcon className="w-5 h-5 text-blue-600 dark:text-blue-400" />
              </div>
              System Health
            </h2>
            {health.overall ? (
              <div className="flex items-center gap-2 px-3 py-1.5 bg-green-100 dark:bg-green-900/30 rounded-full">
                <CheckCircle className="w-5 h-5 text-green-600 dark:text-green-400" />
                <span className="text-sm font-semibold text-green-700 dark:text-green-300">Healthy</span>
              </div>
            ) : (
              <div className="flex items-center gap-2 px-3 py-1.5 bg-red-100 dark:bg-red-900/30 rounded-full">
                <XCircle className="w-5 h-5 text-red-600 dark:text-red-400" />
                <span className="text-sm font-semibold text-red-700 dark:text-red-300">Unhealthy</span>
              </div>
            )}
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4 p-6">
            <div className="flex items-start gap-3 p-4 rounded-xl bg-white dark:bg-gray-800 border border-gray-100 dark:border-gray-700">
              <div className={`p-2 rounded-lg ${health.database.isHealthy ? 'bg-green-100 dark:bg-green-900/30' : 'bg-red-100 dark:bg-red-900/30'}`}>
                <Database className={`w-5 h-5 ${health.database.isHealthy ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`} />
              </div>
              <div>
                <p className="font-semibold text-gray-900 dark:text-white mb-1">Database</p>
                <p className="text-sm text-gray-600 dark:text-gray-400">{health.database.message}</p>
              </div>
            </div>

            <div className="flex items-start gap-3 p-4 rounded-xl bg-white dark:bg-gray-800 border border-gray-100 dark:border-gray-700">
              <div className={`p-2 rounded-lg ${health.docker.isHealthy ? 'bg-green-100 dark:bg-green-900/30' : 'bg-red-100 dark:bg-red-900/30'}`}>
                <Container className={`w-5 h-5 ${health.docker.isHealthy ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`} />
              </div>
              <div>
                <p className="font-semibold text-gray-900 dark:text-white mb-1">Docker</p>
                <p className="text-sm text-gray-600 dark:text-gray-400">{health.docker.message}</p>
              </div>
            </div>

            <div className="flex items-start gap-3 p-4 rounded-xl bg-white dark:bg-gray-800 border border-gray-100 dark:border-gray-700">
              <div className={`p-2 rounded-lg ${health.composePaths.isHealthy ? 'bg-green-100 dark:bg-green-900/30' : 'bg-red-100 dark:bg-red-900/30'}`}>
                <HardDrive className={`w-5 h-5 ${health.composePaths.isHealthy ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`} />
              </div>
              <div>
                <p className="font-semibold text-gray-900 dark:text-white mb-1">Compose Paths</p>
                <p className="text-sm text-gray-600 dark:text-gray-400">{health.composePaths.message}</p>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Statistics Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 lg:gap-8">
        {/* Containers Stats */}
        <StatsCard
          icon={<Container className="w-8 h-8 text-blue-500" />}
          title="Total Containers"
          value={stats?.totalContainers ?? 0}
          subtitle={
            <div className="flex items-center gap-3 text-sm">
              <span className="flex items-center gap-1 text-green-600 dark:text-green-400">
                <PlayCircle className="w-4 h-4" />
                {stats?.runningContainers ?? 0} running
              </span>
              <span className="flex items-center gap-1 text-gray-600 dark:text-gray-400">
                <StopCircle className="w-4 h-4" />
                {stats?.stoppedContainers ?? 0} stopped
              </span>
            </div>
          }
          loading={statsLoading}
        />

        {/* Compose Projects */}
        <StatsCard
          icon={<Package className="w-8 h-8 text-purple-500" />}
          title="Compose Projects"
          value={stats?.totalComposeProjects ?? 0}
          subtitle={`${stats?.activeProjects ?? 0} active`}
          loading={statsLoading}
        />

        {/* Compose Files */}
        <StatsCard
          icon={<FileText className="w-8 h-8 text-green-500" />}
          title="Compose Files"
          value={stats?.composeFilesCount ?? 0}
          subtitle="Total files tracked"
          loading={statsLoading}
        />

        {/* Users */}
        <StatsCard
          icon={<Users className="w-8 h-8 text-orange-500" />}
          title="Users"
          value={stats?.usersCount ?? 0}
          subtitle={`${stats?.activeUsersCount ?? 0} active`}
          loading={statsLoading}
        />
      </div>

      {/* Recent Activity */}
      <div className="bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl shadow-lg border border-gray-100 dark:border-gray-700 overflow-hidden">
        <div className="p-6 border-b border-gray-100 dark:border-gray-700 bg-white/50 dark:bg-gray-800/50">
          <h2 className="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-3">
            <div className="p-2 bg-purple-100 dark:bg-purple-900/30 rounded-lg">
              <ActivityIcon className="w-5 h-5 text-purple-600 dark:text-purple-400" />
            </div>
            Recent Activity
          </h2>
        </div>

        <div className="divide-y divide-gray-100 dark:divide-gray-700">
          {activityLoading && (
            <div className="p-8 text-center">
              <div className="inline-block animate-spin rounded-full h-8 w-8 border-4 border-gray-300 dark:border-gray-600 border-t-blue-600"></div>
              <p className="text-gray-600 dark:text-gray-400 mt-3">Loading activity...</p>
            </div>
          )}

          {!activityLoading && activity && activity.length === 0 && (
            <div className="p-8 text-center">
              <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-gray-100 dark:bg-gray-800 mb-3">
                <ActivityIcon className="w-8 h-8 text-gray-400" />
              </div>
              <p className="text-gray-600 dark:text-gray-400">No recent activity</p>
            </div>
          )}

          {!activityLoading && activity && activity.map((item) => (
            <div key={item.id} className="p-5 hover:bg-white dark:hover:bg-gray-800 transition-all duration-200 group">
              <div className="flex items-start gap-4">
                <div className={`p-2 rounded-lg ${
                  item.success
                    ? 'bg-green-100 dark:bg-green-900/30'
                    : 'bg-red-100 dark:bg-red-900/30'
                }`}>
                  {item.success ? (
                    <CheckCircle className="w-5 h-5 text-green-600 dark:text-green-400" />
                  ) : (
                    <AlertCircle className="w-5 h-5 text-red-600 dark:text-red-400" />
                  )}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <p className="text-sm font-semibold text-gray-900 dark:text-white">
                      {item.username}
                    </p>
                    <span className="text-xs px-2.5 py-0.5 rounded-full bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300 font-medium">
                      {item.resourceType}
                    </span>
                  </div>
                  <p className="text-sm text-gray-600 dark:text-gray-400 mb-1">
                    <span className="font-medium text-gray-700 dark:text-gray-300">{item.action}</span> - {item.details}
                  </p>
                  <p className="text-xs text-gray-500 dark:text-gray-500">
                    {new Date(item.timestamp).toLocaleString()}
                  </p>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

// Helper component for stats cards
interface StatsCardProps {
  icon: React.ReactNode;
  title: string;
  value: number;
  subtitle: React.ReactNode | string;
  loading?: boolean;
}

function StatsCard({ icon, title, value, subtitle, loading }: StatsCardProps) {
  return (
    <div className="relative overflow-hidden bg-gradient-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl shadow-lg hover:shadow-2xl transition-all duration-300 hover:-translate-y-1 border border-gray-100 dark:border-gray-700">
      {/* Background decoration */}
      <div className="absolute top-0 right-0 w-32 h-32 bg-gradient-to-br from-blue-500/5 to-purple-500/5 rounded-full -mr-16 -mt-16"></div>

      <div className="relative p-6">
        <div className="flex items-center justify-between mb-4">
          <div className="p-3 rounded-xl bg-gradient-to-br from-blue-50 to-blue-100 dark:from-blue-900/30 dark:to-blue-800/30 shadow-sm">
            {icon}
          </div>
        </div>

        {loading ? (
          <div className="animate-pulse">
            <div className="h-8 bg-gray-200 dark:bg-gray-700 rounded-lg w-20 mb-3"></div>
            <div className="h-4 bg-gray-200 dark:bg-gray-700 rounded-lg w-28"></div>
          </div>
        ) : (
          <>
            <p className="text-4xl font-bold text-gray-900 dark:text-white mb-2 tracking-tight">
              {value.toLocaleString()}
            </p>
            <p className="text-sm font-semibold text-gray-600 dark:text-gray-400 mb-3">{title}</p>
            {typeof subtitle === 'string' ? (
              <p className="text-xs text-gray-500 dark:text-gray-500">{subtitle}</p>
            ) : (
              <div className="mt-2">{subtitle}</div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
