// Canonical log types shared by the log viewer, its stream controller and the API
// client. Mirrors the backend LogEntryDto / LogPageDto / AttachedContainerDto.

export type LogStream = 'stdout' | 'stderr';

export interface LogEntry {
  /** RFC3339Nano timestamp from Docker; also the pagination cursor. Empty when unparseable. */
  timestamp: string;
  containerId: string;
  containerName: string;
  service: string | null;
  stream: LogStream;
  message: string;
}

/** One page of historical logs, ascending by timestamp. */
export interface LogPage {
  entries: LogEntry[];
  hasMore: boolean;
}

/** A container currently attached to a compose log stream (drives filter chips). */
export interface AttachedContainer {
  id: string;
  name: string;
  service: string | null;
  state: string;
}

/** Payload of the SSE `logs` event. */
export interface LogsEventPayload {
  entries: LogEntry[];
}

/** Payload of the SSE `containers` event (compose streams only). */
export interface ContainersEventPayload {
  containers: AttachedContainer[];
}
