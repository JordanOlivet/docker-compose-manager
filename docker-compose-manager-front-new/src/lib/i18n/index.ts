import i18n from 'i18next';
import { writable, derived, get } from 'svelte/store';
import en from './en';
import fr from './fr';
import es from './es';
import { browser } from '$app/environment';

export type Locale = 'en' | 'fr' | 'es';

export interface AvailableLocale {
  code: Locale;
  name: string;
  flag: string;
}

export const availableLocales: AvailableLocale[] = [
  { code: 'en', name: 'English', flag: '🇬🇧' },
  { code: 'fr', name: 'Français', flag: '🇫🇷' },
  { code: 'es', name: 'Español', flag: '🇪🇸' }
];

const savedLocale = browser ? (localStorage.getItem('locale') as Locale) || 'en' : 'en';

i18n.init({
  resources: {
    en: { translation: en },
    fr: { translation: fr },
    es: { translation: es }
  },
  lng: savedLocale,
  fallbackLng: 'en',
  interpolation: {
    escapeValue: false, // Svelte handles escaping
    prefix: '{',
    suffix: '}'
  }
});

// Reactive store for i18n
export const locale = writable<Locale>(i18n.language as Locale);

// Counter that increments on language change to force reactivity
const translationVersion = writable<number>(0);

// Update localStorage and i18n when locale changes
locale.subscribe((lng) => {
  if (browser) {
    localStorage.setItem('locale', lng);
    i18n.changeLanguage(lng);
    // Increment version to trigger reactivity
    translationVersion.update(v => v + 1);
  }
});

// Reactive translation store for use in templates with $t('key') syntax
// This will cause components to re-render when language changes
export const t = derived(
  translationVersion,
  () => {
    return (key: string, options?: Record<string, unknown>): string => {
      return i18n.t(key, options);
    };
  }
);

// Helper to get current translation function (for use in non-reactive contexts like callbacks)
export function getT(): (key: string, options?: Record<string, unknown>) => string {
  return (key: string, options?: Record<string, unknown>) => i18n.t(key, options);
}

// Reactive translation store - matches what LanguageSelector expects
export const i18nStore = {
  subscribe: locale.subscribe,
  t: (key: string, options?: Record<string, unknown>) => i18n.t(key, options),
  setLocale: (lng: Locale) => {
    locale.set(lng);
  },
  get locale() {
    return get(locale);
  }
};

// Listen to language changes from i18next
i18n.on('languageChanged', (lng) => {
  locale.set(lng as Locale);
});

export default i18n;
