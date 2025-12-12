import type { ComposeFile, ComposeProject, ComposeFileContent, EntityState } from '$lib/types';

// Svelte 5 pattern: export state object with properties
export const compose = $state({
  files: [] as ComposeFile[],
  selectedFile: null as ComposeFileContent | null,
  isLoadingFiles: false,
  filesError: null as string | null,
  projects: [] as ComposeProject[],
  selectedProject: null as ComposeProject | null,
  isLoadingProjects: false,
  projectsError: null as string | null
});

// Actions - Files
export function setFiles(newFiles: ComposeFile[]) {
  compose.files = newFiles;
}

export function setSelectedFile(file: ComposeFileContent | null) {
  compose.selectedFile = file;
}

export function setIsLoadingFiles(isLoading: boolean) {
  compose.isLoadingFiles = isLoading;
}

export function setFilesError(error: string | null) {
  compose.filesError = error;
}

export function addFile(file: ComposeFile) {
  compose.files = [...compose.files, file];
}

export function updateFile(id: number, updates: Partial<ComposeFile>) {
  compose.files = compose.files.map((f) => (f.id === id ? { ...f, ...updates } : f));
}

export function removeFile(id: number) {
  compose.files = compose.files.filter((f) => f.id !== id);
  if (compose.selectedFile?.id === id) {
    compose.selectedFile = null;
  }
}

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
  compose.files = [];
  compose.selectedFile = null;
  compose.isLoadingFiles = false;
  compose.filesError = null;
  compose.projects = [];
  compose.selectedProject = null;
  compose.isLoadingProjects = false;
  compose.projectsError = null;
}
