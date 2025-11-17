import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { composeApi } from '@/api/compose';
import { Edit, Network, HardDrive, Tag, Variable } from 'lucide-react';
import type { ServiceDetails, NetworkDetails, VolumeDetails } from '@/types/compose';
import { InfoCard, type InfoSection } from '@/components/common/InfoCard';
import { useTranslation } from 'react-i18next';

interface ProjectInfoSectionProps {
  projectName: string;
  projectPath?: string;
}

export function ProjectInfoSection({ projectName, projectPath }: ProjectInfoSectionProps) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { data: parsedDetails, isLoading, error } = useQuery({
    queryKey: ['projectParsedDetails', projectName],
    queryFn: () => composeApi.getProjectParsedDetails(projectName),
  });

  const handleEditFile = async () => {
    if (!projectPath) return;
    try {
      const fileContent = await composeApi.getFileByPath(projectPath);
      navigate(`/compose/files/${fileContent.id}/edit`);
    } catch (e) {
      console.error('Failed to get file ID:', e);
    }
  };

  if (isLoading) {
    return <InfoCard title="Compose File Details" sections={[]} />;
  }
  if (error || !parsedDetails) {
    return <InfoCard title="Compose File Details" sections={[]} />;
  }

  // Build sections array
  const sections: InfoSection[] = [];

  // Services section
  if (parsedDetails.services && Object.keys(parsedDetails.services).length > 0) {
    sections.push({
      id: 'services',
      title: `Services`,
      icon: <Tag className="h-4 w-4 text-blue-600 dark:text-blue-400" />,
      count: Object.keys(parsedDetails.services).length,
      initiallyOpen: true,
      content: (
        <div className="space-y-3">
          {Object.entries(parsedDetails.services).map(([serviceName, service]: [string, ServiceDetails]) => (
            <div key={serviceName} className="border border-gray-200 dark:border-gray-700 rounded-lg p-4 space-y-3">
              <div className="flex items-center justify-between">
                <h4 className="font-semibold text-sm text-gray-900 dark:text-white">{serviceName}</h4>
                {service.image && (
                  <span className="text-xs text-gray-600 dark:text-gray-400 font-mono bg-gray-100 dark:bg-gray-700 px-2 py-1 rounded">
                    {service.image}
                  </span>
                )}
              </div>
              {service.environment && Object.keys(service.environment).length > 0 && (
                <div className="space-y-1">
                  <div className="flex items-center gap-1 text-xs text-gray-600 dark:text-gray-400">
                    <Variable className="h-3 w-3" />
                    <span>{t('common.environmentVariables')}</span>
                  </div>
                  <div className="bg-gray-50 dark:bg-gray-900/50 rounded p-2 text-xs font-mono space-y-1 max-h-32 overflow-y-auto">
                    {Object.entries(service.environment).map(([key, value]) => (
                      <div key={key} className="flex gap-2">
                        <span className="text-blue-600 dark:text-blue-400 font-semibold">{key}:</span>
                        <span className="text-gray-700 dark:text-gray-300 break-all">{value as string || '""'}</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}
              {service.ports && service.ports.length > 0 && (
                <div className="space-y-1">
                  <div className="text-xs text-gray-600 dark:text-gray-400">{t('containers.ports')}</div>
                  <div className="flex flex-wrap gap-1">
                    {service.ports.map((port: string, idx: number) => (
                      <span key={idx} className="text-xs bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 px-2 py-1 rounded font-mono">
                        {port}
                      </span>
                    ))}
                  </div>
                </div>
              )}
              {service.volumes && service.volumes.length > 0 && (
                <div className="space-y-1">
                  <div className="text-xs text-gray-600 dark:text-gray-400">{t('containers.volumes')}</div>
                  <div className="bg-gray-50 dark:bg-gray-900/50 rounded p-2 text-xs font-mono space-y-1 max-h-24 overflow-y-auto">
                    {service.volumes.map((volume: string, idx: number) => (
                      <div key={idx} className="text-gray-700 dark:text-gray-300">{volume}</div>
                    ))}
                  </div>
                </div>
              )}
              {service.labels && Object.keys(service.labels).length > 0 && (
                <div className="space-y-1">
                  <div className="text-xs text-gray-600 dark:text-gray-400">{t('containers.labels')}</div>
                  <div className="bg-gray-50 dark:bg-gray-900/50 rounded p-2 text-xs font-mono space-y-1 max-h-24 overflow-y-auto">
                    {Object.entries(service.labels).map(([key, value]) => (
                      <div key={key} className="flex gap-2">
                        <span className="text-blue-600 dark:text-blue-400">{key}:</span>
                        <span className="text-gray-700 dark:text-gray-300">{value as string}</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}
              <div className="flex flex-wrap gap-2 text-xs">
                {service.restart && (
                  <span className="bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300 px-2 py-1 rounded">
                    restart: {service.restart}
                  </span>
                )}
                {service.dependsOn && service.dependsOn.length > 0 && (
                  <span className="bg-purple-100 dark:bg-purple-900/30 text-purple-700 dark:text-purple-300 px-2 py-1 rounded">
                    depends on: {service.dependsOn.join(', ')}
                  </span>
                )}
              </div>
            </div>
          ))}
        </div>
      ),
    });
  }

  if (parsedDetails.networks && Object.keys(parsedDetails.networks).length > 0) {
    sections.push({
      id: 'networks',
      title: 'Networks',
      icon: <Network className="h-4 w-4 text-green-600 dark:text-green-400" />,
      count: Object.keys(parsedDetails.networks).length,
      content: (
        <div className="space-y-2">
          {Object.entries(parsedDetails.networks).map(([networkName, network]: [string, NetworkDetails]) => (
            <div key={networkName} className="border border-gray-200 dark:border-gray-700 rounded-lg p-3 space-y-2">
              <div className="flex items-center justify-between">
                <h4 className="font-semibold text-sm text-gray-900 dark:text-white">{networkName}</h4>
                {network.driver && (
                  <span className="text-xs bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 px-2 py-1 rounded font-mono">
                    driver: {network.driver}
                  </span>
                )}
              </div>
              {network.external && (
                <span className="text-xs bg-yellow-100 dark:bg-yellow-900/30 text-yellow-700 dark:text-yellow-300 px-2 py-1 rounded inline-block">
                  external
                </span>
              )}
              {network.labels && Object.keys(network.labels).length > 0 && (
                <div className="space-y-1">
                  <div className="text-xs text-gray-600 dark:text-gray-400">Labels</div>
                  <div className="bg-gray-50 dark:bg-gray-900/50 rounded p-2 text-xs font-mono space-y-1">
                    {Object.entries(network.labels).map(([key, value]) => (
                      <div key={key} className="text-gray-700 dark:text-gray-300">
                        {key}: {value as string}
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      ),
    });
  }

  if (parsedDetails.volumes && Object.keys(parsedDetails.volumes).length > 0) {
    sections.push({
      id: 'volumes',
      title: 'Volumes',
      icon: <HardDrive className="h-4 w-4 text-orange-600 dark:text-orange-400" />,
      count: Object.keys(parsedDetails.volumes).length,
      content: (
        <div className="space-y-2">
          {Object.entries(parsedDetails.volumes).map(([volumeName, volume]: [string, VolumeDetails]) => (
            <div key={volumeName} className="border border-gray-200 dark:border-gray-700 rounded-lg p-3 space-y-2">
              <div className="flex items-center justify-between">
                <h4 className="font-semibold text-sm text-gray-900 dark:text-white">{volumeName}</h4>
                {volume.driver && (
                  <span className="text-xs bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 px-2 py-1 rounded font-mono">
                    driver: {volume.driver}
                  </span>
                )}
              </div>
              {volume.external && (
                <span className="text-xs bg-yellow-100 dark:bg-yellow-900/30 text-yellow-700 dark:text-yellow-300 px-2 py-1 rounded inline-block">
                  external
                </span>
              )}
              {volume.labels && Object.keys(volume.labels).length > 0 && (
                <div className="space-y-1">
                  <div className="text-xs text-gray-600 dark:text-gray-400">Labels</div>
                  <div className="bg-gray-50 dark:bg-gray-900/50 rounded p-2 text-xs font-mono space-y-1">
                    {Object.entries(volume.labels).map(([key, value]) => (
                      <div key={key} className="text-gray-700 dark:text-gray-300">
                        {key}: {value as string}
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      ),
    });
  }

  return (
    <InfoCard
      title="Compose File Details"
      sections={sections}
      headerActions={projectPath && (
        <button
          onClick={handleEditFile}
          className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-gray-700 dark:text-gray-300 bg-gray-100 dark:bg-gray-700 hover:bg-gray-200 dark:hover:bg-gray-600 rounded-lg transition-colors"
        >
          <Edit className="h-4 w-4" />
          Edit File
        </button>
      )}
    />
  );
}
