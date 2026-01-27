# Plan de Migration : Découverte Docker-Only pour Frontend Svelte

> **Date de création :** 2026-01-08
> **Branche :** Revamp-compose-discover-mecanism
> **Estimation :** 18-24 heures (2-3 jours)

## Vue d'Ensemble

Cette migration transforme le système de découverte des projets Compose en éliminant la persistance en base de données (tables `ComposePaths` et `ComposeFiles`) au profit d'une approche **Docker-only** où `docker compose ls --all` devient la source unique de vérité. Le système utilisera un cache mémoire de 10 secondes pour les performances.

**PRINCIPE FONDAMENTAL : Conserver 100% des Fonctionnalités**
- ✅ **Pas de changement de DTO** : `ComposeProjectDto` actuel conservé tel quel
- ✅ **Pas de changement d'EntityState** : Utilise l'enum existant (Running, Stopped, etc.)
- ✅ **Pas de changement frontend** : Types TypeScript inchangés, composants fonctionnent tel quel
- ✅ **Changement uniquement de SOURCE** : Docker API au lieu de DB pour découvrir les projets

**Objectifs :**
- ✅ Conserver 100% des fonctionnalités de gestion des projets compose et containers
- ✅ Interface visuelle inchangée (même apparence, même données)
- ✅ Supprimer les tables ComposePaths et ComposeFiles (source de vérité = Docker)
- ✅ Désactiver l'édition de fichiers et templates (retourner HTTP 501)
- ✅ Masquer les boutons d'accès aux fonctionnalités désactivées dans le frontend Svelte
- ✅ Frontend Svelte fonctionne sans modification (même structure de données)

**Ce qui change :**
- 🔄 Source de découverte : `docker compose ls --all` + `docker compose ps` au lieu de DB
- 🗑️ Supprimer : Tables ComposePaths/ComposeFiles, background service de sync
- 💾 Cache : Mémoire 10s au lieu de sync DB périodique
- 🚫 Désactiver : Endpoints édition/templates (HTTP 501)

**Ce qui reste identique :**
- ✅ ComposeProjectDto actuel (Name, Path, State, Services, ComposeFiles, LastUpdated)
- ✅ ComposeServiceDto actuel (Id, Name, Image, State, Status, Ports, Health)
- ✅ EntityState existant (pas de nouveau enum)
- ✅ Types frontend TypeScript inchangés
- ✅ Composants Svelte fonctionnent sans modification

---

## Résumé de l'Approche

**Le Concept Clé : Changer la SOURCE, pas la STRUCTURE**

L'ancien système :
```
DB (ComposePaths/ComposeFiles) → ComposeProjectDto → Frontend
```

Le nouveau système :
```
Docker (`docker compose ls` + `docker compose ps`) → ComposeProjectDto → Frontend
                                                      (MÊME STRUCTURE)
```

**ComposeDiscoveryService - Flux de Données :**

1. **Découverte des projets :**
   ```bash
   docker compose ls --all --format json
   # Retourne : [{ Name: "myapp", Status: "running(3)", ConfigFiles: "/path/..." }]
   ```

2. **Pour chaque projet, récupérer les containers :**
   ```bash
   docker compose -p myapp ps --format json
   # Retourne : [{ ID: "abc123", Name: "myapp-web-1", State: "running", ... }]
   ```

3. **Mapper vers DTO existant :**
   ```csharp
   new ComposeProjectDto(
       Name: "myapp",                          // De docker compose ls
       Path: "/path/to/compose",               // Extrait de ConfigFiles (pour futur)
       State: EntityState.Running,             // Converti depuis Status
       Services: List<ComposeServiceDto>,      // Depuis docker compose ps
       ComposeFiles: ["/path/..."],            // De docker compose ls
       LastUpdated: DateTime.UtcNow
   )
   ```

4. **Le frontend reçoit exactement la même structure qu'avant** ✅

---

## Phase 1 : Infrastructure Backend (2-3h)

### 1.1 Vérifier les DTOs Existants

**Fichier :** `docker-compose-manager-back/src/DTOs/ComposeDtos.cs`

**Actions :** Les DTOs actuels sont conservés tels quels. Ajouter uniquement si pas déjà présent :
```csharp
public record OperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; }
    public string? Output { get; init; }
    public string? Error { get; init; }
}
```

### 1.2 Créer DockerCommandExecutor

**Fichier :** `docker-compose-manager-back/src/Services/DockerCommandExecutor.cs` (NOUVEAU)

Extraire depuis ComposeService.cs :
- `IsComposeV2Available()`
- `ExecuteComposeCommandAsync()`

### 1.3 Créer les Interfaces

**Fichier :** `docker-compose-manager-back/src/Services/IComposeDiscoveryService.cs` (NOUVEAU)
**Fichier :** `docker-compose-manager-back/src/Services/IComposeOperationService.cs` (NOUVEAU)

---

## Phase 2 : Services Backend (5-6h)

### 2.1 ComposeDiscoveryService

**Fichier :** `docker-compose-manager-back/src/Services/ComposeDiscoveryService.cs` (NOUVEAU)

**Méthodes clés :**
- `FetchProjectsFromDockerAsync()` : Appelle `docker compose ls --all` + `docker compose ps` pour chaque projet
- `MapDockerStatusToEntityState()` : Convertit vers EntityState existant
- `MapContainersToServices()` : Convertit vers ComposeServiceDto existant
- `GetProjectsForUserAsync()` : Avec cache 10s et filtrage permissions

### 2.2 ComposeOperationService

**Fichier :** `docker-compose-manager-back/src/Services/ComposeOperationService.cs` (NOUVEAU)

**Méthodes :** UpAsync, DownAsync, RestartAsync, StopAsync, StartAsync
Toutes utilisent `-p projectName` sans accès aux fichiers.

---

## Phase 3 : API et Base de Données (4-5h)

### 3.1 Refactoriser ComposeController

**Fichier :** `docker-compose-manager-back/src/Controllers/ComposeController.cs`

- Nouveaux endpoints : GET/POST `/projects/*`
- Déprécier endpoints : `/files/*`, `/templates` (retourner HTTP 501)

### 3.2 Mettre à Jour Program.cs

- Ajouter `AddMemoryCache()`
- Enregistrer nouveaux services
- Commenter `AddHostedService<ComposeFileDiscoveryService>()`

### 3.3 Marquer Modèles Obsolètes

- ComposePath.cs
- ComposeFile.cs

### 3.4 Migration Base de Données

```bash
dotnet ef migrations add RemoveComposePathsAndFiles
```

⚠️ Ne pas appliquer avant tests !

---

## Phase 4 : Frontend Svelte (2-3h)

### 4.1 Types TypeScript

**Aucune modification** - Les types restent identiques.

### 4.2 Feature Flags

**Fichier :** `docker-compose-manager-front-new/src/lib/config/features.ts` (NOUVEAU)

```typescript
export const FEATURES = {
  COMPOSE_FILE_EDITING: false,
  COMPOSE_TEMPLATES: false,
} as const;
```

### 4.3 Masquer Boutons Edit

Utiliser feature flags dans :
- ProjectInfoSection.svelte
- compose/files/+page.svelte (ajouter message "désactivé")

---

## Phase 5 : Tests (3-4h)

### 5.1 Vérifications

- TypeScript : `npm run check` (devrait passer sans erreur)
- Backend : `dotnet build`

### 5.2 Tests Manuels

1. Découverte de projets
2. Opérations (up, down, restart)
3. Fonctionnalités désactivées
4. Permissions
5. Cas limites

### 5.3 Application Migration

```bash
# Backup
cp Data/app.db Data/app.db.backup

# Appliquer
dotnet ef database update
```

---

## Phase 6 : Documentation (2-3h)

- Mettre à jour CLAUDE.md
- Créer MIGRATION_GUIDE.md
- Mettre à jour README.md

---

## Critères de Succès

### Fonctionnel
- ✅ Tous les projets découverts depuis Docker
- ✅ Toutes les opérations fonctionnent
- ✅ Permissions correctes
- ✅ SignalR temps réel
- ✅ Édition désactivée (HTTP 501)

### Performance
- ✅ Liste projets < 300ms (avec cache)
- ✅ Taux hit cache > 80%
- ✅ Pas de requêtes N+1

### Qualité
- ✅ Aucune erreur TypeScript
- ✅ Aucun warning build
- ✅ Documentation à jour

---

## Notes Importantes

**Compatibilité Totale Préservée :**
- DTOs inchangés → Frontend fonctionne sans modification
- EntityState inchangé → Pas de nouveaux enums
- Changement uniquement de SOURCE (Docker au lieu de DB)

**Réactivation Future :**
- Code d'édition conservé
- Propriété Path conservée dans DTO
- Routes frontend conservées (avec message "désactivé")

**Pourquoi Cette Approche :**
- Pas de "big bang" → Moins de risques
- Types identiques → Pas de bugs de compatibilité
- Tests simplifiés → Frontend "juste fonctionne"
- Rollback facile → Restauration DB + code

---

## Fichiers Critiques

**Backend :**
1. DockerCommandExecutor.cs (NOUVEAU)
2. ComposeDiscoveryService.cs (NOUVEAU)
3. ComposeOperationService.cs (NOUVEAU)
4. ComposeController.cs (REFACTOR)
5. Program.cs (SERVICES)
6. AppDbContext.cs (MIGRATION)

**Frontend :**
1. features.ts (NOUVEAU)
2. ProjectInfoSection.svelte (FEATURE FLAGS)
3. compose/files/+page.svelte (MESSAGE DÉSACTIVÉ)

---

**Estimation Totale :** 18-24 heures (2-3 jours de développement)
