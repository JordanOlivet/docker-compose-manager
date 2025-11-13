import { APP_VERSION, getShortCommit, BUILD_DATE } from '@/utils/version'

interface VersionInfoProps {
  showDetails?: boolean
  className?: string
}

/**
 * Component to display application version information
 * Can be used in footer, about page, or settings
 */
export function VersionInfo({ showDetails = false, className = '' }: VersionInfoProps) {
  const shortCommit = getShortCommit()
  const buildDate = new Date(BUILD_DATE).toLocaleDateString()

  if (!showDetails) {
    // Simple badge version
    return (
      <span
        className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 dark:bg-gray-700 text-gray-800 dark:text-gray-200 ${className}`}
        title={`Build: ${buildDate} | Commit: ${shortCommit}`}
      >
        v{APP_VERSION}
      </span>
    )
  }

  // Detailed version info
  return (
    <div className={`text-sm space-y-1 ${className}`}>
      <div className="flex items-center gap-2">
        <span className="font-semibold text-gray-700 dark:text-gray-300">Version:</span>
        <span className="text-gray-600 dark:text-gray-400">{APP_VERSION}</span>
      </div>
      <div className="flex items-center gap-2">
        <span className="font-semibold text-gray-700 dark:text-gray-300">Build Date:</span>
        <span className="text-gray-600 dark:text-gray-400">{buildDate}</span>
      </div>
      <div className="flex items-center gap-2">
        <span className="font-semibold text-gray-700 dark:text-gray-300">Git Commit:</span>
        <span className="font-mono text-xs text-gray-600 dark:text-gray-400">{shortCommit}</span>
      </div>
    </div>
  )
}
