import en from './en';
import fr from './fr';
import es from './es';

// Vérification au build (sera tree-shaken en production)
if (import.meta.env.DEV) {
  const checkKeys = (obj: any, ref: any, lang: string, path = '') => {
    for (const key in ref) {
      const currentPath = path ? `${path}.${key}` : key;
      if (!(key in obj)) {
        console.error(`Missing key in ${lang}: ${currentPath}`);
      } else if (typeof ref[key] === 'object' && ref[key] !== null) {
        checkKeys(obj[key], ref[key], lang, currentPath);
      }
    }
  };
  
  checkKeys(fr, en, 'fr');
  checkKeys(es, en, 'es');
}

export type SupportedLocale = 'en' | 'fr' | 'es';
// Type pour les clés de traduction (inféré depuis en.json)
export type TranslationKeys = typeof en;

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
  let value: any = translations[currentLocale];
  
  for (const k of keys) {
    if (typeof value === 'object' && value !== null) {
      value = value[k];
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