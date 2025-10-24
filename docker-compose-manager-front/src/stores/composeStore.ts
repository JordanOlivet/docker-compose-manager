import { create } from 'zustand';
import type { ComposeFile, ComposeProject, ComposeFileContent } from '../types';

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

  // Actions - Files
  setFiles: (files: ComposeFile[]) => void;
  setSelectedFile: (file: ComposeFileContent | null) => void;
  setIsLoadingFiles: (isLoading: boolean) => void;
  setFilesError: (error: string | null) => void;
  addFile: (file: ComposeFile) => void;
  updateFile: (id: number, updates: Partial<ComposeFile>) => void;
  removeFile: (id: number) => void;

  // Actions - Projects
  setProjects: (projects: ComposeProject[]) => void;
  setSelectedProject: (project: ComposeProject | null) => void;
  setIsLoadingProjects: (isLoading: boolean) => void;
  setProjectsError: (error: string | null) => void;
  updateProjectStatus: (projectName: string, status: string) => void;

  // Reset
  reset: () => void;
}

const initialState = {
  files: [],
  selectedFile: null,
  isLoadingFiles: false,
  filesError: null,
  projects: [],
  selectedProject: null,
  isLoadingProjects: false,
  projectsError: null,
};

export const useComposeStore = create<ComposeState>((set) => ({
  ...initialState,

  // Files Actions
  setFiles: (files) => set({ files }),

  setSelectedFile: (file) => set({ selectedFile: file }),

  setIsLoadingFiles: (isLoading) => set({ isLoadingFiles: isLoading }),

  setFilesError: (error) => set({ filesError: error }),

  addFile: (file) =>
    set((state) => ({
      files: [...state.files, file],
    })),

  updateFile: (id, updates) =>
    set((state) => ({
      files: state.files.map((f) => (f.id === id ? { ...f, ...updates } : f)),
    })),

  removeFile: (id) =>
    set((state) => ({
      files: state.files.filter((f) => f.id !== id),
      selectedFile: state.selectedFile?.id === id ? null : state.selectedFile,
    })),

  // Projects Actions
  setProjects: (projects) => set({ projects }),

  setSelectedProject: (project) => set({ selectedProject: project }),

  setIsLoadingProjects: (isLoading) => set({ isLoadingProjects: isLoading }),

  setProjectsError: (error) => set({ projectsError: error }),

  updateProjectStatus: (projectName, status) =>
    set((state) => ({
      projects: state.projects.map((p) =>
        p.name === projectName ? { ...p, status: status as any } : p
      ),
    })),

  // Reset
  reset: () => set(initialState),
}));
