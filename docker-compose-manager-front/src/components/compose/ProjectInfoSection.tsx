import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { composeApi } from '@/api/compose';
import { ChevronDown, ChevronRight, Edit, FileCode, Network, HardDrive, Tag, Variable } from 'lucide-react';

interface ProjectInfoSectionProps {
  projectName: string;
  projectPath?: string;
}

export function ProjectInfoSection({ projectName, projectPath }: ProjectInfoSectionProps) {
  const navigate = useNavigate();
  const [openSections, setOpenSections] = useState<Record<string, boolean>>({
    services: true,
    networks: false,
    volumes: false,
  });

  const { data: parsedDetails, isLoading, error } = useQuery({
    queryKey: ['projectParsedDetails', projectName],
    queryFn: () => composeApi.getProjectParsedDetails(projectName),
  });

  const toggleSection = (section: string) => {
    setOpenSections((prev) => ({ ...prev, [section]: !prev[section] }));
  };

  const handleEditFile = async () => {
    if (projectPath) {
      try {
        const fileContent = await composeApi.getFileByPath(projectPath);
        navigate(`/compose/files/${fileContent.id}/edit`);
      } catch (error) {
        console.error('Failed to get file ID:', error);
      }
    }
  };

  if (isLoading) {
    return (
      <div className="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg p-6">
        <div className="flex items-center gap-2 mb-4">
          <FileCode className="h-5 w-5 text-gray-600 dark:text-gray-400" />
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">Compose File Details</h3>
        </div>
        <p className="text-sm text-gray-600 dark:text-gray-400">Loading...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg p-6">
        <div className="flex items-center gap-2 mb-4">
          <FileCode className="h-5 w-5 text-gray-600 dark:text-gray-400" />
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">Compose File Details</h3>
        </div>
        <p className="text-sm text-red-600 dark:text-red-400">Failed to load compose file details</p>
      </div>
    );
  }

  if (!parsedDetails) return null;

  return (
    <div className="bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg hover:shadow-2xl transition-all duration-300 overflow-hidden">
      <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <FileCode className="h-5 w-5 text-gray-600 dark:text-gray-400" />
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white">Compose File Details</h3>
          </div>
          {projectPath && (
            <button
              onClick={handleEditFile}
              className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-gray-700 dark:text-gray-300 bg-gray-100 dark:bg-gray-700 hover:bg-gray-200 dark:hover:bg-gray-600 rounded-lg transition-colors"
            >
              <Edit className="h-4 w-4" />
              Edit File
            </button>
          )}
        </div>
      </div>

      <div className="p-6 space-y-4">
        {/* Services Section */}
        <div className="border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden">
          <button
            onClick={() => toggleSection('services')}
            className="flex items-center gap-2 w-full px-4 py-3 bg-gray-50 dark:bg-gray-700/50 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
          >
            {openSections.services ? (
              <ChevronDown className="h-4 w-4 text-gray-600 dark:text-gray-400" />
            ) : (
              <ChevronRight className="h-4 w-4 text-gray-600 dark:text-gray-400" />
            )}
            <Tag className="h-4 w-4 text-blue-600 dark:text-blue-400" />
            <span className="font-semibold text-gray-900 dark:text-white">
              Services ({Object.keys(parsedDetails.services).length})
            </span>
          </button>

          {openSections.services && (
            <div className="p-4 space-y-3 bg-white dark:bg-gray-800">
              {Object.entries(parsedDetails.services).map(([serviceName, service]: [string, any]) => (
                <div key={serviceName} className="border border-gray-200 dark:border-gray-700 rounded-lg p-4 space-y-3">
                  <div className="flex items-center justify-between">
                    <h4 className="font-semibold text-sm text-gray-900 dark:text-white">{serviceName}</h4>
                    {service.image && (
                      <span className="text-xs text-gray-600 dark:text-gray-400 font-mono bg-gray-100 dark:bg-gray-700 px-2 py-1 rounded">
                        {service.image}
                      </span>
                    )}
                  </div>

                  {/* Environment Variables */}
                  {service.environment && Object.keys(service.environment).length > 0 && (
                    <div className="space-y-1">
                      <div className="flex items-center gap-1 text-xs text-gray-600 dark:text-gray-400">
                        <Variable className="h-3 w-3" />
                        <span>Environment Variables</span>
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

                  {/* Ports */}
                  {service.ports && service.ports.length > 0 && (
                    <div className="space-y-1">
                      <div className="text-xs text-gray-600 dark:text-gray-400">Ports</div>
                      <div className="flex flex-wrap gap-1">
                        {service.ports.map((port: any, idx: number) => (
                          <span key={idx} className="text-xs bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 px-2 py-1 rounded font-mono">
                            {port}
                          </span>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* Volumes */}
                  {service.volumes && service.volumes.length > 0 && (
                    <div className="space-y-1">
                      <div className="text-xs text-gray-600 dark:text-gray-400">Volumes</div>
                      <div className="bg-gray-50 dark:bg-gray-900/50 rounded p-2 text-xs font-mono space-y-1 max-h-24 overflow-y-auto">
                        {service.volumes.map((volume: any, idx: number) => (
                          <div key={idx} className="text-gray-700 dark:text-gray-300">{volume}</div>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* Labels */}
                  {service.labels && Object.keys(service.labels).length > 0 && (
                    <div className="space-y-1">
                      <div className="text-xs text-gray-600 dark:text-gray-400">Labels</div>
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

                  {/* Other properties */}
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
          )}
        </div>

        {/* Networks Section */}
        {parsedDetails.networks && Object.keys(parsedDetails.networks).length > 0 && (
          <div className="border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden">
            <button
              onClick={() => toggleSection('networks')}
              className="flex items-center gap-2 w-full px-4 py-3 bg-gray-50 dark:bg-gray-700/50 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
            >
              {openSections.networks ? (
                <ChevronDown className="h-4 w-4 text-gray-600 dark:text-gray-400" />
              ) : (
                <ChevronRight className="h-4 w-4 text-gray-600 dark:text-gray-400" />
              )}
              <Network className="h-4 w-4 text-green-600 dark:text-green-400" />
              <span className="font-semibold text-gray-900 dark:text-white">
                Networks ({Object.keys(parsedDetails.networks).length})
              </span>
            </button>

            {openSections.networks && (
              <div className="p-4 space-y-2 bg-white dark:bg-gray-800">
                {Object.entries(parsedDetails.networks).map(([networkName, network]: [string, any]) => (
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
            )}
          </div>
        )}

        {/* Volumes Section */}
        {parsedDetails.volumes && Object.keys(parsedDetails.volumes).length > 0 && (
          <div className="border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden">
            <button
              onClick={() => toggleSection('volumes')}
              className="flex items-center gap-2 w-full px-4 py-3 bg-gray-50 dark:bg-gray-700/50 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
            >
              {openSections.volumes ? (
                <ChevronDown className="h-4 w-4 text-gray-600 dark:text-gray-400" />
              ) : (
                <ChevronRight className="h-4 w-4 text-gray-600 dark:text-gray-400" />
              )}
              <HardDrive className="h-4 w-4 text-orange-600 dark:text-orange-400" />
              <span className="font-semibold text-gray-900 dark:text-white">
                Volumes ({Object.keys(parsedDetails.volumes).length})
              </span>
            </button>

            {openSections.volumes && (
              <div className="p-4 space-y-2 bg-white dark:bg-gray-800">
                {Object.entries(parsedDetails.volumes).map(([volumeName, volume]: [string, any]) => (
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
            )}
          </div>
        )}
      </div>
    </div>
  );
}
