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
        await signalRService.streamContainerLogs(containerId, tail);
      } else if (projectPath) {
        await signalRService.streamComposeLogs(projectPath, serviceName || undefined, tail);
      }
    } catch (error: any) {
      toast.error(`Failed to start streaming: ${error.message}`);
      setIsStreaming(false);
    }
  };

  const handleStopStream = async () => {
    try {
      await signalRService.stopStream();
      await signalRService.disconnectFromLogsHub();
      setIsStreaming(false);
      toast.success('Log streaming stopped');
    } catch (error: any) {
      toast.error(`Failed to stop streaming: ${error.message}`);
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
    <div className="h-full flex flex-col">
      <div className="flex justify-between items-center mb-4">
        <h1 className="text-2xl font-bold">{getTitle()}</h1>
        <div className="flex items-center space-x-4">
          <div className="flex items-center space-x-2">
            <label className="text-sm font-medium">Tail:</label>
            <input
              type="number"
              value={tail}
              onChange={(e) => setTail(parseInt(e.target.value) || 100)}
              className="w-20 border rounded px-2 py-1 text-sm"
              disabled={isStreaming}
              min="10"
              max="1000"
            />
          </div>
          <button
            onClick={handleClearLogs}
            className="flex items-center space-x-2 px-3 py-1 bg-gray-500 text-white rounded hover:bg-gray-600"
            disabled={logs.length === 0}
          >
            <Trash2 className="w-4 h-4" />
            <span>Clear</span>
          </button>
          {!isStreaming ? (
            <button
              onClick={handleStartStream}
              className="flex items-center space-x-2 px-4 py-2 bg-green-500 text-white rounded hover:bg-green-600"
            >
              <Play className="w-4 h-4" />
              <span>Start Streaming</span>
            </button>
          ) : (
            <button
              onClick={handleStopStream}
              className="flex items-center space-x-2 px-4 py-2 bg-red-500 text-white rounded hover:bg-red-600"
            >
              <Square className="w-4 h-4" />
              <span>Stop Streaming</span>
            </button>
          )}
        </div>
      </div>

      <div className="flex-1 bg-gray-900 text-green-400 font-mono text-sm rounded-lg p-4 overflow-auto">
        {logs.length === 0 ? (
          <div className="text-gray-500 text-center py-8">
            {isStreaming ? 'Waiting for logs...' : 'No logs to display. Click "Start Streaming" to begin.'}
          </div>
        ) : (
          <div>
            {logs.map((log, index) => (
              <div key={index} className="mb-1 hover:bg-gray-800 px-2 py-1 rounded">
                <span className="text-gray-500 mr-3">{new Date(log.timestamp).toLocaleTimeString()}</span>
                <span className="text-green-400">{log.message}</span>
              </div>
            ))}
            <div ref={logsEndRef} />
          </div>
        )}
      </div>

      <div className="mt-4 flex items-center justify-between text-sm text-gray-600">
        <div>
          {isStreaming && (
            <span className="flex items-center">
              <span className="w-2 h-2 bg-green-500 rounded-full animate-pulse mr-2"></span>
              Streaming active
            </span>
          )}
        </div>
        <div>Total logs: {logs.length}</div>
      </div>
    </div>
  );
}
