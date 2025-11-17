import { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Save, X, FileText } from 'lucide-react';
import Editor from '@monaco-editor/react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { composeApi } from '../api';
import { LoadingSpinner, ErrorDisplay } from '../components/common';
import { useTranslation } from 'react-i18next';

const DEFAULT_COMPOSE_CONTENT = `version: '3.8'

services:
  app:
    image: nginx:latest
    ports:
      - "8080:80"
    volumes:
      - ./data:/usr/share/nginx/html
    restart: unless-stopped
`;

function ComposeEditor() {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const isEditMode = id !== undefined && id !== 'create';

  const [filePath, setFilePath] = useState('');
  const [content, setContent] = useState(DEFAULT_COMPOSE_CONTENT);
  const [etag, setEtag] = useState('');
  const [hasChanges, setHasChanges] = useState(false);

  // Fetch file if editing
  const {
    data: fileData,
    isLoading: isLoadingFile,
    error: loadError,
  } = useQuery({
    queryKey: ['composeFile', id],
    queryFn: () => composeApi.getFile(Number(id)),
    enabled: isEditMode,
  });

  // Update state when file data loads
  useEffect(() => {
    if (fileData) {
      setFilePath(fileData.fullPath);
      setContent(fileData.content);
      setEtag(fileData.etag);
      setHasChanges(false);
    }
  }, [fileData]);

  // Create mutation
  const createMutation = useMutation({
    mutationFn: (data: { filePath: string; content: string }) =>
      composeApi.createFile(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['composeFiles'] });
      navigate('/compose/files');
    },
  });

  // Update mutation
  const updateMutation = useMutation({
    mutationFn: (data: { id: number; content: string; etag: string }) =>
      composeApi.updateFile(data.id, { content: data.content, etag: data.etag }),
    onSuccess: (updatedFile) => {
      queryClient.invalidateQueries({ queryKey: ['composeFiles'] });
      queryClient.invalidateQueries({ queryKey: ['composeFile', id] });
      setEtag(updatedFile.etag);
      setHasChanges(false);
    },
  });

  const handleSave = () => {
    if (isEditMode && id) {
      updateMutation.mutate({
        id: Number(id),
        content,
        etag,
      });
    } else {
      if (!filePath.trim()) {
        alert(t('compose.filePath'));
        return;
      }
      createMutation.mutate({ filePath, content });
    }
  };

  const handleCancel = () => {
    if (hasChanges) {
      if (confirm(t('compose.unsavedChanges'))) {
        navigate('/compose/files');
      }
    } else {
      navigate('/compose/files');
    }
  };

  const handleEditorChange = (value: string | undefined) => {
    if (value !== undefined) {
      setContent(value);
      setHasChanges(true);
    }
  };

  if (isEditMode && isLoadingFile) {
    return (
      <div className="flex items-center justify-center h-96">
        <LoadingSpinner size="lg" text="Loading compose file..." />
      </div>
    );
  }

  if (isEditMode && loadError) {
    return (
      <ErrorDisplay
        title={t('compose.failedToLoadFile')}
        message={loadError instanceof Error ? loadError.message : 'An unexpected error occurred'}
        onRetry={() => navigate('/compose/files')}
      />
    );
  }

  const isSaving = createMutation.isPending || updateMutation.isPending;
  const saveError = createMutation.error || updateMutation.error;

  return (
    <div className="flex flex-col h-[calc(100vh-8rem)]">
      {/* Header */}
      <div className="mb-4">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-gray-900">
              {isEditMode ? t('compose.editFile') : t('compose.createFile')}
            </h1>
            <p className="text-sm text-gray-600 mt-1">
              {isEditMode ? fileData?.fullPath : t('compose.filePathPlaceholder')}
            </p>
          </div>
          <div className="flex gap-3">
            <button
              onClick={handleCancel}
              disabled={isSaving}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              <X className="w-4 h-4" />
              {t('common.cancel')}
            </button>
            <button
              onClick={handleSave}
              disabled={isSaving || (!isEditMode && !filePath.trim())}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              <Save className="w-4 h-4" />
              {isSaving ? `${t('common.save')}...` : t('common.save')}
            </button>
          </div>
        </div>
      </div>

      {/* File Path Input (Create Mode Only) */}
      {!isEditMode && (
        <div className="mb-4">
          <label htmlFor="filePath" className="block text-sm font-medium text-gray-700 mb-2">
            {t('compose.filePath')} *
          </label>
          <div className="flex items-center gap-2">
            <FileText className="w-5 h-5 text-gray-400" />
            <input
              type="text"
              id="filePath"
              value={filePath}
              onChange={(e) => setFilePath(e.target.value)}
              placeholder={t('compose.filePathPlaceholder')}
              className="flex-1 px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
              disabled={isSaving}
            />
          </div>
          <p className="text-xs text-gray-500 mt-1">
            {t('compose.fileContent')}
          </p>
        </div>
      )}

      {/* Save Error */}
      {saveError && (
        <div className="mb-4">
          <ErrorDisplay
            title="Failed to save"
            message={saveError instanceof Error ? saveError.message : 'An unexpected error occurred'}
          />
        </div>
      )}

      {/* Editor */}
      <div className="flex-1 border border-gray-300 rounded-lg overflow-hidden bg-white">
        <Editor
          height="100%"
          defaultLanguage="yaml"
          value={content}
          onChange={handleEditorChange}
          theme="vs-light"
          options={{
            minimap: { enabled: true },
            fontSize: 14,
            lineNumbers: 'on',
            rulers: [80, 120],
            wordWrap: 'on',
            scrollBeyondLastLine: false,
            automaticLayout: true,
            tabSize: 2,
            insertSpaces: true,
            formatOnPaste: true,
            formatOnType: true,
          }}
        />
      </div>

      {/* Status Bar */}
      <div className="mt-2 flex items-center justify-between text-xs text-gray-600">
        <div className="flex items-center gap-4">
          <span>{t('compose.yaml')}</span>
          <span>{t('compose.utf8')}</span>
          <span>{content.split('\n').length} {t('compose.lines')}</span>
          <span>{content.length} {t('compose.characters')}</span>
        </div>
        {hasChanges && (
          <div className="flex items-center gap-2">
            <div className="w-2 h-2 bg-orange-500 rounded-full"></div>
            <span className="text-orange-600 font-medium">{t('compose.unsavedChanges')}</span>
          </div>
        )}
      </div>
    </div>
  );
}
export default ComposeEditor;
