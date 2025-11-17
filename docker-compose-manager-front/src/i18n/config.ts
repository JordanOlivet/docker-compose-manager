import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import en from './en';
import fr from './fr';
import es from './es';

// Récupérer la langue sauvegardée ou utiliser 'en' par défaut
const savedLocale = localStorage.getItem('locale') || 'en';

i18n
  .use(initReactI18next)
  .init({
    resources: {
      en: { translation: en },
      fr: { translation: fr },
      es: { translation: es },
    },
    lng: savedLocale,
    fallbackLng: 'en',
    interpolation: {
      escapeValue: false, // React already escapes values
    },
    react: {
      useSuspense: false,
    },
  });

// Sauvegarder la langue quand elle change
i18n.on('languageChanged', (lng) => {
  localStorage.setItem('locale', lng);
});

export default i18n;
