
import React from 'react';

const Dashboard = React.lazy(() => import('./pages/Dashboard'));
const Containers = React.lazy(() => import('./pages/Containers'));
const ContainerDetails = React.lazy(() => import('./pages/ContainerDetails'));
const ComposeFiles = React.lazy(() => import('./pages/ComposeFiles'));
const ComposeEditor = React.lazy(() => import('./pages/ComposeEditor'));
const ComposeProjects = React.lazy(() => import('./pages/ComposeProjects'));
const ComposeDetails = React.lazy(() => import('./pages/ComposeDetails'));
const AuditLogs = React.lazy(() => import('./pages/AuditLogs'));
const ChangePassword = React.lazy(() => import('./pages/ChangePassword'));
const UserManagement = React.lazy(() => import('./pages/UserManagement'));
const UserGroups = React.lazy(() => import('./pages/UserGroups'));
const Permissions = React.lazy(() => import('./pages/Permissions'));
const Settings = React.lazy(() => import('./pages/Settings'));
const LogsViewer = React.lazy(() => import('./pages/LogsViewer'));
const Login = React.lazy(() => import('./pages/Login'));

export interface AppRoute {
  path: string;
  element: React.ComponentType;
  protected?: boolean;
}

export const appRoutes: AppRoute[] = [
  { path: '/', element: Dashboard, protected: true },
  { path: '/dashboard', element: Dashboard, protected: true },
  { path: '/containers', element: Containers, protected: true },
  { path: '/containers/:containerId', element: ContainerDetails, protected: true },
  { path: '/users', element: UserManagement, protected: true },
  { path: '/user-groups', element: UserGroups, protected: true },
  { path: '/permissions', element: Permissions, protected: true },
  { path: '/settings', element: Settings, protected: true },
  { path: '/compose/files', element: ComposeFiles, protected: true },
  { path: '/compose/files/:id/edit', element: ComposeEditor, protected: true },
  { path: '/compose/files/create', element: ComposeEditor, protected: true },
  { path: '/compose/projects', element: ComposeProjects, protected: true },
  { path: '/compose/projects/:projectName', element: ComposeDetails, protected: true },
  { path: '/audit', element: AuditLogs, protected: true },
  { path: '/logs', element: LogsViewer, protected: true },
  { path: '/login', element: Login, protected: false },
  { path: '/change-password', element: ChangePassword, protected: false },
];
