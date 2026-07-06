import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { LogStreamController, type EventSourceLike } from './logStream.svelte';
import type { LogEntry, LogPage } from '$lib/types';

class FakeEventSource implements EventSourceLike {
  onerror: ((event: Event) => void) | null = null;
  closed = false;
  private listeners: Record<string, ((event: MessageEvent) => void)[]> = {};

  constructor(public url: string) {}

  addEventListener(type: string, listener: (event: MessageEvent) => void): void {
    (this.listeners[type] ??= []).push(listener);
  }
  close(): void {
    this.closed = true;
  }
  emit(type: string, data?: unknown): void {
    const payload = { data: typeof data === 'string' ? data : JSON.stringify(data) } as MessageEvent;
    (this.listeners[type] ?? []).forEach((cb) => cb(payload));
  }
  triggerError(): void {
    this.onerror?.(new Event('error'));
  }
}

function entry(ts: string, msg: string, id = 'c1'): LogEntry {
  return { timestamp: ts, containerId: id, containerName: id, service: id, stream: 'stdout', message: msg };
}

const flush = () => Promise.resolve().then(() => Promise.resolve());

describe('LogStreamController', () => {
  let sources: FakeEventSource[];

  function build(options: Partial<{ loadHistory: (until: string | undefined) => Promise<LogPage> }> = {}) {
    sources = [];
    const controller = new LogStreamController(
      { type: 'container', id: 'c1', name: 'c1' },
      {
        tail: 150,
        maxEntries: 5,
        eventSourceFactory: (url) => {
          const es = new FakeEventSource(url);
          sources.push(es);
          return es;
        },
        tokenProvider: async () => 'test-token',
        loadHistory: options.loadHistory,
      }
    );
    return controller;
  }

  const latest = () => sources[sources.length - 1];

  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it('appends batched entries from a logs event', async () => {
    const c = build();
    c.start();
    await flush();

    latest().emit('connected');
    latest().emit('logs', { entries: [entry('2026-07-04T12:00:00.000000000Z', 'a'), entry('2026-07-04T12:00:01.000000000Z', 'b')] });

    expect(c.status).toBe('connected');
    expect(c.entries.map((e) => e.message)).toEqual(['a', 'b']);
  });

  it('trims to the ring-buffer cap while following', async () => {
    const c = build(); // maxEntries = 5
    c.start();
    await flush();

    const batch = Array.from({ length: 8 }, (_, i) =>
      entry(`2026-07-04T12:00:0${i}.000000000Z`, `m${i}`)
    );
    latest().emit('logs', { entries: batch });

    expect(c.entries).toHaveLength(5);
    expect(c.entries[0].message).toBe('m3'); // oldest trimmed
    expect(c.entries[4].message).toBe('m7');
  });

  it('buffers while paused and flushes on resume', async () => {
    const c = build();
    c.start();
    await flush();

    c.pause();
    latest().emit('logs', { entries: [entry('2026-07-04T12:00:00.000000000Z', 'x')] });
    expect(c.entries).toHaveLength(0);

    c.resume();
    expect(c.entries.map((e) => e.message)).toEqual(['x']);
  });

  it('updates the container roster from a containers event', async () => {
    const c = build();
    c.start();
    await flush();

    latest().emit('containers', { containers: [{ id: 'c1', name: 'web', service: 'web', state: 'running' }] });
    expect(c.containers).toHaveLength(1);
    expect(c.containers[0].service).toBe('web');
  });

  it('loadOlder prepends older entries and dedups the boundary', async () => {
    const boundaryTs = '2026-07-04T12:00:05.000000000Z';
    const loadHistory = vi.fn(async (): Promise<LogPage> => ({
      entries: [
        entry('2026-07-04T12:00:03.000000000Z', 'older'),
        entry(boundaryTs, 'boundary'), // duplicate of what we already show
      ],
      hasMore: true,
    }));
    const c = build({ loadHistory });
    c.start();
    await flush();

    latest().emit('logs', { entries: [entry(boundaryTs, 'boundary'), entry('2026-07-04T12:00:06.000000000Z', 'newer')] });
    expect(c.entries.map((e) => e.message)).toEqual(['boundary', 'newer']);

    const prepended = await c.loadOlder();

    expect(prepended).toBe(1); // boundary dup dropped, only 'older' prepended
    expect(c.entries.map((e) => e.message)).toEqual(['older', 'boundary', 'newer']);
    expect(c.hasMore).toBe(true);
  });

  it('reconnects with backoff and resumes from the newest timestamp', async () => {
    const c = build();
    c.start();
    await flush();

    latest().emit('connected');
    latest().emit('logs', { entries: [entry('2026-07-04T12:00:09.000000000Z', 'last')] });

    latest().triggerError();
    expect(c.status).toBe('reconnecting');

    await vi.advanceTimersByTimeAsync(1000); // first backoff
    await flush();

    expect(sources).toHaveLength(2);
    expect(latest().url).toContain('since=');
  });

  it('dedups the inclusive boundary replayed after reconnect', async () => {
    const c = build();
    c.start();
    await flush();

    const lastTs = '2026-07-04T12:00:09.000000000Z';
    latest().emit('connected');
    latest().emit('logs', { entries: [entry(lastTs, 'last')] });

    latest().triggerError();
    await vi.advanceTimersByTimeAsync(1000);
    await flush();

    // Backend replays from `since` (inclusive) → same entry arrives again.
    latest().emit('connected');
    latest().emit('logs', { entries: [entry(lastTs, 'last'), entry('2026-07-04T12:00:10.000000000Z', 'after')] });

    expect(c.entries.map((e) => e.message)).toEqual(['last', 'after']);
  });

  it('surfaces an auth failure when no token is available', async () => {
    sources = [];
    const c = new LogStreamController(
      { type: 'container', id: 'c1', name: 'c1' },
      { eventSourceFactory: (url) => new FakeEventSource(url), tokenProvider: async () => null }
    );
    c.start();
    await flush();

    expect(c.status).toBe('disconnected');
    expect(c.error).toBe('authentication failed');
  });
});
