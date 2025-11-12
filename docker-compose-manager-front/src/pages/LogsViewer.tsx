import { useState, useEffect, useRef } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Play, Square, Trash2 } from 'lucide-react';
import { signalRService } from '../services/signalRService';
import { useToast } from '../hooks/useToast';

type LogEntry = {
  timestamp: string;
  message: string;
};

export default function LogsViewer() {
  const [searchParams] = useSearchParams();
  const containerId = searchParams.get('containerId');
  const projectPath = searchParams.get('projectPath');
  const serviceName = searchParams.get('service');

  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [tail, setTail] = useState(100);
  const logsEndRef = useRef<HTMLDivElement>(null);
  const toast = useToast();

  const scrollToBottom = () => {
    logsEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    scrollToBottom();
  }, [logs]);

  const handleStartStream = async () => {
    try {
      setLogs([]);
      setIsStreaming(true);

      // Connect to logs hub
      await signalRService.connectToLogsHub();

      // Set up log receiver
      signalRService.onReceiveLogs((logLine: string) => {
        const entry: LogEntry = {
          timestamp: new Date().toISOString(),
          message: logLine,
        };
        setLogs((prev) => [...prev, entry]);
      });

      // Handle errors
      signalRService.onLogError((error: string) => {
        toast.error(`Log streaming error: ${error}`);
        setIsStreaming(false);
      });

      // Handle stream complete
      signalRService.onStreamComplete(() => {
        toast.success('Log streaming completed');
        setIsStreaming(false);
      });

      // Start streaming
      if (containerId) {
        await signalRService.streamContainerLogs(containerId);
      } else if (projectPath) {
        await signalRService.streamComposeLogs(projectPath, serviceName || undefined);
      }
    } catch (error: unknown) {
      const err = error as Error;
      toast.error(`Failed to start streaming: ${err.message}`);
      setIsStreaming(false);
    }
  };

  const handleStopStream = async () => {
    try {
      await signalRService.stopStream();
      await signalRService.disconnectFromLogsHub();
      setIsStreaming(false);
      toast.success('Log streaming stopped');
    } catch (error: unknown) {
      const err = error as Error;
      toast.error(`Failed to stop streaming: ${err.message}`);
    }
  };

  const handleClearLogs = () => {
    setLogs([]);
  };

  useEffect(() => {
    // Cleanup on unmount
    return () => {
      if (isStreaming) {
        signalRService.stopStream();
        signalRService.disconnectFromLogsHub();
      }
    };
  }, [isStreaming]);

  const getTitle = () => {
    if (containerId) return `Container Logs: ${containerId.substring(0, 12)}`;
    if (projectPath && serviceName) return `Compose Logs: ${projectPath} / ${serviceName}`;
    if (projectPath) return `Compose Logs: ${projectPath}`;
    return 'Logs Viewer';
  };

  return (
    <div className="h-full flex flex-col space-y-6">
      {/* Page Header */}
      <div className="mb-4">
        <h1 className="text-4xl font-bold text-gray-900 dark:text-white mb-3">{getTitle()}</h1>
        <p className="text-lg text-gray-600 dark:text-gray-400">
          Stream and view real-time logs from your containers
        </p>
      </div>

      {/* Controls */}
      <div className="flex justify-between items-center p-4 bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm">
        <div className="flex items-center space-x-4">
          <div className="flex items-center space-x-2">
            <label className="text-sm font-medium text-gray-700 dark:text-gray-300">Tail:</label>
            <input
              type="number"
              value={tail}
              onChange={(e) => setTail(parseInt(e.target.value) || 100)}
              className="w-20 border border-gray-300 dark:border-gray-600 rounded-lg px-2 py-1 text-sm bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 dark:focus:ring-blue-400"
              disabled={isStreaming}
              min="10"
              max="1000"
            />
          </div>
        </div>
        <div className="flex items-center space-x-2">
          <button
            onClick={handleClearLogs}
            className="flex items-center space-x-2 px-3 py-2 bg-gray-500 dark:bg-gray-600 text-white rounded-lg hover:bg-gray-600 dark:hover:bg-gray-500 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            disabled={logs.length === 0}
          >
            <Trash2 className="w-4 h-4" />
            <span>Clear</span>
          </button>
          {!isStreaming ? (
            <button
              onClick={handleStartStream}
              className="flex items-center space-x-2 px-4 py-2 bg-green-600 dark:bg-green-700 text-white rounded-lg hover:bg-green-700 dark:hover:bg-green-600 transition-colors"
            >
              <Play className="w-4 h-4" />
              <span>Start Streaming</span>
            </button>
          ) : (
            <button
              onClick={handleStopStream}
              className="flex items-center space-x-2 px-4 py-2 bg-red-600 dark:bg-red-700 text-white rounded-lg hover:bg-red-700 dark:hover:bg-red-600 transition-colors"
            >
              <Square className="w-4 h-4" />
              <span>Stop Streaming</span>
            </button>
          )}
        </div>
      </div>

      {/* Logs Terminal */}
      <div className="flex-1 bg-gray-900 dark:bg-gray-950 text-green-400 dark:text-green-300 font-mono text-sm rounded-2xl p-4 overflow-auto border border-gray-700 dark:border-gray-800 shadow-2xl">
        {logs.length === 0 ? (
          <div className="text-gray-500 dark:text-gray-400 text-center py-8 flex flex-col items-center">
            <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-gray-800 dark:bg-gray-900 mb-3">
              <Play className="w-8 h-8 text-gray-600 dark:text-gray-500" />
            </div>
            <p className="text-lg font-medium">{isStreaming ? 'Waiting for logs...' : 'No logs to display'}</p>
            <p className="text-sm mt-1">{isStreaming ? '' : 'Click "Start Streaming" to begin.'}</p>
          </div>
        ) : (
          <div>
            {logs.map((log, index) => (
              <div key={index} className="mb-1 hover:bg-gray-800 dark:hover:bg-gray-900 px-2 py-1 rounded transition-colors">
                <span className="text-gray-500 dark:text-gray-400 mr-3">{new Date(log.timestamp).toLocaleTimeString()}</span>
                <span className="text-green-400 dark:text-green-300">{log.message}</span>
              </div>
            ))}
            <div ref={logsEndRef} />
          </div>
        )}
      </div>

      {/* Status Bar */}
      <div className="flex items-center justify-between p-4 bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm">
        <div>
          {isStreaming && (
            <span className="flex items-center text-sm text-gray-700 dark:text-gray-300">
              <span className="w-2 h-2 bg-green-500 dark:bg-green-400 rounded-full animate-pulse mr-2"></span>
              Streaming active
            </span>
          )}
          {!isStreaming && logs.length > 0 && (
            <span className="text-sm text-gray-500 dark:text-gray-400">Streaming stopped</span>
          )}
        </div>
        <div className="text-sm text-gray-600 dark:text-gray-400">
          Total logs: <span className="font-semibold text-gray-900 dark:text-white">{logs.length}</span>
        </div>
      </div>
    </div>
  );
}
