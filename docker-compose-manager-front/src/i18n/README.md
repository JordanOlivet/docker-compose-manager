# Système de traduction i18next

Ce projet utilise **react-i18next** pour gérer les traductions multilingues.

## 📚 Langues supportées

- 🇬🇧 Anglais (en) - langue par défaut
- 🇫🇷 Français (fr)
- 🇪🇸 Espagnol (es)

## 🚀 Utilisation

### Dans un composant fonctionnel

```tsx
import { useTranslation } from 'react-i18next';

function MyComponent() {
  const { t } = useTranslation();
  
  return <h1>{t('common.welcome')}</h1>;
}
```

### Avec interpolation

```tsx
const { t } = useTranslation();

// Dans les traductions: "Hello {name}!"
<p>{t('greeting', { name: 'Jordan' })}</p>
```

### Changer de langue

```tsx
import { useTranslation } from 'react-i18next';

function LanguageSwitcher() {
  const { i18n } = useTranslation();
  
  const changeLanguage = (lng: string) => {
    i18n.changeLanguage(lng);
  };
  
  return (
    <button onClick={() => changeLanguage('fr')}>
      Français
    </button>
  );
}
```

### Dans un composant de classe

```tsx
import { withTranslation, WithTranslation } from 'react-i18next';

interface Props extends WithTranslation {
  // vos props
}

class MyClassComponent extends Component<Props> {
  render() {
    const { t } = this.props;
    return <h1>{t('common.title')}</h1>;
  }
}

export default withTranslation()(MyClassComponent);
```

## 📁 Structure des fichiers

```
i18n/
├── config.ts       # Configuration i18next
├── en.ts          # Traductions anglaises (référence)
├── fr.ts          # Traductions françaises
├── es.ts          # Traductions espagnoles
└── README.md      # Ce fichier
```

## ✅ Bonnes pratiques

1. **Toujours utiliser des clés structurées** : `section.subsection.key`
2. **Le fichier `en.ts` est la référence** : toutes les clés doivent y être présentes
3. **Utiliser TypeScript** : les types sont inférés automatiquement depuis `en.ts`
4. **Interpolation** : utiliser `{variable}` dans les chaînes et passer un objet `{ variable: value }`
5. **Ajouter des commentaires** pour les traductions complexes

## 🔧 Ajouter une nouvelle traduction

1. Ajouter la clé dans `en.ts` :
```typescript
export default {
  mySection: {
    myKey: 'My English text'
  }
}
```

2. Ajouter la même clé dans `fr.ts` et `es.ts`

3. Utiliser dans votre composant :
```tsx
const { t } = useTranslation();
return <p>{t('mySection.myKey')}</p>;
```

## 🌍 Persistence

La langue sélectionnée est automatiquement sauvegardée dans `localStorage` et restaurée au chargement de l'application.

## 📖 Documentation

- [react-i18next](https://react.i18next.com/)
- [i18next](https://www.i18next.com/)
