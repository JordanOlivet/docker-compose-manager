import { useState } from 'react';
import { RefreshCw, Filter, Eye } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { auditApi } from '../api';
import { LoadingSpinner, ErrorDisplay } from '../components/common';
import { AuditSortField, SortOrder } from '../types';
import type { AuditFilterRequest } from '../types';

export const AuditLogs = () => {
  const [filter, setFilter] = useState<AuditFilterRequest>({
    page: 1,
    pageSize: 50,
    sortBy: AuditSortField.Timestamp,
    sortOrder: SortOrder.Descending,
  });

  const [actionFilter, setActionFilter] = useState('');
  const [resourceTypeFilter, setResourceTypeFilter] = useState('');

  // Fetch audit logs
  const {
    data: auditData,
    isLoading,
    error,
    refetch,
  } = useQuery({
    queryKey: ['auditLogs', filter],
    queryFn: () => auditApi.listAuditLogs(filter),
  });

  // Fetch distinct actions for filter
  const { data: distinctActions = [] } = useQuery({
    queryKey: ['distinctActions'],
    queryFn: auditApi.getDistinctActions,
  });

  // Fetch distinct resource types for filter
  const { data: distinctResourceTypes = [] } = useQuery({
    queryKey: ['distinctResourceTypes'],
    queryFn: auditApi.getDistinctResourceTypes,
  });

  const applyFilters = () => {
    setFilter({
      ...filter,
      page: 1,
      action: actionFilter || undefined,
      resourceType: resourceTypeFilter || undefined,
    });
  };

  const clearFilters = () => {
    setActionFilter('');
    setResourceTypeFilter('');
    setFilter({
      page: 1,
      pageSize: 50,
      sortBy: AuditSortField.Timestamp,
      sortOrder: SortOrder.Descending,
    });
  };

  const handlePageChange = (newPage: number) => {
    setFilter({ ...filter, page: newPage });
  };

  if (isLoading && !auditData) {
    return (
      <div className="flex items-center justify-center h-96">
        <LoadingSpinner size="lg" text="Loading audit logs..." />
      </div>
    );
  }

  if (error) {
    return (
      <ErrorDisplay
        title="Failed to load audit logs"
        message={error instanceof Error ? error.message : 'An unexpected error occurred'}
        onRetry={() => refetch()}
      />
    );
  }

  const logs = auditData?.logs || [];
  const totalPages = auditData?.totalPages || 1;
  const currentPage = auditData?.page || 1;

  return (
    <div>
      {/* Header */}
      <div className="mb-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-gray-900">Audit Logs</h1>
            <p className="text-sm text-gray-600 mt-1">
              View system activity and user actions
            </p>
          </div>
          <button
            onClick={() => refetch()}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors"
          >
            <RefreshCw className="w-4 h-4" />
            Refresh
          </button>
        </div>
      </div>

      {/* Filters */}
      <div className="mb-6 p-4 bg-white rounded-lg border border-gray-200">
        <div className="flex items-center gap-2 mb-3">
          <Filter className="w-4 h-4 text-gray-600" />
          <h3 className="text-sm font-semibold text-gray-900">Filters</h3>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div>
            <label htmlFor="actionFilter" className="block text-sm font-medium text-gray-700 mb-1">
              Action
            </label>
            <select
              id="actionFilter"
              value={actionFilter}
              onChange={(e) => setActionFilter(e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
            >
              <option value="">All Actions</option>
              {distinctActions.map((action) => (
                <option key={action} value={action}>
                  {action}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="resourceTypeFilter" className="block text-sm font-medium text-gray-700 mb-1">
              Resource Type
            </label>
            <select
              id="resourceTypeFilter"
              value={resourceTypeFilter}
              onChange={(e) => setResourceTypeFilter(e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
            >
              <option value="">All Types</option>
              {distinctResourceTypes.map((type) => (
                <option key={type} value={type}>
                  {type}
                </option>
              ))}
            </select>
          </div>
          <div className="flex items-end gap-2">
            <button
              onClick={applyFilters}
              className="flex-1 px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700 transition-colors"
            >
              Apply
            </button>
            <button
              onClick={clearFilters}
              className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors"
            >
              Clear
            </button>
          </div>
        </div>
      </div>

      {/* Results Count */}
      <div className="mb-4">
        <p className="text-sm text-gray-600">
          {auditData?.totalCount || 0} total logs found
        </p>
      </div>

      {/* Logs Table */}
      {logs.length === 0 ? (
        <div className="text-center py-12 bg-white rounded-lg border border-gray-200">
          <Eye className="w-12 h-12 text-gray-400 mx-auto mb-4" />
          <h3 className="text-lg font-semibold text-gray-900 mb-2">No audit logs found</h3>
          <p className="text-sm text-gray-600">
            No logs match your current filters
          </p>
        </div>
      ) : (
        <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Timestamp
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    User
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Action
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Resource
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    IP Address
                  </th>
                </tr>
              </thead>
              <tbody className="bg-white divide-y divide-gray-200">
                {logs.map((log) => (
                  <tr key={log.id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-900">
                      {new Date(log.timestamp).toLocaleString()}
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-900">
                      {log.username || 'System'}
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap">
                      <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-100 text-blue-800">
                        {log.action}
                      </span>
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-600">
                      {log.resourceType && log.resourceId ? (
                        <span>
                          {log.resourceType}: {log.resourceId}
                        </span>
                      ) : (
                        <span className="text-gray-400">-</span>
                      )}
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-600 font-mono">
                      {log.ipAddress}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="px-4 py-3 border-t border-gray-200 bg-gray-50">
              <div className="flex items-center justify-between">
                <div className="text-sm text-gray-600">
                  Page {currentPage} of {totalPages}
                </div>
                <div className="flex gap-2">
                  <button
                    onClick={() => handlePageChange(currentPage - 1)}
                    disabled={currentPage === 1}
                    className="px-3 py-1 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                  >
                    Previous
                  </button>
                  <button
                    onClick={() => handlePageChange(currentPage + 1)}
                    disabled={currentPage === totalPages}
                    className="px-3 py-1 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                  >
                    Next
                  </button>
                </div>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
};
