import { cloneElement, isValidElement } from 'react';

interface HealthItemProps {
  label: string;
  state: { isHealthy: boolean; message: string };
  icon: React.ReactElement;
}

export const HealthItem = ({ label, state, icon }: HealthItemProps) => {
  const colorClass = state.isHealthy ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400';
  
  return (
    <div className="flex items-start gap-3 p-4 rounded-xl bg-white dark:bg-gray-800 border border-gray-100 dark:border-gray-700">
      <div className={`p-2 rounded-lg ${state.isHealthy ? 'bg-green-100 dark:bg-green-900/30' : 'bg-red-100 dark:bg-red-900/30'}`}>
        {isValidElement(icon) && cloneElement(icon, {
          className: `w-5 h-5 ${colorClass}`
        } as React.HTMLAttributes<SVGElement>)}
      </div>
      <div>
        <p className="font-semibold text-gray-900 dark:text-white mb-1">{label}</p>
        <p className="text-sm text-gray-600 dark:text-gray-400">{state.message}</p>
      </div>
    </div>
  );
};
