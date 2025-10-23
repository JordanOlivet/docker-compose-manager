import { NavLink } from 'react-router-dom';
import {
  LayoutDashboard,
  FileText,
  Package,
  Container,
  ClipboardList,
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
  { to: '/audit', icon: <ClipboardList className="w-5 h-5" />, label: 'Audit Logs' },
  { to: '/settings', icon: <Settings className="w-5 h-5" />, label: 'Settings' },
];

export const Sidebar = ({ isOpen }: SidebarProps) => {
  if (!isOpen) return null;

  return (
    <aside className="w-64 bg-gray-900 text-white flex flex-col">
      <div className="flex items-center h-16 px-6 border-b border-gray-800">
        <div className="flex items-center gap-2">
          <Package className="w-6 h-6 text-blue-500" />
          <span className="font-semibold text-lg">DCM</span>
        </div>
      </div>

      <nav className="flex-1 overflow-y-auto py-4">
        <ul className="space-y-1 px-3">
          {navItems.map((item) => (
            <li key={item.to}>
              <NavLink
                to={item.to}
                end={item.to === '/'}
                className={({ isActive }) =>
                  `flex items-center gap-3 px-3 py-2.5 rounded-lg transition-colors ${
                    isActive
                      ? 'bg-blue-600 text-white'
                      : 'text-gray-300 hover:bg-gray-800 hover:text-white'
                  }`
                }
              >
                {item.icon}
                <span className="font-medium">{item.label}</span>
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>

      <div className="p-4 border-t border-gray-800">
        <div className="text-xs text-gray-500">
          <p>Docker Compose Manager</p>
          <p>Version 1.0.0</p>
        </div>
      </div>
    </aside>
  );
};
