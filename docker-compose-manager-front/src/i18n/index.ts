import { en, type TranslationKeys } from './en';
import { fr } from './fr';
import { es } from './es';

export type SupportedLocale = 'en' | 'fr' | 'es';

// Récupérer la langue sauvegardée ou utiliser 'en' par défaut
const savedLocale = (localStorage.getItem('locale') as SupportedLocale) || 'en';
let currentLocale: SupportedLocale = savedLocale;

const translations: Record<SupportedLocale, TranslationKeys> = {
  en,
  fr,
  es,
};

/**
 * Récupère une traduction par chemin (ex: 'common.loading')
 * Supporte l'interpolation de variables (ex: t('common.noAvailable', { type: 'user' }))
 */
export function t(key: string, params?: Record<string, string | number>): string {
  const keys = key.split('.');
  let value: TranslationKeys | string | undefined = translations[currentLocale];
  
  for (const k of keys) {
    if (typeof value === 'object' && value !== null) {
      value = (value as Record<string, unknown>)[k] as TranslationKeys | string | undefined;
    } else {
      value = undefined;
    }
    if (value === undefined) {
      console.warn(`Translation key not found: ${key}`);
      return key;
    }
  }
  
  let result = typeof value === 'string' ? value : key;
  
  // Interpolation des variables : {variable} -> valeur
  if (params) {
    Object.entries(params).forEach(([paramKey, paramValue]) => {
      result = result.replace(new RegExp(`\\{${paramKey}\\}`, 'g'), String(paramValue));
    });
  }
  
  return result;
}

/**
 * Change la langue active et la sauvegarde dans le localStorage
 */
export function setLocale(locale: SupportedLocale) {
  currentLocale = locale;
  localStorage.setItem('locale', locale);
}

/**
 * Retourne la langue active
 */
export function getLocale(): SupportedLocale {
  return currentLocale;
}

// Export des traductions pour usage direct si besoin
export { en, fr, es };
export type { TranslationKeys };
