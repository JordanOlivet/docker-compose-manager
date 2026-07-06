import { refreshAccessToken } from '$lib/api/tokenRefresh';
import { isExpired } from '$lib/utils/jwt';
import { buildAppLogStreamUrl, type AppLogEntry, type AppLogFilter } from '$lib/api/appLogs';
import { logger } from '$lib/utils/logger';

export type AppLogStreamStatus = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

/** Minimal EventSource surface, so tests can inject a fake. */
export interface EventSourceLike {
  addEventListener(type: string, listener: (event: MessageEvent) => void): void;
  close(): void;
  onerror: ((event: Event) => void) | null;
}

export interface AppLogStreamOptions {
  tail?: number;
  maxEntries?: number;
  eventSourceFactory?: (url: string) => EventSourceLike;
  tokenProvider?: () => Promise<string | null>;
}

const DEFAULT_TAIL = 200;
const DEFAULT_MAX_ENTRIES = 5000;
const HEARTBEAT_TIMEOUT_MS = 90_000;
const MAX_RECONNECT_ATTEMPTS = 10;

function entryKey(entry: AppLogEntry): string {
  return `${entry.timestamp} ${entry.level} ${entry.category ?? ''} ${entry.message}`;
}

async function defaultTokenProvider(): Promise<string | null> {
  const token = localStorage.getItem('accessToken');
  if (token && !isExpired(token, 60_000)) {
    return token;
  }
  return refreshAccessToken();
}

/**
 * Owns a single application-log EventSource. The server applies the active filter,
 * so changing the filter tears down and reconnects the stream (each connection first
 * replays the last `tail` matching lines, then follows live events).
 */
export class AppLogStreamController {
  entries = $state<AppLogEntry[]>([]);
  status = $state<AppLogStreamStatus>('idle');
  error = $state<string | null>(null);
  paused = $state(false);

  readonly #tail: number;
  readonly #maxEntries: number;
  readonly #createEventSource: (url: string) => EventSourceLike;
  readonly #getToken: () => Promise<string | null>;

  #filter: AppLogFilter = {};
  #eventSource: EventSourceLike | null = null;
  #reconnectAttempt = 0;
  #heartbeatTimer: ReturnType<typeof setTimeout> | null = null;
  #reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  #pausedBuffer: AppLogEntry[] = [];
  #destroyed = false;

  constructor(options: AppLogStreamOptions = {}) {
    this.#tail = options.tail ?? DEFAULT_TAIL;
    this.#maxEntries = options.maxEntries ?? DEFAULT_MAX_ENTRIES;
    this.#createEventSource =
      options.eventSourceFactory ?? ((url) => new EventSource(url) as unknown as EventSourceLike);
    this.#getToken = options.tokenProvider ?? defaultTokenProvider;
  }

  /** (Re)starts the stream with the given filter, replacing any existing connection. */
  start(filter: AppLogFilter): void {
    if (this.#destroyed) return;
    this.#filter = filter;
    this.#reconnectAttempt = 0;
    this.entries = [];
    this.#pausedBuffer = [];
    this.#closeEventSource();
    this.#clearTimers();
    void this.#connect();
  }

  pause(): void {
    this.paused = true;
  }

  resume(): void {
    if (!this.paused) return;
    this.paused = false;
    if (this.#pausedBuffer.length > 0) {
      const buffered = this.#pausedBuffer;
      this.#pausedBuffer = [];
      this.#appendLive(buffered);
    }
  }

  clear(): void {
    this.entries = [];
    this.#pausedBuffer = [];
  }

  stop(): void {
    this.status = 'disconnected';
    this.#closeEventSource();
    this.#clearTimers();
  }

  destroy(): void {
    this.#destroyed = true;
    this.stop();
  }

  // --- internals ---

  async #connect(): Promise<void> {
    if (this.#destroyed) return;
    this.status = this.#reconnectAttempt > 0 ? 'reconnecting' : 'connecting';
    this.error = null;

    const token = await this.#getToken();
    if (this.#destroyed) return;
    if (!token) {
      this.error = 'authentication failed';
      this.status = 'disconnected';
      return;
    }

    const url = buildAppLogStreamUrl(token, { ...this.#filter, tail: this.#tail });
    const es = this.#createEventSource(url);
    this.#eventSource = es;

    es.addEventListener('connected', () => {
      this.status = 'connected';
      this.#reconnectAttempt = 0;
      this.#resetHeartbeat();
    });

    es.addEventListener('logs', (event) => {
      this.#resetHeartbeat();
      try {
        const payload = JSON.parse(event.data) as { entries: AppLogEntry[] };
        this.#ingest(payload.entries);
      } catch (err) {
        logger.error('[AppLogStream] Failed to parse logs event', err);
      }
    });

    es.addEventListener('error', (event) => {
      try {
        const payload = JSON.parse((event as MessageEvent).data) as { message?: string };
        if (payload.message) this.error = payload.message;
      } catch {
        // network-level error, handled by onerror below
      }
    });

    es.onerror = () => {
      this.#reconnect();
    };
  }

  #ingest(entries: AppLogEntry[]): void {
    if (this.paused) {
      this.#pausedBuffer.push(...entries);
      if (this.#pausedBuffer.length > this.#maxEntries) {
        this.#pausedBuffer = this.#pausedBuffer.slice(-this.#maxEntries);
      }
      return;
    }
    this.#appendLive(entries);
  }

  #appendLive(incoming: AppLogEntry[]): void {
    if (incoming.length === 0) return;

    // On reconnect the server replays the last `tail` lines, which can overlap what
    // we already show; drop entries we already hold by key.
    const existing = new Set(this.entries.map(entryKey));
    const accepted = incoming.filter((e) => !existing.has(entryKey(e)));
    if (accepted.length === 0) return;

    let next = this.entries.concat(accepted);
    if (next.length > this.#maxEntries) {
      next = next.slice(next.length - this.#maxEntries);
    }
    this.entries = next;
  }

  #resetHeartbeat(): void {
    if (this.#heartbeatTimer) clearTimeout(this.#heartbeatTimer);
    this.#heartbeatTimer = setTimeout(() => {
      if (this.status === 'connected') {
        logger.warn('[AppLogStream] No events for 90s, reconnecting');
        this.#reconnect();
      }
    }, HEARTBEAT_TIMEOUT_MS);
  }

  #reconnect(): void {
    if (this.#destroyed) return;
    this.#closeEventSource();
    this.#clearTimers();
    this.#reconnectAttempt += 1;

    if (this.#reconnectAttempt > MAX_RECONNECT_ATTEMPTS) {
      this.status = 'disconnected';
      this.error = 'connection lost';
      return;
    }

    this.status = 'reconnecting';
    const delay = Math.min(1000 * Math.pow(2, this.#reconnectAttempt - 1), 30000);
    this.#reconnectTimer = setTimeout(() => {
      void this.#connect();
    }, delay);
  }

  #closeEventSource(): void {
    if (this.#eventSource) {
      this.#eventSource.onerror = null;
      this.#eventSource.close();
      this.#eventSource = null;
    }
  }

  #clearTimers(): void {
    if (this.#heartbeatTimer) {
      clearTimeout(this.#heartbeatTimer);
      this.#heartbeatTimer = null;
    }
    if (this.#reconnectTimer) {
      clearTimeout(this.#reconnectTimer);
      this.#reconnectTimer = null;
    }
  }
}
