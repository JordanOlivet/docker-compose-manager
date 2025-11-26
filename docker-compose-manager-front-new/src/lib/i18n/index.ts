import i18n from 'i18next';
import { writable, get } from 'svelte/store';
import en from './en';
import fr from './fr';
import es from './es';
import { browser } from '$app/environment';

export type Locale = 'en' | 'fr' | 'es';

export interface AvailableLocale {
  code: Locale;
  name: string;
}

export const availableLocales: AvailableLocale[] = [
  { code: 'en', name: 'English' },
  { code: 'fr', name: 'Français' },
  { code: 'es', name: 'Español' }
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
    escapeValue: false // Svelte handles escaping
  }
});

// Reactive store for i18n
export const locale = writable<Locale>(i18n.language as Locale);

// Update localStorage when locale changes
locale.subscribe((lng) => {
  if (browser) {
    localStorage.setItem('locale', lng);
    i18n.changeLanguage(lng);
  }
});

// Translation function
export function t(key: string, options?: Record<string, unknown>): string {
  return i18n.t(key, options);
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

// Listen to language changes
i18n.on('languageChanged', (lng) => {
  locale.set(lng as Locale);
});

export default i18n;
