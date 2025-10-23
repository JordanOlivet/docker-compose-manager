import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ProtectedRoute } from './components/ProtectedRoute';
import { MainLayout } from './components/layout';
import { Login } from './pages/Login';
import { Dashboard } from './pages/Dashboard';
import { ComposeFiles } from './pages/ComposeFiles';
import { ComposeEditor } from './pages/ComposeEditor';
import { ComposeProjects } from './pages/ComposeProjects';
import { AuditLogs } from './pages/AuditLogs';

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
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<Login />} />
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
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  );
}

export default App;
