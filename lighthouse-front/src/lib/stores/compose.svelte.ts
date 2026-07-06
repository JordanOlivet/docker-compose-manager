import type { ComposeProject, EntityState } from '$lib/types';

// Svelte 5 pattern: export state object with properties
export const compose = $state({
  projects: [] as ComposeProject[],
  selectedProject: null as ComposeProject | null,
  isLoadingProjects: false,
  projectsError: null as string | null
});

// Actions - Projects
export function setProjects(newProjects: ComposeProject[]) {
  compose.projects = newProjects;
}

export function setSelectedProject(project: ComposeProject | null) {
  compose.selectedProject = project;
}

export function setIsLoadingProjects(isLoading: boolean) {
  compose.isLoadingProjects = isLoading;
}

export function setProjectsError(error: string | null) {
  compose.projectsError = error;
}

export function updateProjectStatus(projectName: string, status: EntityState) {
  compose.projects = compose.projects.map((p) =>
    p.name === projectName ? { ...p, state: status } : p
  );
}

// Reset all state
export function reset() {
  compose.projects = [];
  compose.selectedProject = null;
  compose.isLoadingProjects = false;
  compose.projectsError = null;
}
