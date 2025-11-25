import type { ComposeFile, ComposeProject, ComposeFileContent, EntityState } from '$lib/types';

interface ComposeState {
  // Compose Files
  files: ComposeFile[];
  selectedFile: ComposeFileContent | null;
  isLoadingFiles: boolean;
  filesError: string | null;

  // Compose Projects
  projects: ComposeProject[];
  selectedProject: ComposeProject | null;
  isLoadingProjects: boolean;
  projectsError: string | null;
}

const initialState: ComposeState = {
  files: [],
  selectedFile: null,
  isLoadingFiles: false,
  filesError: null,
  projects: [],
  selectedProject: null,
  isLoadingProjects: false,
  projectsError: null,
};

function createComposeStore() {
  let state = $state<ComposeState>({ ...initialState });

  return {
    // Getters - Files
    get files() { return state.files; },
    get selectedFile() { return state.selectedFile; },
    get isLoadingFiles() { return state.isLoadingFiles; },
    get filesError() { return state.filesError; },

    // Getters - Projects
    get projects() { return state.projects; },
    get selectedProject() { return state.selectedProject; },
    get isLoadingProjects() { return state.isLoadingProjects; },
    get projectsError() { return state.projectsError; },

    // Actions - Files
    setFiles(files: ComposeFile[]) {
      state.files = files;
    },

    setSelectedFile(file: ComposeFileContent | null) {
      state.selectedFile = file;
    },

    setIsLoadingFiles(isLoading: boolean) {
      state.isLoadingFiles = isLoading;
    },

    setFilesError(error: string | null) {
      state.filesError = error;
    },

    addFile(file: ComposeFile) {
      state.files = [...state.files, file];
    },

    updateFile(id: number, updates: Partial<ComposeFile>) {
      state.files = state.files.map((f) => (f.id === id ? { ...f, ...updates } : f));
    },

    removeFile(id: number) {
      state.files = state.files.filter((f) => f.id !== id);
      if (state.selectedFile?.id === id) {
        state.selectedFile = null;
      }
    },

    // Actions - Projects
    setProjects(projects: ComposeProject[]) {
      state.projects = projects;
    },

    setSelectedProject(project: ComposeProject | null) {
      state.selectedProject = project;
    },

    setIsLoadingProjects(isLoading: boolean) {
      state.isLoadingProjects = isLoading;
    },

    setProjectsError(error: string | null) {
      state.projectsError = error;
    },

    updateProjectStatus(projectName: string, status: EntityState) {
      state.projects = state.projects.map((p) =>
        p.name === projectName ? { ...p, state: status } : p
      );
    },

    // Reset
    reset() {
      state = { ...initialState };
    },
  };
}

export const composeStore = createComposeStore();


