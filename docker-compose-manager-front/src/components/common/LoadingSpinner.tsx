import { Loader2 } from 'lucide-react';

interface LoadingSpinnerProps {
  size?: 'sm' | 'md' | 'lg';
  text?: string;
  className?: string;
}

const sizeClasses = {
  sm: 'w-4 h-4',
  md: 'w-8 h-8',
  lg: 'w-12 h-12',
};

export const LoadingSpinner = ({ size = 'md', text, className = '' }: LoadingSpinnerProps) => {
  return (
    <div
      role="status"
      aria-live="polite"
      aria-label={text || "Loading"}
      className={`flex flex-col items-center justify-center gap-3 ${className}`}
    >
      <Loader2 className={`${sizeClasses[size]} animate-spin text-blue-600`} aria-hidden="true" />
      {text && <p className="text-sm text-gray-600">{text}</p>}
    </div>
  );
};
