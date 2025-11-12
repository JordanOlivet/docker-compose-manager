import { useEffect, useState, useRef, useCallback } from 'react';
import { signalRService } from '@/services/signalRService';
import { FileText, Play, Pause, Trash2, AlertCircle } from 'lucide-react';

interface ComposeLogsProps {
  // Compose project streaming
  projectPath?: string;
  projectName?: string;
  // Single container streaming
  containerId?: string;
  containerName?: string;
}

interface LogEntry {
  timestamp: Date;
  service: string;
  message: string;
  raw: string;
}

export function ComposeLogs({ projectPath, projectName, containerId, containerName }: ComposeLogsProps) {
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [autoScroll, setAutoScroll] = useState(true);
  const logsEndRef = useRef<HTMLDivElement>(null);
  const scrollContainerRef = useRef<HTMLDivElement>(null);
  const lastScrollTop = useRef<number>(0);
  const streamingRef = useRef<boolean>(false); // Track if streaming is in progress

  // Service colors for visual identification
  const serviceColors = useRef<Map<string, string>>(new Map());
  const colorPalette = [
    'bg-blue-600',
    'bg-green-600',
    'bg-purple-600',
    'bg-orange-600',
    'bg-pink-600',
    'bg-cyan-600',
    'bg-indigo-600',
    'bg-teal-600',
    'bg-red-600',
    'bg-yellow-600',
  ];

  const getServiceColor = (serviceName: string): string => {
    if (!serviceColors.current.has(serviceName)) {
      const colorIndex = serviceColors.current.size % colorPalette.length;
      serviceColors.current.set(serviceName, colorPalette[colorIndex]);
    }
    return serviceColors.current.get(serviceName)!;
  };

  // Parse log line to extract service name, timestamp, and message
  const parseLogLine = useCallback((rawLog: string): LogEntry => {
    // Docker Compose format with --timestamps:
    // service-name  | 2024-01-15T10:30:45.123456789Z actual log message

    // Split by pipe
    const pipeIndex = rawLog.indexOf('|');

    if (pipeIndex === -1) {
      // Likely a container log line without compose formatting
      const svc = containerId ? (containerName || containerId.substring(0, 12)) : 'unknown';
      return {
        timestamp: new Date(),
        service: svc,
        message: rawLog,
        raw: rawLog,
      };
    }

    // Extract service name (before pipe) and content (after pipe)
    const serviceName = rawLog.substring(0, pipeIndex).trim();
    const content = rawLog.substring(pipeIndex + 1).trim();

    // Try to extract timestamp from content
    // Format: 2025-11-07T11:54:17.112938720Z message
    const timestampMatch = content.match(/^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z?)\s+(.*)$/);

    if (timestampMatch) {
      const timestamp = timestampMatch[1];
      const message = timestampMatch[2];

      return {
        timestamp: new Date(timestamp),
        service: serviceName,
        message: message,
        raw: rawLog,
      };
    }

    // No timestamp found, use entire content as message
    return {
      timestamp: new Date(),
      service: serviceName,
      message: content,
      raw: rawLog,
    };
  }, [containerId, containerName]);

  // Auto-scroll to bottom when new logs arrive
  useEffect(() => {
    if (autoScroll && logsEndRef.current && scrollContainerRef.current) {
      scrollContainerRef.current.scrollTop = scrollContainerRef.current.scrollHeight;
    }
  }, [logs, autoScroll]);

  // Handle logs received from SignalR
  const handleReceiveLogs = useCallback((logsText: string) => {
    // Clean up the logs - remove \r characters
    const cleanedText = logsText.replace(/\r/g, '');
    const lines = cleanedText.split('\n').filter(line => line.trim() !== '');
    console.log(`[RECEIVE] Received batch with ${lines.length} log lines`);

    const newLogs = lines.map(line => parseLogLine(line));

    setLogs(prevLogs => {
      const allLogs = [...prevLogs, ...newLogs];
      allLogs.sort((a, b) => a.timestamp.getTime() - b.timestamp.getTime());
      return allLogs;
    });
  }, [parseLogLine]);

  const handleLogError = useCallback((errorMsg: string) => {
    setError(errorMsg);
    console.error('Log streaming error:', errorMsg);
  }, []);

  const handleStreamComplete = useCallback(() => {
    console.log('Log stream completed');
    setIsStreaming(false);
  }, []);

  // Start streaming logs
  const startStreaming = useCallback(async (clearExisting = false) => {
    // Prevent multiple simultaneous streaming attempts
    if (streamingRef.current) {
      console.log('[STREAMING] Already streaming, skipping...');
      return;
    }

    streamingRef.current = true;

    try {
      setError(null);

      // Only clear logs if explicitly requested (e.g., on initial mount or after clear)
      if (clearExisting) {
        setLogs([]);
      }

      // Connect to logs hub if not already connected
      if (!signalRService.isLogsConnected()) {
        console.log('[STREAMING] Connecting to logs hub...');
        await signalRService.connectToLogsHub();
      }

      // IMPORTANT: Unregister old handlers first to prevent duplicates
      signalRService.offReceiveLogs(handleReceiveLogs);
      signalRService.offLogError(handleLogError);
      signalRService.offStreamComplete(handleStreamComplete);

      // Register event handlers
      signalRService.onReceiveLogs(handleReceiveLogs);
      signalRService.onLogError(handleLogError);
      signalRService.onStreamComplete(handleStreamComplete);

      // Start streaming (will wait for connection to be ready)
      if (containerId) {
        await signalRService.streamContainerLogs(containerId);
      } else if (projectPath) {
        await signalRService.streamComposeLogs(projectPath, undefined);
      } else {
        throw new Error('No source provided for logs (projectPath or containerId required)');
      }
      setIsStreaming(true);
      console.log('[STREAMING] Log streaming started successfully');
    } catch (err) {
      console.error('[STREAMING] Failed to start log streaming:', err);
      setError(`Failed to start streaming: ${err instanceof Error ? err.message : String(err)}`);
      setIsStreaming(false);
      streamingRef.current = false;
    }
  }, [handleReceiveLogs, handleLogError, handleStreamComplete, projectPath, containerId]);

  // Stop streaming logs
  const stopStreaming = useCallback(async () => {
    try {
      await signalRService.stopStream();

      signalRService.offReceiveLogs(handleReceiveLogs);
      signalRService.offLogError(handleLogError);
      signalRService.offStreamComplete(handleStreamComplete);

      setIsStreaming(false);
      streamingRef.current = false; // Reset streaming flag
    } catch (err) {
      console.error('Failed to stop log streaming:', err);
      setError(`Failed to stop streaming: ${err instanceof Error ? err.message : String(err)}`);
      streamingRef.current = false; // Reset on error too
    }
  }, [handleReceiveLogs, handleLogError, handleStreamComplete]);

  // Clear logs
  const clearLogs = () => {
    setLogs([]);
    setError(null);
  };

// Handle scroll for auto-scroll behavior and lazy loading
  useEffect(() => {
    const scrollContainer = scrollContainerRef.current;
    if (!scrollContainer) return;

    const handleScroll = () => {
      const { scrollTop, scrollHeight, clientHeight } = scrollContainer;

      // Disable auto-scroll when user scrolls up
      if (scrollTop < lastScrollTop.current) {
        setAutoScroll(false);
      }

      // Re-enable auto-scroll when scrolled to bottom
      if (scrollTop + clientHeight >= scrollHeight - 50) {
        setAutoScroll(true);
      }

      lastScrollTop.current = scrollTop;
    };

    scrollContainer.addEventListener('scroll', handleScroll);
    return () => scrollContainer.removeEventListener('scroll', handleScroll);
  });

  // Auto-start streaming on mount
  useEffect(() => {
    let isMounted = true;

    const initStreaming = async () => {
      if (isMounted) {
        console.log('[MOUNT] Component mounted, starting streaming...');
        await startStreaming(true); // Clear logs on initial mount (will skip if already streaming)
      }
    };

    initStreaming();

    // Cleanup on unmount
    return () => {
      console.log('[MOUNT] Component unmounting, cleaning up...');
      isMounted = false;

      // Unregister handlers
      signalRService.offReceiveLogs(handleReceiveLogs);
      signalRService.offLogError(handleLogError);
      signalRService.offStreamComplete(handleStreamComplete);

      // Stop streaming
      stopStreaming();
    };
  }, [projectPath, containerId, handleReceiveLogs, handleLogError, handleStreamComplete, startStreaming, stopStreaming]); // Re-init if source changes

  return (
    <div className="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg hover:shadow-2xl transition-all duration-300 overflow-hidden flex flex-col h-full">
      {/* Header */}
      <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <FileText className="h-5 w-5 text-gray-600 dark:text-gray-400" />
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
              {containerId ? `Container Logs - ${containerName || containerId.substring(0,12)}` : `Compose Logs - ${projectName}`}
            </h3>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={clearLogs}
              disabled={logs.length === 0}
              className="p-2 text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              title="Clear logs"
            >
              <Trash2 className="h-4 w-4" />
            </button>
            {isStreaming ? (
              <button
                onClick={() => void stopStreaming()}
                className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-white bg-yellow-600 hover:bg-yellow-700 rounded-lg transition-colors"
                title="Pause streaming (logs will be kept)"
              >
                <Pause className="h-4 w-4" />
                Pause
              </button>
            ) : (
              <button
                onClick={() => void startStreaming(true)}
                className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-white bg-green-600 hover:bg-green-700 rounded-lg transition-colors"
                title="Resume streaming"
              >
                <Play className="h-4 w-4" />
                Resume
              </button>
            )}
          </div>
        </div>
      </div>

      {/* Error Display */}
      {error && (
        <div className="mx-6 mt-4 p-3 bg-red-100 dark:bg-red-900/30 border border-red-300 dark:border-red-700 rounded-lg flex items-center gap-2">
          <AlertCircle className="h-4 w-4 text-red-600 dark:text-red-400" />
          <span className="text-sm text-red-700 dark:text-red-300">{error}</span>
        </div>
      )}

      {/* Logs Container - Flexible Height with Scroll */}
      <div
        ref={scrollContainerRef}
        className="flex-1 overflow-y-auto px-6 py-4 bg-gray-50 dark:bg-gray-900/50"
      >
        <div className="font-mono text-xs space-y-1">
          
          {logs.length === 0 ? (
            <div className="flex items-center justify-center h-full text-gray-500 dark:text-gray-400 py-20">
              {isStreaming ? (
                <div className="flex flex-col items-center gap-2">
                  <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-gray-500"></div>
                  <span>Waiting for logs...</span>
                </div>
              ) : (
                'Streaming paused. Click "Resume" to continue.'
              )}
            </div>
          ) : (
            logs.map((log, index) => (
              <div
                key={index}
                className="flex gap-2 hover:bg-gray-100 dark:hover:bg-gray-800 py-1 px-2 rounded transition-colors"
              >
                {/* Service identifier badge (only in compose mode) */}
                {!containerId && (
                  <div
                    className={`shrink-0 px-2 py-0.5 rounded text-white text-[10px] font-semibold flex items-center justify-center min-w-[100px] max-w-[140px] ${getServiceColor(
                      log.service
                    )}`}
                  >
                    <span className="truncate">{log.service}</span>
                  </div>
                )}

                {/* Log message (no timestamp display) */}
                <span className="text-gray-900 dark:text-gray-100 break-all flex-1">
                  {log.message}
                </span>
              </div>
            ))
          )}
          <div ref={logsEndRef} />
        </div>
      </div>

      {/* Footer */}
      <div className="border-t border-gray-200 dark:border-gray-700 px-6 py-3 bg-gray-50 dark:bg-gray-700/30">
        <div className="flex items-center justify-between">
          <div className="text-xs text-gray-600 dark:text-gray-400">
            {logs.length} log{logs.length !== 1 ? 's' : ''} displayed
          </div>
          <label className="flex items-center gap-2 text-xs text-gray-600 dark:text-gray-400 cursor-pointer">
            <input
              type="checkbox"
              checked={autoScroll}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setAutoScroll(e.target.checked)}
              className="w-4 h-4 text-blue-600 bg-gray-100 border-gray-300 rounded focus:ring-blue-500 dark:focus:ring-blue-600 dark:ring-offset-gray-800 focus:ring-2 dark:bg-gray-700 dark:border-gray-600"
            />
            Auto-scroll
          </label>
        </div>
      </div>
    </div>
  );
}
