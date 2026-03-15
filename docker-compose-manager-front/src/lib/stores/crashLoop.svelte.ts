import type { Container } from '$lib/types/container';
import type { ComposeProject } from '$lib/types/compose';

const crashLoopingEntities = $state(new Set<string>());

export function setCrashLooping(entityKey: string): void {
  crashLoopingEntities.add(entityKey);
}

export function clearCrashLooping(entityKey: string): void {
  crashLoopingEntities.delete(entityKey);
}

export function clearAllCrashLooping(): void {
  crashLoopingEntities.clear();
}

export function isCrashLooping(type: 'project' | 'container', id: string): boolean {
  return crashLoopingEntities.has(`${type}:${id}`);
}

/**
 * Synchronizes crash loop state from containers API data.
 */
export function syncFromContainers(containers: Container[]): void {
  for (const c of containers) {
    const key = `container:${c.id}`;
    if (c.isCrashLooping) {
      crashLoopingEntities.add(key);
    } else {
      crashLoopingEntities.delete(key);
    }
  }
}

/**
 * Synchronizes crash loop state from compose projects API data.
 */
export function syncFromProjects(projects: ComposeProject[]): void {
  for (const p of projects) {
    const projectKey = `project:${p.name}`;
    if (p.isCrashLooping) {
      crashLoopingEntities.add(projectKey);
    } else {
      crashLoopingEntities.delete(projectKey);
    }
    for (const s of p.services ?? []) {
      const serviceKey = `container:${s.id}`;
      if (s.isCrashLooping) {
        crashLoopingEntities.add(serviceKey);
      } else {
        crashLoopingEntities.delete(serviceKey);
      }
    }
  }
}
