import { t } from '../../i18n';

export const LoadingState = ({ message = t('common.loading') }: { message?: string }) => (
  <div className="p-8 text-center" role="status" aria-live="polite">
    <div className="inline-block animate-spin rounded-full h-8 w-8 border-4 border-gray-300 dark:border-gray-600 border-t-blue-600" aria-hidden="true"></div>
    <p className="text-gray-600 dark:text-gray-400 mt-3">{message}</p>
  </div>
);
