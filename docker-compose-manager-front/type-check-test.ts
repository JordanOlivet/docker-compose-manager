// Type check test for new compose discovery types
import type {
  DiscoveredComposeFileDto,
  ComposeHealthDto,
  ComposeHealthStatusDto,
  DockerDaemonStatusDto,
  ConflictErrorDto,
  ConflictsResponse,
  ComposeProject
} from './src/lib/types/compose';

// Test DiscoveredComposeFileDto
const discoveredFile: DiscoveredComposeFileDto = {
  filePath: '/path/to/docker-compose.yml',
  projectName: 'my-project',
  directoryPath: '/path/to',
  lastModified: '2026-01-10T12:00:00Z',
  isValid: true,
  isDisabled: false,
  services: ['web', 'db']
};

// Test ComposeHealthDto
const healthStatus: ComposeHealthDto = {
  status: 'healthy',
  composeDiscovery: {
    status: 'healthy',
    rootPath: '/compose',
    exists: true,
    accessible: true,
    degradedMode: false
  },
  dockerDaemon: {
    status: 'healthy',
    connected: true,
    version: '24.0.0',
    apiVersion: '1.43'
  }
};

// Test ConflictsResponse
const conflicts: ConflictsResponse = {
  conflicts: [
    {
      projectName: 'myapp',
      conflictingFiles: ['/path1/docker-compose.yml', '/path2/docker-compose.yml'],
      message: 'Multiple files found',
      resolutionSteps: ['Step 1', 'Step 2']
    }
  ],
  hasConflicts: true
};

// Test ComposeProject with new optional fields
const project: ComposeProject = {
  name: 'test-project',
  path: '/test',
  state: 'running',
  services: [],
  composeFiles: [],
  lastUpdated: new Date(),
  composeFilePath: '/test/docker-compose.yml',
  hasComposeFile: true,
  warning: null,
  availableActions: {
    up: true,
    down: true,
    restart: true
  }
};

console.log('All type checks passed!');
