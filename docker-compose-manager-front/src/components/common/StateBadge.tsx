import { Circle } from 'lucide-react';

interface StateBadgeProps {
  status: string;
  size?: 'sm' | 'md' | 'lg';
  showIcon?: boolean;
  className?: string;
}

const statusStyles: Record<string, { bg: string; text: string; border: string }> = {
  // Operation Status
  pending: { bg: 'bg-gray-100', text: 'text-gray-700', border: 'border-gray-300' },
  running: { bg: 'bg-green-100', text: 'text-green-700', border: 'border-green-300' },
  completed: { bg: 'bg-green-100', text: 'text-green-700', border: 'border-green-300' },
  failed: { bg: 'bg-red-100', text: 'text-red-700', border: 'border-red-300' },
  cancelled: { bg: 'bg-yellow-100', text: 'text-yellow-700', border: 'border-yellow-300' },

  // Container Status
  exited: { bg: 'bg-gray-100', text: 'text-gray-700', border: 'border-gray-300' },
  paused: { bg: 'bg-yellow-100', text: 'text-yellow-700', border: 'border-yellow-300' },
  restarting: { bg: 'bg-orange-100', text: 'text-orange-700', border: 'border-orange-300' },
  dead: { bg: 'bg-red-100', text: 'text-red-700', border: 'border-red-300' },
  created: { bg: 'bg-blue-100', text: 'text-blue-700', border: 'border-blue-300' },

  // Project Status
  stopped: { bg: 'bg-gray-100', text: 'text-gray-700', border: 'border-gray-300' },
  down: { bg: 'bg-gray-100', text: 'text-gray-700', border: 'border-gray-300' },
  partial: { bg: 'bg-yellow-100', text: 'text-yellow-700', border: 'border-yellow-300' },
  unknown: { bg: 'bg-gray-100', text: 'text-gray-700', border: 'border-gray-300' },

  // Discovered/Manual
  discovered: { bg: 'bg-blue-100', text: 'text-blue-700', border: 'border-blue-300' },
  manual: { bg: 'bg-purple-100', text: 'text-purple-700', border: 'border-purple-300' },
};

const sizeClasses = {
  sm: { container: 'text-xs px-2 py-0.5', icon: 'w-2 h-2' },
  md: { container: 'text-sm px-2.5 py-1', icon: 'w-3 h-3' },
  lg: { container: 'text-base px-3 py-1.5', icon: 'w-4 h-4' },
};

export const StateBadge = ({
  status,
  size = 'md',
  showIcon = true,
  className = '',
}: StateBadgeProps) => {
  const normalizedStatus = status.toString().toLowerCase().replace(/\s+/g, '-');
  const styles = statusStyles[normalizedStatus] || statusStyles.unknown;
  const sizes = sizeClasses[size];

  return (
    <span
      className={`inline-flex items-center gap-1.5 font-medium rounded-full ${styles.bg} ${styles.text} ${styles.border} ${sizes.container} ${className}`}
    >
      {showIcon && <Circle className={`${sizes.icon} fill-current`} />}
      <span className="capitalize">{status}</span>
    </span>
  );
};
