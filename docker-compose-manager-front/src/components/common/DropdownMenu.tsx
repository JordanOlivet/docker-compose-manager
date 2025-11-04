import { useState, useRef, useEffect, type ReactNode } from 'react';
import { ChevronDown } from 'lucide-react';

interface DropdownMenuItem {
  label: string;
  onClick: () => void;
  icon?: ReactNode;
  variant?: 'default' | 'danger';
  disabled?: boolean;
}

interface DropdownMenuProps {
  trigger: ReactNode;
  items: DropdownMenuItem[];
  align?: 'left' | 'right';
}

export const DropdownMenu = ({ trigger, items, align = 'left' }: DropdownMenuProps) => {
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };

    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }

    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isOpen]);

  const handleItemClick = (item: DropdownMenuItem) => {
    if (!item.disabled) {
      item.onClick();
      setIsOpen(false);
    }
  };

  return (
    <div className="relative inline-block" ref={dropdownRef}>
      <div onClick={() => setIsOpen(!isOpen)} className="cursor-pointer">
        {trigger}
      </div>

      {isOpen && (
        <div
          className={`absolute z-50 mt-2 w-56 rounded-md shadow-lg bg-white ring-1 ring-black ring-opacity-5 ${
            align === 'right' ? 'right-0' : 'left-0'
          }`}
        >
          <div className="py-1" role="menu">
            {items.map((item, index) => (
              <button
                key={index}
                onClick={() => handleItemClick(item)}
                disabled={item.disabled}
                className={`
                  w-full text-left px-4 py-2 text-sm flex items-center gap-2
                  ${
                    item.variant === 'danger'
                      ? 'text-red-700 hover:bg-red-50'
                      : 'text-gray-700 hover:bg-gray-100'
                  }
                  ${item.disabled ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}
                  transition-colors
                `}
                role="menuitem"
              >
                {item.icon && <span className="w-4 h-4">{item.icon}</span>}
                <span>{item.label}</span>
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
};

interface SplitButtonProps {
  label: string;
  icon?: ReactNode;
  onClick: () => void;
  menuItems: DropdownMenuItem[];
  variant?: 'primary' | 'secondary' | 'danger';
  disabled?: boolean;
}

export const SplitButton = ({
  label,
  icon,
  onClick,
  menuItems,
  variant = 'primary',
  disabled = false,
}: SplitButtonProps) => {
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };

    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }

    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isOpen]);

  const handleItemClick = (item: DropdownMenuItem) => {
    if (!item.disabled) {
      item.onClick();
      setIsOpen(false);
    }
  };

  const getButtonClasses = () => {
    const baseClasses = 'flex items-center gap-2 px-3 py-1.5 text-sm font-medium transition-colors';

    if (disabled) {
      return `${baseClasses} opacity-50 cursor-not-allowed bg-gray-300 text-gray-500`;
    }

    switch (variant) {
      case 'primary':
        return `${baseClasses} text-white bg-green-600 hover:bg-green-700`;
      case 'secondary':
        return `${baseClasses} text-gray-700 bg-gray-100 hover:bg-gray-200`;
      case 'danger':
        return `${baseClasses} text-white bg-red-600 hover:bg-red-700`;
      default:
        return baseClasses;
    }
  };

  return (
    <div className="relative inline-block" ref={dropdownRef}>
      <div className="flex">
        <button
          onClick={onClick}
          disabled={disabled}
          className={`${getButtonClasses()} rounded-l-md`}
        >
          {icon}
          {label}
        </button>
        <button
          onClick={() => setIsOpen(!isOpen)}
          disabled={disabled}
          className={`${getButtonClasses()} rounded-r-md border-l border-opacity-20 ${
            variant === 'primary' ? 'border-white' : 'border-gray-700'
          }`}
        >
          <ChevronDown className="w-4 h-4" />
        </button>
      </div>

      {isOpen && (
        <div className="absolute z-50 mt-2 left-0 w-64 rounded-md shadow-lg bg-white ring-1 ring-black ring-opacity-5">
          <div className="py-1" role="menu">
            {menuItems.map((item, index) => (
              <button
                key={index}
                onClick={() => handleItemClick(item)}
                disabled={item.disabled}
                className={`
                  w-full text-left px-4 py-2 text-sm flex items-center gap-2
                  ${
                    item.variant === 'danger'
                      ? 'text-red-700 hover:bg-red-50'
                      : 'text-gray-700 hover:bg-gray-100'
                  }
                  ${item.disabled ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}
                  transition-colors
                `}
                role="menuitem"
              >
                {item.icon && <span className="w-4 h-4">{item.icon}</span>}
                <span>{item.label}</span>
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
};
