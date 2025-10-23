import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../stores/authStore';
import { authApi } from '../api/auth';
import { containersApi } from '../api/containers';
import type { Container } from '../types';

export function Dashboard() {
  const [containers, setContainers] = useState<Container[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const navigate = useNavigate();
  const { user, logout: storeLogout } = useAuthStore();

  useEffect(() => {
    loadContainers();
  }, []);

  const loadContainers = async () => {
    try {
      setLoading(true);
      const data = await containersApi.list(true);
      setContainers(data);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to load containers');
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = async () => {
    const refreshToken = localStorage.getItem('refreshToken');
    if (refreshToken) {
      try {
        await authApi.logout(refreshToken);
      } catch (err) {
        console.error('Logout error:', err);
      }
    }
    storeLogout();
    navigate('/login');
  };

  const handleContainerAction = async (id: string, action: 'start' | 'stop' | 'restart') => {
    try {
      switch (action) {
        case 'start':
          await containersApi.start(id);
          break;
        case 'stop':
          await containersApi.stop(id);
          break;
        case 'restart':
          await containersApi.restart(id);
          break;
      }
      await loadContainers();
    } catch (err: any) {
      alert(err.response?.data?.message || `Failed to ${action} container`);
    }
  };

  return (
    <div className="min-h-screen bg-gray-100">
      <nav className="bg-white shadow-sm">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex justify-between h-16">
            <div className="flex items-center">
              <h1 className="text-xl font-bold">Docker Compose Manager</h1>
            </div>
            <div className="flex items-center space-x-4">
              <span className="text-sm text-gray-700">
                Welcome, <strong>{user?.username}</strong> ({user?.role})
              </span>
              <button
                onClick={handleLogout}
                className="bg-red-600 text-white px-4 py-2 rounded-md hover:bg-red-700"
              >
                Logout
              </button>
            </div>
          </div>
        </div>
      </nav>

      <main className="max-w-7xl mx-auto py-6 sm:px-6 lg:px-8">
        <div className="px-4 py-6 sm:px-0">
          <div className="flex justify-between items-center mb-6">
            <h2 className="text-2xl font-bold">Containers</h2>
            <button
              onClick={loadContainers}
              className="bg-blue-600 text-white px-4 py-2 rounded-md hover:bg-blue-700"
            >
              Refresh
            </button>
          </div>

          {loading && (
            <div className="text-center py-12">
              <p className="text-gray-600">Loading containers...</p>
            </div>
          )}

          {error && (
            <div className="mb-4 p-4 bg-red-100 border border-red-400 text-red-700 rounded">
              {error}
            </div>
          )}

          {!loading && !error && containers.length === 0 && (
            <div className="text-center py-12">
              <p className="text-gray-600">No containers found</p>
            </div>
          )}

          {!loading && containers.length > 0 && (
            <div className="bg-white shadow overflow-hidden sm:rounded-md">
              <ul className="divide-y divide-gray-200">
                {containers.map((container) => (
                  <li key={container.id} className="px-6 py-4">
                    <div className="flex items-center justify-between">
                      <div className="flex-1">
                        <h3 className="text-lg font-medium text-gray-900">
                          {container.name}
                        </h3>
                        <p className="text-sm text-gray-600">
                          Image: {container.image}
                        </p>
                        <p className="text-sm">
                          Status:{' '}
                          <span
                            className={`font-medium ${
                              container.state === 'running'
                                ? 'text-green-600'
                                : 'text-gray-600'
                            }`}
                          >
                            {container.status}
                          </span>
                        </p>
                      </div>
                      <div className="flex space-x-2">
                        {container.state !== 'running' && (
                          <button
                            onClick={() => handleContainerAction(container.id, 'start')}
                            className="bg-green-600 text-white px-3 py-1 rounded text-sm hover:bg-green-700"
                          >
                            Start
                          </button>
                        )}
                        {container.state === 'running' && (
                          <>
                            <button
                              onClick={() => handleContainerAction(container.id, 'stop')}
                              className="bg-yellow-600 text-white px-3 py-1 rounded text-sm hover:bg-yellow-700"
                            >
                              Stop
                            </button>
                            <button
                              onClick={() => handleContainerAction(container.id, 'restart')}
                              className="bg-blue-600 text-white px-3 py-1 rounded text-sm hover:bg-blue-700"
                            >
                              Restart
                            </button>
                          </>
                        )}
                      </div>
                    </div>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
