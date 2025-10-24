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
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-lg w-[600px] max-h-[600px] flex flex-col">
        <div className="p-4 border-b">
          <h2 className="text-xl font-bold mb-3">Select Folder</h2>
          <div className="flex gap-2">
            <input
              type="text"
              value={selectedPath}
              onChange={handleInputChange}
              onKeyDown={handleInputKeyDown}
              placeholder="Enter or select a path"
              className="flex-1 border rounded px-3 py-2 text-sm font-mono"
            />
            <button
              onClick={() => setCurrentPath(selectedPath)}
              className="px-3 py-2 bg-gray-200 rounded hover:bg-gray-300 text-sm"
              disabled={!selectedPath}
            >
              Go
            </button>
          </div>
        </div>

        <div className="flex-1 overflow-auto p-4">
          {isLoading && (
            <div className="flex justify-center items-center h-32">
              <LoadingSpinner />
            </div>
          )}

          {error && (
            <div className="text-red-600 text-sm">
              Error loading directories: {error instanceof Error ? error.message : 'Unknown error'}
            </div>
          )}

          {data && !isLoading && (
            <div className="space-y-1">
              {data.currentPath && (
                <div className="mb-3 text-sm text-gray-600">
                  <span className="font-semibold">Current:</span> {data.currentPath || 'Root'}
                </div>
              )}

              {data.parentPath && (
                <button
                  onClick={handleParentClick}
                  className="w-full text-left px-3 py-2 rounded hover:bg-gray-100 flex items-center gap-2 text-sm"
                >
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 19l-7-7m0 0l7-7m-7 7h18" />
                  </svg>
                  <span className="font-semibold">.. (Parent Directory)</span>
                </button>
              )}

              {data.directories.length === 0 && (
                <div className="text-gray-500 text-sm italic py-4">No subdirectories found</div>
              )}

              {data.directories.map((dir) => (
                <button
                  key={dir.path}
                  onClick={() => dir.isAccessible && handleDirectoryClick(dir.path)}
                  disabled={!dir.isAccessible}
                  className={`w-full text-left px-3 py-2 rounded flex items-center gap-2 text-sm ${
                    dir.isAccessible
                      ? 'hover:bg-blue-50 cursor-pointer'
                      : 'text-gray-400 cursor-not-allowed'
                  } ${selectedPath === dir.path ? 'bg-blue-100 border-2 border-blue-500' : ''}`}
                >
                  <svg className="w-4 h-4 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20">
                    <path d="M2 6a2 2 0 012-2h5l2 2h5a2 2 0 012 2v6a2 2 0 01-2 2H4a2 2 0 01-2-2V6z" />
                  </svg>
                  <span className="truncate">{dir.name}</span>
                  {!dir.isAccessible && (
                    <svg className="w-4 h-4 ml-auto flex-shrink-0" fill="currentColor" viewBox="0 0 20 20">
                      <path fillRule="evenodd" d="M5 9V7a5 5 0 0110 0v2a2 2 0 012 2v5a2 2 0 01-2 2H5a2 2 0 01-2-2v-5a2 2 0 012-2zm8-2v2H7V7a3 3 0 016 0z" clipRule="evenodd" />
                    </svg>
                  )}
                </button>
              ))}
            </div>
          )}
        </div>

        <div className="p-4 border-t flex justify-end gap-2">
          <button
            onClick={onCancel}
            className="px-4 py-2 bg-gray-300 rounded hover:bg-gray-400"
          >
            Cancel
          </button>
          <button
            onClick={handleSelect}
            disabled={!selectedPath}
            className="px-4 py-2 bg-blue-500 text-white rounded hover:bg-blue-600 disabled:bg-gray-300 disabled:cursor-not-allowed"
          >
            Select
          </button>
        </div>
      </div>
    </div>
  );
}
