import React from 'react';
import { CheckCircle, AlertCircle } from 'lucide-react';

interface ActivityItemProps {
  item: {
    id: number;
    username: string;
    action: string;
    resourceType: string;
    details: string;
    timestamp: string;
    success: boolean;
  };
}


const ActivityItemComponent = ({ item }: ActivityItemProps) => (
  <div className="p-5 hover:bg-white dark:hover:bg-gray-800 transition-all duration-200 group">
    <div className="flex items-start gap-4">
      <div className={`p-2 rounded-lg ${item.success ? 'bg-green-100 dark:bg-green-900/30' : 'bg-red-100 dark:bg-red-900/30'}`}>
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
);

export const ActivityItem = React.memo(ActivityItemComponent);
