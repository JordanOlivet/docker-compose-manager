import { NavLink } from 'react-router-dom';
import {
  LayoutDashboard,
  FileText,
  Package,
  Container,
  ClipboardList,
  Users,
  UsersRound,
  Shield,
  FileOutput,
  Settings,
  Boxes
} from 'lucide-react';
import { useAuthStore } from '../../stores/authStore';

interface SidebarProps {
  isOpen: boolean;
  onToggle: () => void;
}

interface NavItem {
  to: string;
  icon: React.ReactNode;
  label: string;
  category?: string;
}

export const Sidebar = ({ isOpen }: SidebarProps) => {
  const { user } = useAuthStore();

  if (!isOpen) return null;

  const navItems: NavItem[] = user?.role === "admin" ? [
    { to: '/', icon: <LayoutDashboard className="w-5 h-5" />, label: 'Dashboard', category: 'Overview' },
    { to: '/compose/projects', icon: <Package className="w-5 h-5" />, label: 'Compose Projects', category: 'Docker' },
    { to: '/containers', icon: <Container className="w-5 h-5" />, label: 'Containers', category: 'Docker' },  
    { to: '/logs', icon: <FileOutput className="w-5 h-5" />, label: 'Logs Viewer', category: 'Docker' },
    { to: '/users', icon: <Users className="w-5 h-5" />, label: 'User Management', category: 'Administration' },
    { to: '/user-groups', icon: <UsersRound className="w-5 h-5" />, label: 'User Groups', category: 'Administration' },
    { to: '/permissions', icon: <Shield className="w-5 h-5" />, label: 'Permissions', category: 'Administration' },
    { to: '/audit', icon: <ClipboardList className="w-5 h-5" />, label: 'Audit Logs', category: 'Administration' },
    { to: '/compose/files', icon: <FileText className="w-5 h-5" />, label: 'Compose Files', category: 'Administration' },
    { to: '/settings', icon: <Settings className="w-5 h-5" />, label: 'Settings', category: 'Administration' },
  ] : 
  [
    { to: '/', icon: <LayoutDashboard className="w-5 h-5" />, label: 'Dashboard', category: 'Overview' },
    { to: '/compose/projects', icon: <Package className="w-5 h-5" />, label: 'Compose Projects', category: 'Docker' },
    { to: '/containers', icon: <Container className="w-5 h-5" />, label: 'Containers', category: 'Docker' },  
    { to: '/logs', icon: <FileOutput className="w-5 h-5" />, label: 'Logs Viewer', category: 'Docker' },
  ];

  // Group items by category
  const groupedNavItems = navItems.reduce((acc, item) => {
    const category = item.category || 'Other';
    if (!acc[category]) {
      acc[category] = [];
    }
    acc[category].push(item);
    return acc;
  }, {} as Record<string, NavItem[]>);

  return (
    <aside className="w-64 bg-white dark:bg-gray-900 border-r border-gray-200 dark:border-gray-800 flex flex-col shadow-lg transition-colors duration-200">
      {/* Logo Header */}
      <div className="flex items-center h-16 px-6 border-b border-gray-200 dark:border-gray-800">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-linear-to-br from-blue-500 to-blue-600 dark:from-blue-600 dark:to-blue-700 rounded-lg shadow-md">
            <Boxes className="w-5 h-5 text-white" />
          </div>
          <div>
            <span className="font-bold text-lg tracking-tight text-gray-900 dark:text-white">DCM</span>
            <p className="text-xs text-gray-500 dark:text-gray-400">Compose Manager</p>
          </div>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 overflow-y-auto py-4">
        {Object.entries(groupedNavItems).map(([category, items]) => (
          <div key={category} className="mb-6">
            <div className="px-6 mb-2">
              <h3 className="text-xs font-semibold text-gray-400 dark:text-gray-500 uppercase tracking-wider">
                {category}
              </h3>
            </div>
            <ul className="space-y-1 px-3">
              {items.map((item) => (
                <li key={item.to}>
                  <NavLink
                    to={item.to}
                    end={item.to === '/'}
                    className={({ isActive }) =>
                      `flex items-center gap-3 px-4 py-2.5 rounded-lg transition-all duration-200 group relative ${
                        isActive
                          ? 'bg-blue-50 dark:bg-blue-900/20 text-blue-600 dark:text-blue-400 shadow-sm'
                          : 'text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800/50 hover:text-gray-900 dark:hover:text-white'
                      }`
                    }
                  >
                    {({ isActive }) => (
                      <>
                        {/* Active indicator */}
                        {isActive && (
                          <span className="absolute left-0 top-1/2 -translate-y-1/2 w-1 h-8 bg-blue-600 dark:bg-blue-500 rounded-r-full" />
                        )}
                        <span className={`transition-all duration-200 ${
                          isActive ? 'scale-110' : 'group-hover:scale-105'
                        }`}>
                          {item.icon}
                        </span>
                        <span className="font-medium text-sm">{item.label}</span>
                      </>
                    )}
                  </NavLink>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </nav>

      {/* Footer */}
      <div className="p-4 border-t border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900/50">
        <div className="text-xs space-y-1">
          <p className="font-semibold text-gray-700 dark:text-gray-300">Docker Compose Manager</p>
          <p className="text-gray-500 dark:text-gray-500">Version 0.1.0</p>
        </div>
      </div>
    </aside>
  );
};
