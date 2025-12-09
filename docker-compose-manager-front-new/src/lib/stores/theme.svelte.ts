import { browser } from '$app/environment';

export type Theme = 'light' | 'dark';

function createThemeStore() {
  let theme = $state<Theme>(getInitialTheme());

  function getInitialTheme(): Theme {
    if (!browser) return 'light';
    
    // Check localStorage first
    const savedTheme = localStorage.getItem('theme') as Theme | null;
    if (savedTheme) {
      return savedTheme;
    }
    // Check system preference
    if (window.matchMedia('(prefers-color-scheme: dark)').matches) {
      return 'dark';
    }
    return 'light';
  }

  function applyTheme(newTheme: Theme) {
    if (!browser) return;

    const root = document.documentElement;
    if (newTheme === 'dark') {
      root.classList.add('dark');
    } else {
      root.classList.remove('dark');
    }
    localStorage.setItem('theme', newTheme);
  }

  // Apply initial theme on store creation
  // Note: The Svelte warning about capturing initial value is expected here
  // and is intentional for this use case
  if (browser) {
    const initialTheme = theme;
    const root = document.documentElement;
    if (initialTheme === 'dark') {
      root.classList.add('dark');
    } else {
      root.classList.remove('dark');
    }
  }

  return {
    get current() { return theme; },
    get isDark() { return theme === 'dark'; },

    toggle() {
      theme = theme === 'light' ? 'dark' : 'light';
      applyTheme(theme);
    },

    set(newTheme: Theme) {
      theme = newTheme;
      applyTheme(theme);
    },
  };
}

export const themeStore = createThemeStore();
