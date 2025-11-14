import React from 'react';

interface StatsCardProps {
  icon: React.ReactNode;
  title: string;
  value: number;
  subtitle: React.ReactNode | string;
  loading?: boolean;
}

export const StatsCard = ({ icon, title, value, subtitle, loading }: StatsCardProps) => {
  return (
    <div className="relative overflow-hidden bg-linear-to-br from-white to-gray-50 dark:from-gray-800 dark:to-gray-900 rounded-2xl shadow-lg hover:shadow-2xl transition-all duration-300 hover:-translate-y-1 border border-gray-100 dark:border-gray-700">
      {/* Background decoration */}
      <div className="absolute top-0 right-0 w-32 h-32 bg-linear-to-br from-blue-500/5 to-purple-500/5 rounded-full -mr-16 -mt-16"></div>
      <div className="relative p-6">
        <div className="flex items-center justify-between mb-4">
          <div className="p-3 rounded-xl bg-linear-to-br from-blue-50 to-blue-100 dark:from-blue-900/30 dark:to-blue-800/30 shadow-sm">
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
};
