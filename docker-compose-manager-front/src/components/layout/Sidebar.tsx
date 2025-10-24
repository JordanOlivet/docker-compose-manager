import { NavLink } from 'react-router-dom';
import {
  LayoutDashboard,
  FileText,
  Package,
  Container,
  ClipboardList,
  Users,
  FileOutput,
  Settings
} from 'lucide-react';

interface SidebarProps {
  isOpen: boolean;
  onToggle: () => void;
}

interface NavItem {
  to: string;
  icon: React.ReactNode;
  label: string;
}

const navItems: NavItem[] = [
  { to: '/', icon: <LayoutDashboard className="w-5 h-5" />, label: 'Dashboard' },
  { to: '/containers', icon: <Container className="w-5 h-5" />, label: 'Containers' },
  { to: '/compose/files', icon: <FileText className="w-5 h-5" />, label: 'Compose Files' },
  { to: '/compose/projects', icon: <Package className="w-5 h-5" />, label: 'Projects' },
  { to: '/logs', icon: <FileOutput className="w-5 h-5" />, label: 'Logs Viewer' },
  { to: '/audit', icon: <ClipboardList className="w-5 h-5" />, label: 'Audit Logs' },
  { to: '/users', icon: <Users className="w-5 h-5" />, label: 'User Management' },
  { to: '/settings', icon: <Settings className="w-5 h-5" />, label: 'Settings' },
];

export const Sidebar = ({ isOpen }: SidebarProps) => {
  if (!isOpen) return null;

  return (
    <aside className="w-64 bg-gradient-to-b from-gray-900 to-gray-950 dark:from-gray-950 dark:to-black text-white flex flex-col shadow-xl">
      {/* Logo Header */}
      <div className="flex items-center h-16 px-6 border-b border-gray-800/50 dark:border-gray-700/50">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-blue-600 rounded-lg shadow-lg">
            <Package className="w-5 h-5 text-white" />
          </div>
          <div>
            <span className="font-bold text-lg tracking-tight">DCM</span>
            <p className="text-xs text-gray-400">Compose Manager</p>
          </div>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 overflow-y-auto py-6">
        <ul className="space-y-1.5 px-3">
          {navItems.map((item) => (
            <li key={item.to}>
              <NavLink
                to={item.to}
                end={item.to === '/'}
                className={({ isActive }) =>
                  `flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group ${
                    isActive
                      ? 'bg-gradient-to-r from-blue-600 to-blue-700 text-white shadow-lg shadow-blue-500/50'
                      : 'text-gray-300 hover:bg-gray-800/50 hover:text-white'
                  }`
                }
              >
                <span className={`transition-transform duration-200 ${
                  'group-hover:scale-110'
                }`}>
                  {item.icon}
                </span>
                <span className="font-medium text-sm">{item.label}</span>
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>

      {/* Footer */}
      <div className="p-4 border-t border-gray-800/50 dark:border-gray-700/50 bg-gray-900/50">
        <div className="text-xs text-gray-500 space-y-1">
          <p className="font-medium text-gray-400">Docker Compose Manager</p>
          <p className="text-gray-600">Version 1.0.0</p>
        </div>
      </div>
    </aside>
  );
};
