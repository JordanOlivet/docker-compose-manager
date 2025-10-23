import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Toaster } from 'react-hot-toast';
import { ErrorBoundary } from './components/common/ErrorBoundary';
import { ProtectedRoute } from './components/ProtectedRoute';
import { MainLayout } from './components/layout';
import { Login } from './pages/Login';
import { Dashboard } from './pages/Dashboard';
import { ComposeFiles } from './pages/ComposeFiles';
import { ComposeEditor } from './pages/ComposeEditor';
import { ComposeProjects } from './pages/ComposeProjects';
import { AuditLogs } from './pages/AuditLogs';
import ChangePassword from './pages/ChangePassword';
import UserManagement from './pages/UserManagement';
import Settings from './pages/Settings';
import LogsViewer from './pages/LogsViewer';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

function App() {
  return (
    <ErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <Toaster
            position="top-right"
            toastOptions={{
              duration: 3000,
              style: {
                background: '#363636',
                color: '#fff',
              },
              success: {
                duration: 3000,
                iconTheme: {
                  primary: '#4ade80',
                  secondary: '#fff',
                },
              },
              error: {
                duration: 4000,
                iconTheme: {
                  primary: '#ef4444',
                  secondary: '#fff',
                },
              },
            }}
          />
          <Routes>
            <Route path="/login" element={<Login />} />
            <Route path="/change-password" element={<ChangePassword />} />
            <Route
              path="/"
              element={
                <ProtectedRoute>
                  <MainLayout>
                    <Dashboard />
                  </MainLayout>
                </ProtectedRoute>
              }
            />
            <Route
              path="/dashboard"
              element={
                <ProtectedRoute>
                  <MainLayout>
                    <Dashboard />
                  </MainLayout>
                </ProtectedRoute>
              }
            />
            <Route
              path="/users"
              element={
                <ProtectedRoute>
                  <MainLayout>
                    <UserManagement />
                  </MainLayout>
                </ProtectedRoute>
              }
            />
            <Route
              path="/settings"
              element={
                <ProtectedRoute>
                  <MainLayout>
                    <Settings />
                  </MainLayout>
                </ProtectedRoute>
              }
            />
            <Route
              path="/compose/files"
              element={
                <ProtectedRoute>
                  <MainLayout>
                    <ComposeFiles />
                  </MainLayout>
                </ProtectedRoute>
              }
            />
            <Route
              path="/compose/files/:id/edit"
              element={
                <ProtectedRoute>
                  <MainLayout>
                    <ComposeEditor />
                  </MainLayout>
                </ProtectedRoute>
              }
            />
            <Route
              path="/compose/files/create"
              element={
                <ProtectedRoute>
                  <MainLayout>
                    <ComposeEditor />
                  </MainLayout>
                </ProtectedRoute>
              }
            />
            <Route
              path="/compose/projects"
              element={
                <ProtectedRoute>
                  <MainLayout>
                    <ComposeProjects />
                  </MainLayout>
                </ProtectedRoute>
              }
            />
            <Route
              path="/audit"
              element={
                <ProtectedRoute>
                  <MainLayout>
                    <AuditLogs />
                  </MainLayout>
                </ProtectedRoute>
              }
            />
            <Route
              path="/logs"
              element={
                <ProtectedRoute>
                  <MainLayout>
                    <LogsViewer />
                  </MainLayout>
                </ProtectedRoute>
              }
            />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </BrowserRouter>
      </QueryClientProvider>
    </ErrorBoundary>
  );
}

export default App;
