import { useQuery } from '@tanstack/react-query';
import {
  Container,
  FileText,
  Users,
  Activity as ActivityIcon,
  CheckCircle,
  XCircle,
  Package,
  PlayCircle,
  StopCircle,
  Database,
  HardDrive
} from 'lucide-react';
import { dashboardApi, type Activity } from '../api/dashboard';
import { StatsCard } from '../components/common/StatsCard';
import { HealthItem } from '../components/common/HealthItem';
import { ActivityItem } from '../components/common/ActivityItem';
import { LoadingState } from '../components/common/LoadingState';
import { t } from '../i18n';

function Dashboard() {
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
        <h1 className="text-4xl font-bold text-gray-900 dark:text-white mb-3">{t('dashboard.title')}</h1>
        <p className="text-lg text-gray-600 dark:text-gray-400">
          {t('dashboard.subtitle')}
        </p>
      </div>

      {/* Health Status */}
      {health && (
        <div className="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl shadow-lg border border-gray-100 dark:border-gray-700 overflow-hidden">
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
                <span className="text-sm font-semibold text-green-700 dark:text-green-300">{t('dashboard.healthy')}</span>
              </div>
            ) : (
              <div className="flex items-center gap-2 px-3 py-1.5 bg-red-100 dark:bg-red-900/30 rounded-full">
                <XCircle className="w-5 h-5 text-red-600 dark:text-red-400" />
                <span className="text-sm font-semibold text-red-700 dark:text-red-300">{t('dashboard.unhealthy')}</span>
              </div>
            )}
          </div>

           <div className="grid grid-cols-1 md:grid-cols-3 gap-4 p-6">
             <HealthItem label={t('dashboard.database')} state={health.database} icon={<Database />} />
             <HealthItem label={t('dashboard.docker')} state={health.docker} icon={<Container />} />
             <HealthItem label={t('dashboard.composePaths')} state={health.composePaths} icon={<HardDrive />} />
           </div>
        </div>
      )}

      {/* Statistics Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 lg:gap-8">
        {/* Containers Stats */}
        <StatsCard
          icon={<Container className="w-8 h-8 text-blue-500" />}
          title={t('dashboard.totalContainers')}
          value={stats?.totalContainers ?? 0}
          subtitle={
            <div className="flex items-center gap-3 text-sm">
              <span className="flex items-center gap-1 text-green-600 dark:text-green-400">
                <PlayCircle className="w-4 h-4" />
                {stats?.runningContainers ?? 0} {t('dashboard.running')}
              </span>
              <span className="flex items-center gap-1 text-gray-600 dark:text-gray-400">
                <StopCircle className="w-4 h-4" />
                {stats?.stoppedContainers ?? 0} {t('dashboard.stopped')}
              </span>
            </div>
          }
          loading={statsLoading}
        />

        {/* Compose Projects */}
        <StatsCard
          icon={<Package className="w-8 h-8 text-purple-500" />}
          title={t('dashboard.composeProjects')}
          value={stats?.totalComposeProjects ?? 0}
          subtitle={`${stats?.activeProjects ?? 0} ${t('dashboard.active')}`}
          loading={statsLoading}
        />

        {/* Compose Files */}
        <StatsCard
          icon={<FileText className="w-8 h-8 text-green-500" />}
          title={t('dashboard.composeFiles')}
          value={stats?.composeFilesCount ?? 0}
          subtitle={t('dashboard.totalFilesTracked')}
          loading={statsLoading}
        />

        {/* Users */}
        <StatsCard
          icon={<Users className="w-8 h-8 text-orange-500" />}
          title={t('dashboard.users')}
          value={stats?.usersCount ?? 0}
          subtitle={`${stats?.activeUsersCount ?? 0} ${t('dashboard.active')}`}
          loading={statsLoading}
        />
      </div>

      {/* Recent Activity */}
      <div className="bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl shadow-lg border border-gray-100 dark:border-gray-700 overflow-hidden">
        <div className="p-6 border-b border-gray-100 dark:border-gray-700 bg-white/50 dark:bg-gray-800/50">
          <h2 className="text-lg font-bold text-gray-900 dark:text-white flex items-center gap-3">
            <div className="p-2 bg-purple-100 dark:bg-purple-900/30 rounded-lg">
              <ActivityIcon className="w-5 h-5 text-purple-600 dark:text-purple-400" />
            </div>
            Recent Activity
          </h2>
        </div>

        <div className="divide-y divide-gray-100 dark:divide-gray-700">
           {activityLoading && <LoadingState message={t('dashboard.loadingActivity')} />}

          {!activityLoading && activity && activity.length === 0 && (
            <div className="p-8 text-center">
              <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-gray-100 dark:bg-gray-800 mb-3">
                <ActivityIcon className="w-8 h-8 text-gray-400" />
              </div>
              <p className="text-gray-600 dark:text-gray-400">{t('dashboard.noActivity')}</p>
            </div>
          )}

           {!activityLoading && activity && activity.map((item: Activity) => (
             <ActivityItem key={item.id} item={item} />
           ))}
        </div>
      </div>
    </div>
  );
}

export default Dashboard;

// ... StatsCard is now imported from common ...
