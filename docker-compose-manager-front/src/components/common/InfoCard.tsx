import { useState } from 'react';
import type { ReactNode } from 'react';

export interface InfoSection {
  id: string;
  title: string;
  icon?: ReactNode;
  count?: number; // optional badge/count
  initiallyOpen?: boolean;
  content: ReactNode;
}

interface InfoCardProps {
  title: string;
  sections: InfoSection[];
  headerActions?: ReactNode;
  className?: string;
}

/**
 * Generic info card with collapsible sections, shared between compose project details
 * and container technical details.
 */
export function InfoCard({ title, sections, headerActions, className }: InfoCardProps) {
  // Track open state per section (initialize from initiallyOpen or default false)
  const [open, setOpen] = useState<Record<string, boolean>>(() => {
    const initial: Record<string, boolean> = {};
    sections.forEach(s => { initial[s.id] = !!s.initiallyOpen; });
    return initial;
  });

  const toggle = (id: string) => setOpen(prev => ({ ...prev, [id]: !prev[id] }));

  return (
    <div className={"bg-white dark:bg-gray-800 rounded-2xl border border-gray-200 dark:border-gray-700 shadow-lg hover:shadow-2xl transition-all duration-300 overflow-hidden " + (className ?? "")}> 
      <div className="px-6 py-4 border-b border-gray-200 dark:border-gray-700 flex items-center justify-between">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-white flex items-center gap-2">
          {title}
        </h3>
        {headerActions && <div className="flex items-center gap-2">{headerActions}</div>}
      </div>

      <div className="p-6 space-y-4">
        {sections.map(section => (
          <div key={section.id} className="border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden">
            <button
              type="button"
              onClick={() => toggle(section.id)}
              className="flex items-center gap-2 w-full px-4 py-3 bg-gray-50 dark:bg-gray-700/50 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors text-left"
            >
              {/* Chevron */}
              {open[section.id] ? (
                <span className="inline-block transition-transform">
                  <svg className="h-4 w-4 text-gray-600 dark:text-gray-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M6 9l6 6 6-6"/></svg>
                </span>
              ) : (
                <span className="inline-block transition-transform -rotate-90">
                  <svg className="h-4 w-4 text-gray-600 dark:text-gray-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M6 9l6 6 6-6"/></svg>
                </span>
              )}
              {section.icon && <span className="flex items-center">{section.icon}</span>}
              <span className="font-semibold text-gray-900 dark:text-white flex items-center gap-2">
                {section.title}
                {typeof section.count === 'number' && (
                  <span className="text-xs font-medium bg-gray-200 dark:bg-gray-600 text-gray-700 dark:text-gray-200 px-2 py-0.5 rounded-full">
                    {section.count}
                  </span>
                )}
              </span>
            </button>
            {open[section.id] && (
              <div className="p-4 bg-white dark:bg-gray-800">
                {section.content}
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
