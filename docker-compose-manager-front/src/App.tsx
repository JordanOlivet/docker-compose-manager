import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Toaster } from 'react-hot-toast';
import { ErrorBoundary } from './components/common/ErrorBoundary';
import { ProtectedRoute } from './components/ProtectedRoute';
import { AuthInitializer } from './components/AuthInitializer';
import { MainLayout } from './components/layout';
import { ThemeProvider } from './contexts/ThemeContext';
import { appRoutes } from './routes';
import { Suspense } from 'react';

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
      <ThemeProvider>
        <QueryClientProvider client={queryClient}>
          <BrowserRouter>
            <AuthInitializer>
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
              <Suspense fallback={<div className="p-8 text-center">Chargement...</div>}>
                <Routes>
                  {appRoutes.map(({ path, element: Element, protected: isProtected }) => {
                    if (isProtected) {
                      return (
                        <Route
                          key={path}
                          path={path}
                          element={
                            <ProtectedRoute>
                              <MainLayout><Element /></MainLayout>
                            </ProtectedRoute>
                          }
                        />
                      );
                    }
                    return <Route key={path} path={path} element={<Element />} />;
                  })}
                  <Route path="*" element={<Navigate to="/" replace />} />
                </Routes>
              </Suspense>
            </AuthInitializer>
          </BrowserRouter>
        </QueryClientProvider>
      </ThemeProvider>
    </ErrorBoundary>
  );
}

export default App;
