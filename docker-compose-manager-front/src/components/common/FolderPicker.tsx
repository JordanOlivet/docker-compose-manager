import { useState, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import configApi, { type DirectoryBrowseResult } from '../../api/config';
import { LoadingSpinner } from './LoadingSpinner';

interface FolderPickerProps {
  onSelect: (path: string) => void;
  onCancel: () => void;
  initialPath?: string;
}

export function FolderPicker({ onSelect, onCancel, initialPath }: FolderPickerProps) {
  const [currentPath, setCurrentPath] = useState<string | undefined>(initialPath);
  const [selectedPath, setSelectedPath] = useState<string>(initialPath || '');

  const { data, isLoading, error, refetch } = useQuery<DirectoryBrowseResult>({
    queryKey: ['browseDirectories', currentPath],
    queryFn: () => configApi.browseDirectories(currentPath),
  });

  useEffect(() => {
    refetch();
  }, [currentPath, refetch]);

  const handleDirectoryClick = (path: string) => {
    setCurrentPath(path);
    setSelectedPath(path);
  };

  const handleParentClick = () => {
    if (data?.parentPath) {
      setCurrentPath(data.parentPath);
      setSelectedPath(data.parentPath);
    }
  };

  const handleSelect = () => {
    if (selectedPath) {
      onSelect(selectedPath);
    }
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setSelectedPath(e.target.value);
  };

  const handleInputKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      setCurrentPath(selectedPath);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/50 dark:bg-black/70 backdrop-blur-sm flex items-center justify-center z-50">
      <div className="bg-white dark:bg-gray-800 rounded-2xl shadow-2xl border border-gray-200 dark:border-gray-700 w-[600px] max-h-[600px] flex flex-col">
        <div className="p-6 border-b border-gray-200 dark:border-gray-700 bg-white/50 dark:bg-gray-800/50">
          <div className="flex items-center gap-3 mb-4">
            <div className="p-2 bg-blue-100 dark:bg-blue-900/30 rounded-lg">
              <svg className="w-5 h-5 text-blue-600 dark:text-blue-400" fill="currentColor" viewBox="0 0 20 20">
                <path d="M2 6a2 2 0 012-2h5l2 2h5a2 2 0 012 2v6a2 2 0 01-2 2H4a2 2 0 01-2-2V6z" />
              </svg>
            </div>
            <h2 className="text-xl font-bold text-gray-900 dark:text-white">Select Folder</h2>
          </div>
          <div className="flex gap-2">
            <input
              type="text"
              value={selectedPath}
              onChange={handleInputChange}
              onKeyDown={handleInputKeyDown}
              placeholder="Enter or select a path"
              className="flex-1 border border-gray-300 dark:border-gray-600 rounded-lg px-3 py-2 text-sm font-mono bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 dark:focus:ring-blue-400 focus:border-blue-500 transition-colors"
            />
            <button
              onClick={() => setCurrentPath(selectedPath)}
              className="px-4 py-2 bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-300 dark:hover:bg-gray-600 transition-colors text-sm font-medium disabled:opacity-50 disabled:cursor-not-allowed"
              disabled={!selectedPath}
            >
              Go
            </button>
          </div>
        </div>

        <div className="flex-1 overflow-auto p-6 bg-gray-50 dark:bg-gray-900">
          {isLoading && (
            <div className="flex justify-center items-center h-32">
              <LoadingSpinner />
            </div>
          )}

          {error && (
            <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg p-3 text-red-700 dark:text-red-400 text-sm">
              Error loading directories: {error instanceof Error ? error.message : 'Unknown error'}
            </div>
          )}

          {data && !isLoading && (
            <div className="space-y-1">
              {data.currentPath && (
                <div className="mb-4 px-3 py-2 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg text-sm text-gray-700 dark:text-gray-300">
                  <span className="font-semibold text-gray-900 dark:text-white">Current:</span> <span className="font-mono">{data.currentPath || 'Root'}</span>
                </div>
              )}

              {data.parentPath && (
                <button
                  onClick={handleParentClick}
                  className="w-full text-left px-3 py-2.5 rounded-lg hover:bg-white dark:hover:bg-gray-800 border border-transparent hover:border-gray-200 dark:hover:border-gray-700 flex items-center gap-2 text-sm transition-all font-medium text-gray-700 dark:text-gray-300 cursor-pointer"
                >
                  <svg className="w-4 h-4 text-blue-600 dark:text-blue-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 19l-7-7m0 0l7-7m-7 7h18" />
                  </svg>
                  <span className="font-semibold">.. (Parent Directory)</span>
                </button>
              )}

              {data.directories.length === 0 && (
                <div className="text-gray-500 dark:text-gray-400 text-sm italic py-8 text-center bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg">
                  No subdirectories found
                </div>
              )}

              {data.directories.map((dir) => (
                <button
                  key={dir.path}
                  onClick={() => dir.isAccessible && handleDirectoryClick(dir.path)}
                  disabled={!dir.isAccessible}
                  className={`w-full text-left px-3 py-2.5 rounded-lg flex items-center gap-2 text-sm transition-all border ${
                    dir.isAccessible
                      ? 'hover:bg-white dark:hover:bg-gray-800 cursor-pointer hover:border-gray-200 dark:hover:border-gray-700 text-gray-700 dark:text-gray-300'
                      : 'text-gray-400 dark:text-gray-600 cursor-not-allowed bg-gray-100 dark:bg-gray-800/50'
                  } ${
                    selectedPath === dir.path
                      ? 'bg-blue-50 dark:bg-blue-900/20 border-blue-500 dark:border-blue-400 shadow-sm'
                      : 'border-transparent'
                  }`}
                >
                  <svg className={`w-4 h-4 shrink-0 ${selectedPath === dir.path ? 'text-blue-600 dark:text-blue-400' : 'text-gray-500 dark:text-gray-400'}`} fill="currentColor" viewBox="0 0 20 20">
                    <path d="M2 6a2 2 0 012-2h5l2 2h5a2 2 0 012 2v6a2 2 0 01-2 2H4a2 2 0 01-2-2V6z" />
                  </svg>
                  <span className="truncate flex-1">{dir.name}</span>
                  {!dir.isAccessible && (
                    <svg className="w-4 h-4 shrink-0 text-gray-400 dark:text-gray-600" fill="currentColor" viewBox="0 0 20 20">
                      <path fillRule="evenodd" d="M5 9V7a5 5 0 0110 0v2a2 2 0 012 2v5a2 2 0 01-2 2H5a2 2 0 01-2-2v-5a2 2 0 012-2zm8-2v2H7V7a3 3 0 016 0z" clipRule="evenodd" />
                    </svg>
                  )}
                </button>
              ))}
            </div>
          )}
        </div>

        <div className="p-6 border-t border-gray-200 dark:border-gray-700 bg-white/50 dark:bg-gray-800/50 flex justify-end gap-3">
          <button
            onClick={onCancel}
            className="px-5 py-2.5 bg-gray-300 dark:bg-gray-600 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-400 dark:hover:bg-gray-500 transition-colors font-medium"
          >
            Cancel
          </button>
          <button
            onClick={handleSelect}
            disabled={!selectedPath}
            className="px-5 py-2.5 bg-blue-600 dark:bg-blue-700 text-white rounded-lg hover:bg-blue-700 dark:hover:bg-blue-600 disabled:bg-gray-300 dark:disabled:bg-gray-700 disabled:text-gray-500 dark:disabled:text-gray-500 disabled:cursor-not-allowed transition-colors font-medium shadow-lg hover:shadow-xl disabled:shadow-none"
          >
            Select
          </button>
        </div>
      </div>
    </div>
  );
}
