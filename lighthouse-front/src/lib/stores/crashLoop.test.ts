import { describe, it, expect, beforeEach } from 'vitest';
import {
  setCrashLooping,
  clearCrashLooping,
  clearAllCrashLooping,
  isCrashLooping,
  syncFromContainers,
  syncFromProjects
} from './crashLoop.svelte';

describe('crashLoop store', () => {
  beforeEach(() => clearAllCrashLooping());

  it('sets and clears crash-loop state by entity key', () => {
    setCrashLooping('container:c1');
    expect(isCrashLooping('container', 'c1')).toBe(true);

    clearCrashLooping('container:c1');
    expect(isCrashLooping('container', 'c1')).toBe(false);
  });

  it('distinguishes projects from containers', () => {
    setCrashLooping('project:p1');
    expect(isCrashLooping('project', 'p1')).toBe(true);
    expect(isCrashLooping('container', 'p1')).toBe(false);
  });

  it('syncs from container API data', () => {
    syncFromContainers([
      { id: 'c1', isCrashLooping: true },
      { id: 'c2', isCrashLooping: false }
    ] as never);

    expect(isCrashLooping('container', 'c1')).toBe(true);
    expect(isCrashLooping('container', 'c2')).toBe(false);
  });

  it('syncs projects and their services', () => {
    syncFromProjects([
      {
        name: 'p1',
        isCrashLooping: true,
        services: [{ id: 'svc1', isCrashLooping: true }]
      }
    ] as never);

    expect(isCrashLooping('project', 'p1')).toBe(true);
    expect(isCrashLooping('container', 'svc1')).toBe(true);
  });
});
