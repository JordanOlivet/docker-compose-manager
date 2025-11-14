/**
 * Palette de couleurs et tokens de thème centralisés
 * Pour assurer la cohérence visuelle à travers l'application
 */

export const colors = {
  // Primary (Blue)
  primary: {
    50: '#eff6ff',
    100: '#dbeafe',
    200: '#bfdbfe',
    300: '#93c5fd',
    400: '#60a5fa',
    500: '#3b82f6',
    600: '#2563eb',
    700: '#1d4ed8',
    800: '#1e40af',
    900: '#1e3a8a',
  },
  
  // Success (Green)
  success: {
    50: '#f0fdf4',
    100: '#dcfce7',
    200: '#bbf7d0',
    300: '#86efac',
    400: '#4ade80',
    500: '#22c55e',
    600: '#16a34a',
    700: '#15803d',
    800: '#166534',
    900: '#14532d',
  },
  
  // Warning (Yellow/Orange)
  warning: {
    50: '#fffbeb',
    100: '#fef3c7',
    200: '#fde68a',
    300: '#fcd34d',
    400: '#fbbf24',
    500: '#f59e0b',
    600: '#d97706',
    700: '#b45309',
    800: '#92400e',
    900: '#78350f',
  },
  
  // Danger (Red)
  danger: {
    50: '#fef2f2',
    100: '#fee2e2',
    200: '#fecaca',
    300: '#fca5a5',
    400: '#f87171',
    500: '#ef4444',
    600: '#dc2626',
    700: '#b91c1c',
    800: '#991b1b',
    900: '#7f1d1d',
  },
  
  // Info (Cyan)
  info: {
    50: '#ecfeff',
    100: '#cffafe',
    200: '#a5f3fc',
    300: '#67e8f9',
    400: '#22d3ee',
    500: '#06b6d4',
    600: '#0891b2',
    700: '#0e7490',
    800: '#155e75',
    900: '#164e63',
  },
  
  // Gray (Neutral)
  gray: {
    50: '#f9fafb',
    100: '#f3f4f6',
    200: '#e5e7eb',
    300: '#d1d5db',
    400: '#9ca3af',
    500: '#6b7280',
    600: '#4b5563',
    700: '#374151',
    800: '#1f2937',
    900: '#111827',
  },
} as const;

/**
 * Variantes de couleur pour les états d'entité
 */
export const stateColors = {
  running: colors.success[600],
  stopped: colors.gray[500],
  paused: colors.warning[500],
  exited: colors.gray[400],
  restarting: colors.info[500],
  removing: colors.danger[500],
  dead: colors.danger[700],
  created: colors.info[400],
} as const;

/**
 * Bordures et ombres
 */
export const borders = {
  radius: {
    sm: '0.375rem',   // 6px
    md: '0.5rem',     // 8px
    lg: '0.75rem',    // 12px
    xl: '1rem',       // 16px
    '2xl': '1.5rem',  // 24px
    full: '9999px',
  },
  width: {
    thin: '1px',
    normal: '2px',
    thick: '4px',
  },
} as const;

export const shadows = {
  sm: '0 1px 2px 0 rgb(0 0 0 / 0.05)',
  md: '0 4px 6px -1px rgb(0 0 0 / 0.1), 0 2px 4px -2px rgb(0 0 0 / 0.1)',
  lg: '0 10px 15px -3px rgb(0 0 0 / 0.1), 0 4px 6px -4px rgb(0 0 0 / 0.1)',
  xl: '0 20px 25px -5px rgb(0 0 0 / 0.1), 0 8px 10px -6px rgb(0 0 0 / 0.1)',
  '2xl': '0 25px 50px -12px rgb(0 0 0 / 0.25)',
  inner: 'inset 0 2px 4px 0 rgb(0 0 0 / 0.05)',
} as const;

/**
 * Espacements standards
 */
export const spacing = {
  xs: '0.25rem',   // 4px
  sm: '0.5rem',    // 8px
  md: '1rem',      // 16px
  lg: '1.5rem',    // 24px
  xl: '2rem',      // 32px
  '2xl': '3rem',   // 48px
  '3xl': '4rem',   // 64px
} as const;

/**
 * Typographie
 */
export const typography = {
  fontFamily: {
    sans: 'Inter, system-ui, -apple-system, sans-serif',
    mono: 'JetBrains Mono, Consolas, monospace',
  },
  fontSize: {
    xs: '0.75rem',     // 12px
    sm: '0.875rem',    // 14px
    base: '1rem',      // 16px
    lg: '1.125rem',    // 18px
    xl: '1.25rem',     // 20px
    '2xl': '1.5rem',   // 24px
    '3xl': '1.875rem', // 30px
    '4xl': '2.25rem',  // 36px
  },
  fontWeight: {
    normal: 400,
    medium: 500,
    semibold: 600,
    bold: 700,
  },
  lineHeight: {
    tight: 1.25,
    normal: 1.5,
    relaxed: 1.75,
  },
} as const;

/**
 * Z-index hiérarchie
 */
export const zIndex = {
  base: 0,
  dropdown: 1000,
  sticky: 1020,
  fixed: 1030,
  modalBackdrop: 1040,
  modal: 1050,
  popover: 1060,
  tooltip: 1070,
} as const;

/**
 * Transitions et animations
 */
export const transitions = {
  duration: {
    fast: '150ms',
    normal: '200ms',
    slow: '300ms',
  },
  timing: {
    ease: 'ease',
    easeIn: 'ease-in',
    easeOut: 'ease-out',
    easeInOut: 'ease-in-out',
  },
} as const;

/**
 * Helper pour obtenir une classe Tailwind pour une couleur d'état
 */
export function getStateColorClass(state: keyof typeof stateColors, type: 'bg' | 'text' | 'border' = 'text'): string {
  const colorMap: Record<keyof typeof stateColors, string> = {
    running: type === 'bg' ? 'bg-green-600 dark:bg-green-700' : type === 'text' ? 'text-green-600 dark:text-green-400' : 'border-green-600',
    stopped: type === 'bg' ? 'bg-gray-500 dark:bg-gray-600' : type === 'text' ? 'text-gray-500 dark:text-gray-400' : 'border-gray-500',
    paused: type === 'bg' ? 'bg-yellow-500 dark:bg-yellow-600' : type === 'text' ? 'text-yellow-500 dark:text-yellow-400' : 'border-yellow-500',
    exited: type === 'bg' ? 'bg-gray-400 dark:bg-gray-500' : type === 'text' ? 'text-gray-400 dark:text-gray-500' : 'border-gray-400',
    restarting: type === 'bg' ? 'bg-cyan-500 dark:bg-cyan-600' : type === 'text' ? 'text-cyan-500 dark:text-cyan-400' : 'border-cyan-500',
    removing: type === 'bg' ? 'bg-red-500 dark:bg-red-600' : type === 'text' ? 'text-red-500 dark:text-red-400' : 'border-red-500',
    dead: type === 'bg' ? 'bg-red-700 dark:bg-red-800' : type === 'text' ? 'text-red-700 dark:text-red-600' : 'border-red-700',
    created: type === 'bg' ? 'bg-cyan-400 dark:bg-cyan-500' : type === 'text' ? 'text-cyan-400 dark:text-cyan-500' : 'border-cyan-400',
  };
  
  return colorMap[state] || colorMap.stopped;
}
