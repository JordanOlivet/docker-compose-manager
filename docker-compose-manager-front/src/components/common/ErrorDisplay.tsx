import { AlertCircle, RefreshCw } from 'lucide-react';
import { t } from '../../i18n';

interface ErrorDisplayProps {
  title?: string;
  message: string;
  onRetry?: () => void;
  className?: string;
}

export const ErrorDisplay = ({
  title = t('common.error'),
  message,
  onRetry,
  className = ''
}: ErrorDisplayProps) => {
  return (
    <div className={`rounded-lg border border-red-200 bg-red-50 p-6 ${className}`}>
      <div className="flex items-start gap-3">
        <AlertCircle className="w-5 h-5 text-red-600 mt-0.5 flex-shrink-0" />
        <div className="flex-1">
          <h3 className="text-sm font-semibold text-red-900 mb-1">{title}</h3>
          <p className="text-sm text-red-700">{message}</p>
          {onRetry && (
            <button
              onClick={onRetry}
              className="mt-3 flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-red-700 bg-white border border-red-300 rounded-md hover:bg-red-50 transition-colors"
            >
              <RefreshCw className="w-4 h-4" />
              {t('common.retry')}
            </button>
          )}
        </div>
      </div>
    </div>
  );
};
