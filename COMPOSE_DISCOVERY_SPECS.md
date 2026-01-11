# Spécifications : Découverte et Association des Fichiers Compose

## Contexte

Suite à la migration vers "docker only discovery", les commandes `docker compose up`, `down`, etc. ne peuvent plus être exécutées car le système ne sait plus quel fichier compose utiliser pour un projet Docker donné.

## Problème

- Les projets Docker sont découverts via l'API Docker
- Lorsqu'on veut exécuter une commande compose (up, down, restart, etc.), le système ne sait pas quel fichier compose.yml utiliser
- Les fichiers compose qui n'ont jamais été utilisés pour un `docker compose up` n'apparaissent pas dans l'interface

## Résumé de la Solution

**Approche : Découverte automatique avec dossier unique**

1. **Dossier unique obligatoire** : `/app/compose-files` monté via Docker volume
2. **Scan universel** : Tous les fichiers `.yml` et `.yaml`, quelle que soit la convention de nommage
3. **Validation structurelle** : Seuls les fichiers avec clé `services` valide sont retenus
4. **Scan récursif** : Jusqu'à 5 niveaux de profondeur
5. **Limite de taille** : 1 MB max par fichier (configurable) pour éviter les abus
6. **Matching intelligent** : Associer projets Docker ↔ fichiers compose par nom
7. **Gestion des conflits** : Label `x-disabled: true` pour gérer plusieurs fichiers avec même nom de projet
8. **Cache performant** : 10 secondes pour éviter les ralentissements
9. **Initialisation non-bloquante** : Premier scan en arrière-plan après démarrage complet
10. **Mode dégradé** : Fonctionne en lecture seule si dossier inaccessible
11. **Suppression de ComposePaths** : Simplification radicale (breaking change)

**Avantages :**
- ✅ Configuration ultra-simple (un seul chemin)
- ✅ Sécurité renforcée (zone délimitée, limite de taille 1 MB configurable)
- ✅ Découverte automatique (pas de configuration manuelle)
- ✅ **Flexibilité totale de nommage** : `myapp.yml`, `prod.yaml`, `stack.yml`, etc.
- ✅ **Validation intelligente** : seuls les vrais compose files sont détectés
- ✅ Projets "not-started" visibles dans l'interface
- ✅ Performance optimisée avec cache
- ✅ Cohabitation avec autres fichiers YAML (configs, etc.)
- ✅ Protection contre fichiers anormaux/malveillants (limite de taille)
- ✅ Démarrage rapide (scan initial en arrière-plan, non-bloquant)
- ✅ Résilience (mode dégradé si dossier inaccessible)

**Limitations du MVP (Phase 2 pour support complet) :**
- ⚠️ Fichiers override (`docker-compose.override.yml`) ignorés
- ⚠️ Multi-fichiers environnement (dev/prod/staging) non gérés
- ⚠️ Pas de rafraîchissement temps réel (cache TTL 10s uniquement)

**Breaking Changes :**
- ⚠️ Table `ComposePaths` supprimée
- ⚠️ Endpoints `/api/compose/paths` supprimés
- ⚠️ Volume Docker `/app/compose-files` requis
- ⚠️ Migration manuelle des fichiers compose requise

## Solution Proposée

### 1. Dossier Racine et Configuration

**Prérequis : Tous les fichiers compose doivent être dans `/app/compose-files`**

L'application utilise un **dossier unique** pour tous les fichiers compose. Ce dossier doit être monté lors du démarrage du conteneur Docker.

**Configuration :**

```json
// appsettings.json
{
  "ComposeDiscovery": {
    "RootPath": "/app/compose-files",
    "ScanDepthLimit": 5,
    "CacheDurationSeconds": 10,
    "MaxFileSizeKB": 1024  // 1 MB par défaut, configurable
  }
}
```

**Pour le développement local :**

```json
// appsettings.Development.json
{
  "ComposeDiscovery": {
    "RootPath": "C:\\Users\\Lakio\\compose-files"
  }
}
```

**Montage Docker :**

```yaml
# docker-compose.yml de l'application
services:
  backend:
    image: docker-compose-manager-back
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - ./compose-files:/app/compose-files  # ← Montage du dossier compose
      - ./data:/app/data
```

Ou via `docker run` :
```bash
docker run -v /var/run/docker.sock:/var/run/docker.sock \
           -v /host/compose-files:/app/compose-files \
           docker-compose-manager-back
```

### 2. Découverte Récursive des Fichiers Compose

Le système scanne **récursivement** le dossier `/app/compose-files` avec une **limite de profondeur de 5 niveaux**.

**Stratégie de découverte :**

Au lieu de se limiter aux conventions de nommage, le système découvre **tous les fichiers `.yml` et `.yaml`** et valide leur structure :

1. **Scan** : Tous les fichiers `*.yml` et `*.yaml` sont découverts
2. **Validation** : Chaque fichier est parsé pour vérifier s'il a une structure de compose file valide
3. **Critères de validation** :
   - Fichier YAML valide (parsable)
   - Présence de la clé `services` au niveau racine
   - Au moins un service défini dans `services`

**Avantages :**
- ✅ Flexibilité totale : les utilisateurs peuvent nommer leurs fichiers comme ils veulent (`myapp.yml`, `prod.yaml`, `stack-1.yml`, etc.)
- ✅ Pas de configuration de patterns à maintenir
- ✅ Découverte automatique de tous les fichiers compose, quelle que soit la convention

**Exemples de fichiers découverts :**
- `docker-compose.yml` ✓
- `compose.yaml` ✓
- `production.yml` ✓
- `my-stack.yaml` ✓
- `app-config.yml` ✓ (si contient `services`)
- `config.yml` ✗ (si pas de `services`)
- `README.md` ✗ (pas .yml/.yaml)

**Limite de profondeur :**

```
/app/compose-files/              # Niveau 0 (racine)
├── wordpress/                   # Niveau 1 ✓
│   └── docker-compose.yml
├── projets/                     # Niveau 1 ✓
│   ├── dev/                     # Niveau 2 ✓
│   │   └── api/                 # Niveau 3 ✓
│   │       └── backend/         # Niveau 4 ✓
│   │           └── compose.yml  # Niveau 5 ✓ (limite)
│   │               └── deep/    # Niveau 6 ✗ (ignoré)
```

**Raisons de la limite :**
- Évite les scans trop longs en cas de structure profonde
- Encourage une organisation claire des fichiers
- 5 niveaux est largement suffisant pour tous les cas d'usage

**Structure Recommandée :**

```
/app/compose-files/
├── wordpress/
│   ├── docker-compose.yml       # ✓ Découvert
│   ├── config.yml                # ✗ Ignoré (pas de 'services')
│   └── .env                      # ✗ Ignoré (pas .yml/.yaml)
├── nextcloud/
│   ├── compose.yml               # ✓ Découvert
│   └── data/
├── monitoring/
│   ├── prometheus/
│   │   ├── stack.yml             # ✓ Découvert (nom libre)
│   │   └── alerts.yaml           # ✗ Ignoré si pas de 'services'
│   └── grafana/
│       └── deployment.yaml       # ✓ Découvert (nom libre)
├── dev/
│   └── test-app/
│       ├── prod.yml              # ✓ Découvert
│       ├── dev.yaml              # ✓ Découvert
│       └── README.md             # ✗ Ignoré (pas .yml/.yaml)
└── my-custom-stack.yml           # ✓ Découvert (racine OK)
```

**Points importants :**
- ✅ Tous les noms de fichiers `.yml`/`.yaml` sont acceptés
- ✅ Fichiers à la racine ou dans des sous-dossiers (jusqu'à 5 niveaux)
- ✅ Permet de coexister avec d'autres fichiers YAML non-compose
- ⚠️ Les fichiers sans clé `services` sont silencieusement ignorés

**Extraction du nom de projet :**

Pour chaque fichier découvert, déterminer le nom du projet selon cette priorité :
1. Attribut `name` dans le fichier compose (top-level)
2. Nom du répertoire parent du fichier
3. Nom du fichier sans extension (en dernier recours)

**Exemple :**
```yaml
# /app/compose-files/myapp/docker-compose.yml
name: my-application  # ← Nom de projet = "my-application"
services:
  web:
    image: nginx
```

Si pas de `name` défini → nom de projet = "myapp" (nom du répertoire)

**Algorithme de Scan Récursif :**

```csharp
private async Task<List<DiscoveredComposeFile>> ScanComposeFilesRecursive(string rootPath, int currentDepth = 0)
{
    var discoveredFiles = new List<DiscoveredComposeFile>();
    var maxDepth = _options.ScanDepthLimit; // 5

    if (currentDepth > maxDepth)
        return discoveredFiles; // Limite atteinte

    try
    {
        // Scanner TOUS les fichiers .yml et .yaml au niveau actuel
        // Note : Sur Linux, les extensions sont case-sensitive (.yml != .YML)
        var ymlFiles = Directory.GetFiles(rootPath, "*.yml")
            .Concat(Directory.GetFiles(rootPath, "*.yaml"))
            .Concat(Directory.GetFiles(rootPath, "*.YML"))
            .Concat(Directory.GetFiles(rootPath, "*.YAML"));

        foreach (var filePath in ymlFiles)
        {
            var composeFile = await ValidateAndParseComposeFile(filePath);
            if (composeFile != null)
            {
                discoveredFiles.Add(composeFile);
            }
        }

        // Scanner récursivement les sous-répertoires
        foreach (var directory in Directory.GetDirectories(rootPath))
        {
            var subFiles = await ScanComposeFilesRecursive(directory, currentDepth + 1);
            discoveredFiles.AddRange(subFiles);
        }
    }
    catch (UnauthorizedAccessException ex)
    {
        _logger.LogWarning("Access denied to directory: {Path}", rootPath);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error scanning directory: {Path}", rootPath);
    }

    return discoveredFiles;
}

private async Task<DiscoveredComposeFile?> ValidateAndParseComposeFile(string filePath)
{
    try
    {
        var fileInfo = new FileInfo(filePath);

        // 1. Vérifier la taille (configurable, défaut 1 MB)
        var maxSizeBytes = _options.MaxFileSizeKB * 1024; // Config en KB, convert en bytes
        if (fileInfo.Length > maxSizeBytes)
        {
            _logger.LogWarning(
                "Compose file exceeds size limit: {Path} ({ActualKB} KB > {MaxKB} KB allowed)",
                filePath,
                fileInfo.Length / 1024,
                _options.MaxFileSizeKB);
            return null;
        }

        // Note: Pas de validation path traversal nécessaire ici car les chemins
        // proviennent exclusivement du scan récursif de Directory.GetFiles()
        // qui ne peut retourner que des fichiers dans l'arborescence de rootPath

        // 2. Parser le YAML
        var yamlContent = await File.ReadAllTextAsync(filePath);
        var deserializer = new DeserializerBuilder()
            .WithMaximumRecursion(10)
            .Build();

        // Note : Le parsing accepte les variables d'environnement non résolues (ex: ${VERSION})
        // Ces variables seront résolues par Docker Compose lors de l'exécution
        var composeContent = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

        // 3. Valider la structure : doit contenir 'services'
        if (composeContent == null || !composeContent.ContainsKey("services"))
        {
            _logger.LogDebug("File {Path} is not a valid compose file (no 'services' key)", filePath);
            return null;
        }

        // 4. Vérifier qu'il y a au moins un service
        var services = composeContent["services"] as Dictionary<object, object>;
        if (services == null || services.Count == 0)
        {
            _logger.LogDebug("File {Path} has no services defined", filePath);
            return null;
        }

        // 5. Extraire le nom du projet
        var projectName = ExtractProjectName(composeContent, filePath);

        // 6. Extraire la liste des services
        var serviceNames = services.Keys.Select(k => k.ToString()).ToList();

        return new DiscoveredComposeFile
        {
            FilePath = filePath,
            ProjectName = projectName,
            DirectoryPath = Path.GetDirectoryName(filePath),
            LastModified = fileInfo.LastWriteTimeUtc,
            IsValid = true,
            Services = serviceNames
        };
    }
    catch (YamlException ex)
    {
        _logger.LogDebug("File {Path} is not valid YAML: {Error}", filePath, ex.Message);
        return null;
    }
    catch (OutOfMemoryException ex)
    {
        _logger.LogError(ex, "Out of memory while parsing {Path}. File may be corrupted or malicious.", filePath);
        return null;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error parsing compose file: {Path}", filePath);
        return null;
    }
}

private string ExtractProjectName(Dictionary<string, object> composeContent, string filePath)
{
    // 1. Priorité : attribut 'name' dans le fichier
    if (composeContent.ContainsKey("name"))
    {
        return composeContent["name"]?.ToString() ?? GetDefaultProjectName(filePath);
    }

    // 2. Fallback : nom du répertoire parent
    return GetDefaultProjectName(filePath);
}

private string GetDefaultProjectName(string filePath)
{
    var directory = Path.GetDirectoryName(filePath);
    if (!string.IsNullOrEmpty(directory))
    {
        var directoryName = new DirectoryInfo(directory).Name;
        if (!string.IsNullOrEmpty(directoryName))
            return directoryName;
    }

    // 3. Dernier recours : nom du fichier sans extension
    return Path.GetFileNameWithoutExtension(filePath);
}
```

**Initialisation au Démarrage - Background Scan :**

Pour lancer le premier scan après le démarrage complet de l'application, utiliser `IHostedService` :

```csharp
public class ComposeDiscoveryInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ComposeDiscoveryInitializer> _logger;

    public ComposeDiscoveryInitializer(
        IServiceProvider serviceProvider,
        ILogger<ComposeDiscoveryInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Lancer en background pour ne pas bloquer le démarrage
        _ = Task.Run(async () =>
        {
            try
            {
                // Créer un scope pour résoudre les services scoped
                using var scope = _serviceProvider.CreateScope();
                var scanner = scope.ServiceProvider.GetRequiredService<IComposeFileScanner>();

                _logger.LogInformation("Starting initial compose files scan...");

                var files = await scanner.ScanComposeFiles();

                _logger.LogInformation(
                    "Initial compose files scan completed. Found {Count} compose files.",
                    files.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during initial compose files scan");
            }
        }, cancellationToken);

        // Retourner immédiatement pour ne pas bloquer le démarrage
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

// Enregistrement dans Program.cs
builder.Services.AddHostedService<ComposeDiscoveryInitializer>();
```

**Avantages de cette approche :**
- ✅ L'application démarre immédiatement (pas de blocage)
- ✅ Le scan s'exécute en arrière-plan après le démarrage complet
- ✅ Si le scan échoue, l'application reste fonctionnelle
- ✅ Le cache sera pré-rempli pour les premiers appels API
- ✅ Log clair du nombre de fichiers découverts au démarrage

**Alternative avec délai minimal (optionnel) :**

Si on veut s'assurer que tous les services sont bien initialisés :

```csharp
public async Task StartAsync(CancellationToken cancellationToken)
{
    _ = Task.Run(async () =>
    {
        // Attendre un court instant pour laisser tous les services s'initialiser
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

        // Puis lancer le scan
        // ... (code de scan)
    }, cancellationToken);

    return Task.CompletedTask;
}
```

### 3. Association Projet Docker ↔ Fichier Compose

**Algorithme de matching :**

1. Récupérer la liste des projets actifs depuis Docker API
2. Pour chaque projet Docker, rechercher un fichier compose correspondant :
   - Comparer le nom du projet Docker avec le nom extrait de chaque fichier compose
   - Match exact = association trouvée
   - Stocker l'association en mémoire (cache)

3. Pour les fichiers compose sans projet Docker correspondant :
   - Les considérer comme des projets "disponibles mais non démarrés"
   - Les inclure dans la liste retournée à l'interface

**Résultat attendu :**

L'API `/api/compose/projects` retourne :

```json
[
  {
    "name": "my-application",
    "status": "running",
    "composeFile": "/app/compose-files/myapp/docker-compose.yml",
    "containers": [...],
    "hasComposeFile": true
  },
  {
    "name": "another-app",
    "status": "stopped",
    "composeFile": null,
    "containers": [...],
    "hasComposeFile": false,
    "warning": "No compose file found for this project"
  },
  {
    "name": "new-project",
    "status": "not-started",
    "composeFile": "/app/compose-files/new-project/compose.yml",
    "containers": [],
    "hasComposeFile": true
  }
]
```

### 4. Exécution des Commandes Compose

Lorsqu'une commande est demandée (up, down, restart, etc.) :

1. Vérifier si un fichier compose est associé au projet
2. Si oui : exécuter la commande avec ce fichier (`docker compose -f <file> <command>`)
3. Si non : retourner une erreur explicite

**Exemple d'implémentation :**

```csharp
public async Task<Result> ExecuteComposeCommand(string projectName, string command)
{
    // Récupérer le fichier compose associé
    var composeFile = await GetComposeFileForProject(projectName);

    if (composeFile == null)
    {
        return Result.Failure($"No compose file found for project '{projectName}'");
    }

    // Exécuter la commande
    var result = await _dockerService.ExecuteComposeCommand(
        composeFile.Path,
        command,
        projectName
    );

    return result;
}
```

### 5. Système de Cache

Pour éviter les ralentissements et le spam sur le système de fichiers :

**Cache en mémoire (MemoryCache) :**
- Durée de vie : **10 secondes**
- Clé : `"compose_file_discovery"`
- Contenu : Liste des fichiers découverts avec leur nom de projet extrait

**Invalidation du cache :**
- Automatique après 10 secondes
- Manuelle via endpoint `/api/compose/refresh` (admin uniquement)
- Sur modification détectée via FileSystemWatcher (optionnel, amélioration future)

**Implémentation avec Thread-Safety :**

```csharp
// Champ de classe pour gérer la concurrence
private readonly SemaphoreSlim _scanLock = new SemaphoreSlim(1, 1);

private async Task<List<DiscoveredComposeFile>> GetDiscoveredComposeFiles()
{
    var cacheKey = "compose_file_discovery";

    // Premier check sans lock pour performance
    if (_cache.TryGetValue(cacheKey, out List<DiscoveredComposeFile> cached))
    {
        return cached;
    }

    // Éviter les scans concurrents - un seul thread scanne à la fois
    await _scanLock.WaitAsync();
    try
    {
        // Double-check après acquisition du lock
        // (un autre thread a peut-être rempli le cache entre-temps)
        if (_cache.TryGetValue(cacheKey, out cached))
        {
            return cached;
        }

        var discovered = await ScanComposeFiles();

        _cache.Set(cacheKey, discovered, TimeSpan.FromSeconds(10));

        return discovered;
    }
    finally
    {
        _scanLock.Release();
    }
}
```

### 6. Modèle de Données

**Pas de stockage en base de données** - Tout en mémoire/cache

**Classe de configuration :**

```csharp
public class ComposeDiscoveryOptions
{
    public string RootPath { get; set; } = "/app/compose-files";
    public int ScanDepthLimit { get; set; } = 5;
    public int CacheDurationSeconds { get; set; } = 10;
    public int MaxFileSizeKB { get; set; } = 1024; // 1 MB par défaut
}
```

**Classe pour représenter un fichier découvert :**

```csharp
public class DiscoveredComposeFile
{
    public string FilePath { get; set; }
    public string ProjectName { get; set; }  // Extrait du fichier ou déduit
    public string DirectoryPath { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsValid { get; set; }  // Validation YAML
    public bool IsDisabled { get; set; }  // x-disabled: true dans le fichier
    public List<string> Services { get; set; }  // Liste des services définis
}
```

**Classe pour l'association :**

```csharp
public class ComposeProjectInfo
{
    public string Name { get; set; }
    public string Status { get; set; }  // running, stopped, not-started
    public string ComposeFile { get; set; }  // Peut être null
    public List<ContainerInfo> Containers { get; set; }
    public bool HasComposeFile { get; set; }
    public string Warning { get; set; }  // Si pas de fichier trouvé
}
```

## Flux de Données

### Scénario 1 : Listing des Projets

```
1. Frontend → GET /api/compose/projects
2. Backend → Vérifier cache (10s)
3. Backend → Scanner fichiers compose si cache expiré
4. Backend → Récupérer projets Docker actifs
5. Backend → Matcher projets ↔ fichiers
6. Backend → Retourner liste unifiée
7. Frontend → Afficher tous les projets (actifs + disponibles)
```

### Scénario 2 : Démarrage d'un Projet

```
1. Frontend → POST /api/compose/projects/{name}/up
2. Backend → Rechercher fichier compose pour {name}
3. Backend → Si trouvé : docker compose -f {file} up -d
4. Backend → Si non trouvé : erreur 404 avec message explicite
5. Backend → Retourner statut opération
```

### Scénario 3 : Arrêt d'un Projet

```
1. Frontend → POST /api/compose/projects/{name}/down
2. Backend → Rechercher fichier compose pour {name}
3. Backend → docker compose -f {file} down
4. Backend → Retourner statut
```

## Performance et Optimisation

### Optimisations Prévues

1. **Cache de 10 secondes** : Évite les scans répétés du système de fichiers
2. **Scan récursif optimisé** : Limite de profondeur pour éviter les scans excessifs
3. **Parsing YAML léger** : Ne parser que le strict nécessaire (attribut `name` et `services`)
4. **Lazy loading** : Scanner uniquement quand nécessaire (premier appel à l'API)
5. **Early termination** : Arrêter le scan si limite de profondeur atteinte

### Métriques à Surveiller

- Temps de scan des fichiers compose
- Nombre de fichiers découverts
- Taux de cache hit/miss
- Temps de réponse de l'API `/api/compose/projects`

**Objectif de performance :** < 100ms pour le scan avec cache, < 2s sans cache (avec ~50 fichiers)

### Observabilité et Logs Structurés

**Logs de scan avec métriques :**

```csharp
private async Task<List<DiscoveredComposeFile>> ScanComposeFiles()
{
    var stopwatch = Stopwatch.StartNew();
    int totalFiles = 0;
    int validFiles = 0;
    int invalidFiles = 0;
    int conflicts = 0;

    try
    {
        var allFiles = await ScanComposeFilesRecursive(_options.RootPath);
        totalFiles = allFiles.Count;

        var resolved = ResolveProjectNameConflicts(allFiles);
        validFiles = resolved.Count;
        invalidFiles = totalFiles - validFiles;
        conflicts = _conflictErrors.Count;

        stopwatch.Stop();

        // Log structuré avec toutes les métriques
        _logger.LogInformation(
            "Compose file scan completed in {Duration}ms. " +
            "Found {Total} files, {Valid} valid, {Invalid} invalid, {Conflicts} conflicts, " +
            "Scanned depth: {MaxDepth} levels",
            stopwatch.ElapsedMilliseconds,
            totalFiles,
            validFiles,
            invalidFiles,
            conflicts,
            _options.ScanDepthLimit);

        return resolved;
    }
    catch (Exception ex)
    {
        stopwatch.Stop();

        _logger.LogError(ex,
            "Compose file scan failed after {Duration}ms. " +
            "Partial results: {Total} files found before error",
            stopwatch.ElapsedMilliseconds,
            totalFiles);

        throw;
    }
}
```

**Métriques de cache :**

```csharp
private async Task<List<DiscoveredComposeFile>> GetDiscoveredComposeFiles()
{
    var cacheKey = "compose_file_discovery";

    if (_cache.TryGetValue(cacheKey, out List<DiscoveredComposeFile> cached))
    {
        _logger.LogDebug("Cache HIT for compose file discovery");
        return cached;
    }

    _logger.LogDebug("Cache MISS for compose file discovery - starting scan");

    await _scanLock.WaitAsync();
    try
    {
        if (_cache.TryGetValue(cacheKey, out cached))
        {
            _logger.LogDebug("Cache HIT after lock acquisition (another thread filled cache)");
            return cached;
        }

        var discovered = await ScanComposeFiles();
        _cache.Set(cacheKey, discovered, TimeSpan.FromSeconds(10));

        return discovered;
    }
    finally
    {
        _scanLock.Release();
    }
}
```

## Gestion des Cas Limites

### Cas 1 : Plusieurs Fichiers pour un Même Projet (Conflits de Noms)

**Prérequis utilisateur :** Si plusieurs fichiers compose ont le même nom de projet, l'utilisateur **doit** marquer explicitement les fichiers à ignorer.

**Label de désactivation : `x-disabled: true`**

**Algorithme de résolution des conflits :**

1. **Détection** : Lors du scan, grouper les fichiers par nom de projet
2. **Vérification** : Pour chaque groupe avec plusieurs fichiers (doublon détecté)
   - Compter combien de fichiers ont `x-disabled: true`
   - Compter combien de fichiers sont "actifs" (sans le label ou avec `x-disabled: false`)

3. **Règles de décision** :

   **Cas A - Un seul fichier actif** ✅
   - Nombre de fichiers actifs = 1
   - Action : Utiliser ce fichier, ignorer les autres
   - Log : Info "Project 'X' has multiple files, using active one: /path/to/file.yml"

   **Cas B - Tous les fichiers désactivés** ⚠️
   - Nombre de fichiers actifs = 0
   - Action : Ignorer tous les fichiers pour ce projet
   - Log : Warning "Project 'X' has multiple files but all are disabled"

   **Cas C - Plusieurs fichiers actifs** ❌ **ERREUR**
   - Nombre de fichiers actifs > 1
   - Action : **Rejeter tous les fichiers** pour ce projet
   - Log : **Error** "Project 'X' has multiple active files. Add 'x-disabled: true' to files you want to ignore:"
     - `/path/to/file1.yml`
     - `/path/to/file2.yml`
   - Le projet n'apparaîtra pas dans l'interface tant que le conflit n'est pas résolu

**Exemples :**

**✅ Exemple 1 : Conflit résolu correctement**

```yaml
# /app/compose-files/myapp/docker-compose.yml
name: my-application
x-disabled: true  # ← Fichier désactivé

services:
  web:
    image: nginx:old
```

```yaml
# /app/compose-files/myapp/production.yml
name: my-application  # ← Même nom de projet

services:
  web:
    image: nginx:latest
  db:
    image: postgres
```

**Résultat** : Le fichier `production.yml` est utilisé, `docker-compose.yml` est ignoré.

**✅ Exemple 2 : Plusieurs fichiers, plusieurs désactivés**

```yaml
# /app/compose-files/wordpress/dev.yml
name: wordpress
x-disabled: true

services:
  wordpress:
    image: wordpress:latest
```

```yaml
# /app/compose-files/wordpress/staging.yml
name: wordpress
x-disabled: true

services:
  wordpress:
    image: wordpress:6.0
```

```yaml
# /app/compose-files/wordpress/prod.yml
name: wordpress  # ← Seul fichier actif

services:
  wordpress:
    image: wordpress:6.4
  db:
    image: mariadb
```

**Résultat** : Le fichier `prod.yml` est utilisé, les deux autres sont ignorés.

**❌ Exemple 3 : Erreur - Conflit non résolu**

```yaml
# /app/compose-files/api/v1.yml
name: my-api

services:
  api:
    image: myapi:v1
```

```yaml
# /app/compose-files/api/v2.yml
name: my-api  # ← Même nom, pas de x-disabled

services:
  api:
    image: myapi:v2
```

**Résultat** :
- ❌ Erreur loggée : "Project 'my-api' has 2 active files. Add 'x-disabled: true' to files you want to ignore"
- Le projet `my-api` n'apparaît pas dans l'interface
- L'utilisateur doit ajouter `x-disabled: true` à l'un des fichiers

**Implémentation :**

```csharp
private List<DiscoveredComposeFile> ResolveProjectNameConflicts(List<DiscoveredComposeFile> allFiles)
{
    var resolvedFiles = new List<DiscoveredComposeFile>();
    var filesByProject = allFiles.GroupBy(f => f.ProjectName);

    foreach (var group in filesByProject)
    {
        var projectName = group.Key;
        var files = group.ToList();

        if (files.Count == 1)
        {
            // Pas de conflit
            resolvedFiles.Add(files[0]);
            continue;
        }

        // Conflit détecté : plusieurs fichiers pour le même projet
        // Ordre déterministe (alphabétique) pour cohérence entre les scans
        var activeFiles = files.Where(f => !f.IsDisabled).OrderBy(f => f.FilePath).ToList();
        var disabledFiles = files.Where(f => f.IsDisabled).OrderBy(f => f.FilePath).ToList();

        if (activeFiles.Count == 1)
        {
            // Cas A : Un seul fichier actif ✅
            _logger.LogInformation(
                "Project '{Project}' has {Total} files ({Active} active, {Disabled} disabled). Using active file: {File}",
                projectName, files.Count, activeFiles.Count, disabledFiles.Count, activeFiles[0].FilePath);

            resolvedFiles.Add(activeFiles[0]);
        }
        else if (activeFiles.Count == 0)
        {
            // Cas B : Tous désactivés ⚠️
            _logger.LogWarning(
                "Project '{Project}' has {Total} files but all are disabled. Project will not be available.",
                projectName, files.Count);

            // Ne rien ajouter à resolvedFiles
        }
        else
        {
            // Cas C : Plusieurs fichiers actifs ❌
            _logger.LogError(
                "Project '{Project}' has {Count} active files. Add 'x-disabled: true' to files you want to ignore: {Files}",
                projectName, activeFiles.Count, string.Join(", ", activeFiles.Select(f => f.FilePath)));

            // Ne rien ajouter à resolvedFiles
            // Optionnel : Stocker l'erreur pour affichage dans l'UI
            _conflictErrors.Add(new ConflictError
            {
                ProjectName = projectName,
                ConflictingFiles = activeFiles.Select(f => f.FilePath).ToList(),
                Message = $"Multiple active compose files found for project '{projectName}'. Mark unused files with 'x-disabled: true'."
            });
        }
    }

    return resolvedFiles;
}
```

**Modification de `ValidateAndParseComposeFile` :**

```csharp
private async Task<DiscoveredComposeFile?> ValidateAndParseComposeFile(string filePath)
{
    try
    {
        // ... code existant ...

        var composeContent = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

        // Vérifier si le fichier est désactivé
        bool isDisabled = false;
        if (composeContent.ContainsKey("x-disabled"))
        {
            var disabledValue = composeContent["x-disabled"];
            isDisabled = disabledValue is bool b && b;
        }

        // ... extraction nom de projet et services ...

        return new DiscoveredComposeFile
        {
            FilePath = filePath,
            ProjectName = projectName,
            DirectoryPath = Path.GetDirectoryName(filePath),
            LastModified = fileInfo.LastWriteTimeUtc,
            IsValid = true,
            IsDisabled = isDisabled,  // ← Nouveau champ
            Services = serviceNames
        };
    }
    catch (Exception ex)
    {
        // ... gestion erreurs ...
    }
}
```

**Mise à jour du modèle :**

```csharp
public class DiscoveredComposeFile
{
    public string FilePath { get; set; }
    public string ProjectName { get; set; }
    public string DirectoryPath { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsValid { get; set; }
    public bool IsDisabled { get; set; }  // ← Nouveau
    public List<string> Services { get; set; }
}

public class ConflictError
{
    public string ProjectName { get; set; }
    public List<string> ConflictingFiles { get; set; }
    public string Message { get; set; }
}
```

### Cas 2 : Projet Docker Sans Fichier Compose

Si un projet Docker est actif mais aucun fichier correspondant n'est trouvé :
- Afficher le projet avec `hasComposeFile: false`
- Afficher un warning dans l'interface
- **Distinction des commandes** :
  - ✅ **Autoriser** les commandes qui n'ont besoin que du nom du projet
  - ❌ **Bloquer** les commandes qui nécessitent le fichier compose

**Commandes fonctionnelles sans fichier compose :**

Ces commandes utilisent uniquement le flag `-p <project-name>` :

```bash
# Gestion du cycle de vie des conteneurs existants
docker compose -p myproject start      # ✅ OK
docker compose -p myproject stop       # ✅ OK
docker compose -p myproject restart    # ✅ OK
docker compose -p myproject pause      # ✅ OK
docker compose -p myproject unpause    # ✅ OK

# Consultation
docker compose -p myproject ps         # ✅ OK
docker compose -p myproject logs       # ✅ OK
docker compose -p myproject top        # ✅ OK

# Suppression (sans volumes)
docker compose -p myproject down       # ✅ OK (supprime conteneurs/réseaux)
docker compose -p myproject rm         # ✅ OK
```

**Commandes nécessitant le fichier compose :**

Ces commandes ont besoin du fichier pour créer/recréer des ressources :

```bash
# Création/Déploiement
docker compose -f file.yml up          # ❌ BLOQUÉ
docker compose -f file.yml create      # ❌ BLOQUÉ
docker compose -f file.yml run         # ❌ BLOQUÉ

# Build et images
docker compose -f file.yml build       # ❌ BLOQUÉ
docker compose -f file.yml pull        # ❌ BLOQUÉ
docker compose -f file.yml push        # ❌ BLOQUÉ

# Configuration
docker compose -f file.yml config      # ❌ BLOQUÉ

# Suppression avec volumes (nécessite de connaître les volumes définis)
docker compose -f file.yml down -v     # ❌ BLOQUÉ
```

**Implémentation :**

```csharp
public class ComposeCommandType
{
    public static readonly string[] RequiresFile = new[]
    {
        "up",
        "create",
        "run",
        "build",
        "pull",
        "push",
        "config",
        "convert"
    };

    public static readonly string[] WorksWithoutFile = new[]
    {
        "start",
        "stop",
        "restart",
        "pause",
        "unpause",
        "ps",
        "logs",
        "top",
        "down",  // Sans -v
        "rm",
        "kill"
    };

    public static bool RequiresComposeFile(string command)
    {
        return RequiresFile.Contains(command.ToLower());
    }
}
```

**Validation avant exécution :**

```csharp
public async Task<Result> ExecuteComposeCommand(string projectName, string command, string[]? args = null)
{
    var composeFile = await GetComposeFileForProject(projectName);

    // Vérifier si la commande nécessite le fichier
    if (ComposeCommandType.RequiresComposeFile(command))
    {
        if (composeFile == null)
        {
            return Result.Failure(
                $"Cannot execute '{command}' command: No compose file found for project '{projectName}'. " +
                $"This command requires a compose file to function."
            );
        }

        // Exécuter avec le fichier
        return await _dockerService.ExecuteComposeCommand(
            composeFile.Path,
            command,
            projectName,
            args
        );
    }
    else
    {
        // Exécuter avec seulement le nom du projet
        return await _dockerService.ExecuteComposeCommandByProjectName(
            projectName,
            command,
            args
        );
    }
}
```

**Exemple dans l'interface utilisateur :**

```json
// GET /api/compose/projects/myproject
{
  "name": "myproject",
  "status": "running",
  "composeFile": null,
  "hasComposeFile": false,
  "warning": "No compose file found for this project",
  "availableActions": {
    "start": true,
    "stop": true,
    "restart": true,
    "pause": true,
    "unpause": true,
    "logs": true,
    "ps": true,
    "down": true,
    "up": false,        // ❌ Désactivé (nécessite fichier)
    "build": false,     // ❌ Désactivé
    "recreate": false   // ❌ Désactivé
  },
  "containers": [
    {
      "id": "abc123",
      "name": "myproject-web-1",
      "status": "running"
    }
  ]
}
```

**Affichage dans l'UI :**

- Boutons **Start/Stop/Restart/Logs** : ✅ Actifs
- Boutons **Up/Build/Recreate** : ❌ Désactivés avec tooltip "Requires compose file"
- Warning badge : ⚠️ "No compose file - Limited actions available"

**Cas d'usage typique :**

Un utilisateur a démarré un projet avec `docker compose up` en ligne de commande, mais le fichier compose n'est pas dans `/app/compose-files` :
- ✅ Il peut **consulter les logs**, **arrêter/redémarrer** les conteneurs depuis l'interface
- ❌ Il ne peut pas **modifier et relancer** le projet (nécessite le fichier)
- 💡 Il peut **copier le fichier** dans `/app/compose-files` pour obtenir toutes les fonctionnalités

### Cas 3 : Fichier YAML Invalide ou Non-Compose

**Sous-cas 3.1 : YAML invalide (parsing échoue)**
- Logger en debug (pas une erreur)
- Exclure le fichier de la liste
- Ne pas interrompre le scan des autres fichiers

**Sous-cas 3.2 : YAML valide mais pas un compose file (pas de `services`)**
- Logger en debug : "File X is not a valid compose file (no 'services' key)"
- Exclure silencieusement du scan
- Permet d'avoir d'autres fichiers YAML dans le même dossier (config, documentation, etc.)

**Sous-cas 3.3 : Compose file vide (aucun service défini)**
- Logger en debug : "File X has no services defined"
- Exclure du scan
- Ne pas considérer comme une erreur

**Exemples :**

```yaml
# config.yml - Ignoré (pas de 'services')
app:
  name: myapp
  version: 1.0
```

```yaml
# empty-compose.yml - Ignoré (services vide)
services: {}
```

```yaml
# valid-compose.yml - Découvert ✓
services:
  web:
    image: nginx
```

### Cas 4 : Dossier Racine Inexistant ou Inaccessible

**Stratégie de gestion au démarrage :**

L'application peut fonctionner sans le dossier `/app/compose-files` en mode dégradé (lecture seule des projets Docker actifs). La gestion de ce cas ne doit pas empêcher le démarrage.

**Algorithme au démarrage de l'application :**

1. **Vérification de l'existence du dossier**
   ```csharp
   var composeFilesPath = _options.RootPath; // /app/compose-files

   if (!Directory.Exists(composeFilesPath))
   {
       _logger.LogWarning(
           "Compose files directory does not exist: {Path}. Attempting to create it...",
           composeFilesPath);
   ```

2. **Tentative de création automatique**
   ```csharp
       try
       {
           Directory.CreateDirectory(composeFilesPath);
           _logger.LogInformation(
               "Successfully created compose files directory: {Path}",
               composeFilesPath);

           // Vérifier les permissions en écriture
           var testFile = Path.Combine(composeFilesPath, ".write-test");
           File.WriteAllText(testFile, "test");
           File.Delete(testFile);

           _logger.LogInformation("Compose files directory is writable: {Path}", composeFilesPath);
       }
   ```

3. **Gestion de l'échec de création**
   ```csharp
       catch (UnauthorizedAccessException ex)
       {
           _logger.LogError(
               "Failed to create compose files directory: {Path}. Permission denied. " +
               "Application will run in degraded mode (read-only for existing Docker projects).",
               composeFilesPath);

           _isDegraded = true;
       }
       catch (Exception ex)
       {
           _logger.LogError(ex,
               "Failed to create compose files directory: {Path}. " +
               "Application will run in degraded mode (read-only for existing Docker projects).",
               composeFilesPath);

           _isDegraded = true;
       }
   }
   ```

4. **Vérification de l'accessibilité (dossier existe mais inaccessible)**
   ```csharp
   else
   {
       try
       {
           // Tester la lecture
           Directory.GetFiles(composeFilesPath);

           // Tester l'écriture
           var testFile = Path.Combine(composeFilesPath, ".write-test");
           File.WriteAllText(testFile, "test");
           File.Delete(testFile);

           _logger.LogInformation("Compose files directory is accessible: {Path}", composeFilesPath);
       }
       catch (UnauthorizedAccessException ex)
       {
           _logger.LogError(
               "Compose files directory exists but is not accessible: {Path}. Permission denied. " +
               "Application will run in degraded mode.",
               composeFilesPath);

           _isDegraded = true;
       }
       catch (Exception ex)
       {
           _logger.LogError(ex,
               "Compose files directory exists but cannot be accessed: {Path}. " +
               "Application will run in degraded mode.",
               composeFilesPath);

           _isDegraded = true;
       }
   }
   ```

**Niveaux de log :**

| Situation | Niveau | Message |
|-----------|--------|---------|
| Dossier n'existe pas | `Warning` | "Compose files directory does not exist: {Path}. Attempting to create it..." |
| Création réussie | `Information` | "Successfully created compose files directory: {Path}" |
| Création échouée | `Error` | "Failed to create compose files directory: {Path}. Application will run in degraded mode." |
| Dossier inaccessible | `Error` | "Compose files directory exists but is not accessible: {Path}. Application will run in degraded mode." |

**Important :**
- ❌ Pas de niveau `Critical` - L'application peut démarrer
- ✅ Niveaux `Warning` puis `Error` si échec
- ✅ L'application démarre toujours, même en mode dégradé

**Mode Dégradé :**

Lorsque `_isDegraded = true` :

```csharp
private async Task<List<DiscoveredComposeFile>> GetDiscoveredComposeFiles()
{
    if (_isDegraded)
    {
        _logger.LogDebug("Running in degraded mode - no compose file discovery available");
        return new List<DiscoveredComposeFile>(); // Liste vide
    }

    // ... scan normal ...
}
```

**Affichage dans l'interface :**

```json
// GET /api/system/status (nouveau endpoint ou existant)
{
  "composeDiscovery": {
    "status": "degraded",
    "rootPath": "/app/compose-files",
    "accessible": false,
    "message": "Compose files directory is not accessible. Only existing Docker projects can be managed (start/stop/restart). To enable full functionality, ensure the directory exists and has proper permissions, or mount the volume correctly.",
    "suggestions": [
      "Check Docker volume mounting: ./compose-files:/app/compose-files",
      "Verify directory permissions",
      "Restart the application after fixing the issue"
    ]
  }
}
```

**Banner dans l'UI :**

```
⚠️ Limited Functionality
The compose files directory (/app/compose-files) is not accessible.
You can only manage existing Docker projects (start/stop/restart).

To enable full functionality:
• Ensure the directory is mounted: ./compose-files:/app/compose-files
• Check directory permissions
• Restart the application

[Dismiss] [Learn More]
```

**Fonctionnalités disponibles en mode dégradé :**

| Fonctionnalité | Mode Normal | Mode Dégradé |
|----------------|-------------|--------------|
| Lister projets Docker actifs | ✅ | ✅ |
| Start/Stop/Restart projets existants | ✅ | ✅ |
| Consulter logs | ✅ | ✅ |
| Découvrir fichiers compose | ✅ | ❌ |
| Projets "not-started" | ✅ | ❌ |
| `docker compose up` nouveaux projets | ✅ | ❌ |
| Build/Recreate avec fichiers | ✅ | ❌ |

**Récupération automatique :**

Si le dossier devient accessible après le démarrage (volume monté après coup), le prochain appel API avec cache expiré détectera le changement :

```csharp
private async Task<List<DiscoveredComposeFile>> ScanComposeFiles()
{
    // Re-vérifier au cas où le problème est résolu
    if (!Directory.Exists(_options.RootPath))
    {
        if (_isDegraded)
            return new List<DiscoveredComposeFile>();

        _logger.LogWarning("Compose files directory still unavailable: {Path}", _options.RootPath);
        _isDegraded = true;
        return new List<DiscoveredComposeFile>();
    }

    // Le dossier est maintenant disponible
    if (_isDegraded)
    {
        _logger.LogInformation("Compose files directory is now available: {Path}. Exiting degraded mode.", _options.RootPath);
        _isDegraded = false;
    }

    // Continuer le scan normal
    return await ScanComposeFilesRecursive(_options.RootPath);
}
```

**Endpoint de diagnostic :**

```csharp
// GET /api/compose/health
[HttpGet("health")]
public async Task<IActionResult> GetComposeDiscoveryHealth()
{
    // Vérifier le dossier compose
    var rootPath = _options.RootPath;
    var exists = Directory.Exists(rootPath);
    var accessible = false;

    if (exists)
    {
        try
        {
            Directory.GetFiles(rootPath);
            accessible = true;
        }
        catch { }
    }

    // Vérifier Docker daemon
    bool dockerConnected = false;
    string dockerVersion = null;
    string dockerApiVersion = null;
    string dockerError = null;

    try
    {
        var version = await _dockerClient.System.GetVersionAsync();
        dockerConnected = true;
        dockerVersion = version.Version;
        dockerApiVersion = version.ApiVersion;
    }
    catch (Exception ex)
    {
        dockerError = ex.Message;
    }

    // Déterminer le statut global
    string overallStatus;
    if (!dockerConnected)
        overallStatus = "critical"; // Docker inaccessible = critique
    else if (!accessible)
        overallStatus = "degraded"; // Dossier inaccessible = dégradé
    else
        overallStatus = "healthy";  // Tout fonctionne

    return Ok(new
    {
        status = overallStatus,
        composeDiscovery = new
        {
            status = accessible ? "healthy" : "degraded",
            rootPath = rootPath,
            exists = exists,
            accessible = accessible,
            degradedMode = _isDegraded,
            message = accessible ? null : "Compose files directory is not accessible",
            impact = accessible ? null : "Only existing Docker projects can be managed"
        },
        dockerDaemon = new
        {
            status = dockerConnected ? "healthy" : "unhealthy",
            connected = dockerConnected,
            version = dockerVersion,
            apiVersion = dockerApiVersion,
            error = dockerError
        }
    });
}
```

## Sécurité

### Validations Requises

**1. Path Traversal - Uniquement pour les Endpoints API**

⚠️ **Important** : La validation du path traversal n'est **PAS nécessaire** pour le scan de fichiers car :
- Les chemins proviennent de `Directory.GetFiles()` qui ne peut retourner que des fichiers dans l'arborescence du dossier de départ
- Il est impossible d'obtenir un chemin en dehors de `/app/compose-files` via le scan récursif

✅ **La validation est OBLIGATOIRE** pour les endpoints API qui acceptent des chemins fournis par l'utilisateur :

```csharp
// À utiliser UNIQUEMENT dans les contrôleurs API, PAS dans le scanner
public bool IsValidComposeFilePath(string userProvidedPath)
{
    var rootPath = Path.GetFullPath(_options.RootPath); // /app/compose-files
    var fullPath = Path.GetFullPath(userProvidedPath);

    // Le fichier doit être dans le dossier racine
    if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
    {
        _logger.LogWarning("Path traversal attempt detected: {Path}", userProvidedPath);
        return false;
    }

    return true;
}
```

**Endpoints nécessitant cette validation :**
- `GET /api/compose/files/{*filePath}` - Lire un fichier compose spécifique
- `PUT /api/compose/files/{*filePath}` - Modifier un fichier compose
- `DELETE /api/compose/files/{*filePath}` - Supprimer un fichier compose
- Tout endpoint acceptant un chemin de fichier en paramètre

**Exemple d'utilisation dans un contrôleur :**

```csharp
[HttpGet("files/{*filePath}")]
public async Task<IActionResult> GetComposeFile(string filePath)
{
    // ⚠️ VALIDATION CRITIQUE - Ne jamais faire confiance à l'input utilisateur
    if (!_pathValidator.IsValidComposeFilePath(filePath))
    {
        return BadRequest("Invalid file path. Path must be within the compose files directory.");
    }

    // Maintenant on peut utiliser le chemin en toute sécurité
    var fullPath = Path.Combine(_options.RootPath, filePath);
    var content = await File.ReadAllTextAsync(fullPath);
    return Ok(content);
}
```

**Exemples d'attaques bloquées :**
```
GET /api/compose/files/../../../../etc/passwd          ❌ Bloqué
GET /api/compose/files/../../../sensitive-data.yml     ❌ Bloqué
DELETE /api/compose/files/../../important-file.yml     ❌ Bloqué
GET /api/compose/files/myapp/docker-compose.yml        ✅ Autorisé (relatif à /app/compose-files)
```

**2. Permissions**

Vérifier les permissions de lecture sur les fichiers :
```csharp
if (!File.Exists(filePath))
    return Result.Failure("File not found");

try
{
    using var fs = File.OpenRead(filePath);
    // OK, on peut lire
}
catch (UnauthorizedAccessException)
{
    return Result.Failure("Access denied");
}
```

**3. Taille des fichiers**

Limiter la taille max des fichiers compose à parser (configurable, défaut 1 MB) :

```csharp
var maxSizeBytes = _options.MaxFileSizeKB * 1024; // Config en KB, convert en bytes

if (fileInfo.Length > maxSizeBytes)
{
    _logger.LogWarning(
        "Compose file exceeds size limit: {Path} ({ActualKB} KB > {MaxKB} KB allowed)",
        filePath,
        fileInfo.Length / 1024,
        _options.MaxFileSizeKB);
    return null; // Exclure du scan
}
```

**Pourquoi 1 MB ?**
- Fichier compose typique (3-5 services) : 1-10 KB
- Gros projet (20-30 services) : 50-100 KB
- Projet complexe (50+ services) : 200-500 KB
- 1 MB couvre 99.9% des cas légitimes
- Protection contre fichiers anormaux/corrompus/malveillants

**Configuration personnalisée :**
Si un projet nécessite vraiment des fichiers plus gros (rare), augmenter dans `appsettings.json` :
```json
{
  "ComposeDiscovery": {
    "MaxFileSizeKB": 2048  // 2 MB si nécessaire
  }
}
```

**4. YAML Bombing**

Utiliser un parser sécurisé avec limite de profondeur et timeout :
```csharp
var deserializer = new DeserializerBuilder()
    .WithMaximumRecursion(10)  // Limite de profondeur
    .Build();

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var composeContent = await deserializer.Deserialize<ComposeFile>(yaml);
```

### Droits d'Accès

- **Lecture des fichiers** : Tous les utilisateurs authentifiés
- **Refresh du cache** : Admin uniquement
- **Exécution des commandes** : Selon les rôles existants (pas de changement)

## Migration et Compatibilité

### Impact sur l'Existant

**Changements majeurs (Breaking Changes) :**

1. **Suppression de la table ComposePaths**
   - Migration de base de données pour supprimer la table `ComposePaths`
   - Suppression des endpoints : `GET/POST/PUT/DELETE /api/compose/paths`
   - Suppression des contrôleurs et services associés

2. **Nouveau montage Docker requis**
   - Mise à jour du `docker-compose.yml` de l'application
   - Ajout du volume : `./compose-files:/app/compose-files`
   - **Action requise** : Les utilisateurs doivent déplacer leurs fichiers compose dans le nouveau dossier

3. **Configuration simplifiée**
   - Nouvelle section `ComposeDiscovery` dans `appsettings.json`
   - Suppression de la configuration `ComposePaths` (si elle existait)

**Modifications de l'API :**

- **Endpoint `/api/compose/projects`** : Enrichi avec nouveaux champs
  - `composeFile` : Chemin du fichier associé
  - `hasComposeFile` : Boolean indiquant si un fichier est trouvé
  - `warning` : Message si aucun fichier n'est trouvé
  - `services` : Liste des services définis dans le compose

- **Nouveaux endpoints** :
  - `GET /api/compose/files` : Liste tous les fichiers découverts
  - `POST /api/compose/refresh` : Rafraîchir le cache manuellement (admin)

**Frontend :**

- Adapter l'affichage pour montrer les projets "not-started"
- Supprimer l'interface de gestion des ComposePaths
- Afficher le chemin du fichier compose associé à chaque projet

### Migration pour les Utilisateurs

**Étape 1 : Sauvegarder les données**
```bash
# Sauvegarder la base de données actuelle
docker cp container_name:/app/data/app.db ./backup/app.db
```

**Étape 2 : Créer le nouveau dossier**
```bash
mkdir -p ./compose-files
```

**Étape 3 : Déplacer les fichiers compose**
```bash
# Si les utilisateurs avaient leurs fichiers ailleurs
# Exemple : copier depuis /data/docker vers ./compose-files
cp -r /data/docker/* ./compose-files/
```

**Étape 4 : Mettre à jour docker-compose.yml**
```yaml
services:
  backend:
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - ./data:/app/data
      - ./compose-files:/app/compose-files  # ← NOUVEAU
```

**Étape 5 : Redémarrer l'application**
```bash
docker compose down
docker compose up -d
```

### Rétrocompatibilité

**Ce qui continue de fonctionner :**
- Les projets Docker actifs sans fichier compose sont visibles (lecture seule)
- Les commandes échouent gracieusement avec message explicite
- La base de données existante (utilisateurs, sessions, audit logs) est préservée

**Ce qui ne fonctionne plus :**
- Gestion des ComposePaths via l'interface (fonctionnalité supprimée)
- Fichiers compose en dehors de `/app/compose-files` ne seront pas détectés

### Script de Migration de Base de Données

```csharp
// Migration EF Core
public partial class RemoveComposePaths : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Optionnel : Exporter les chemins avant suppression pour info
        migrationBuilder.Sql(@"
            SELECT 'INFO: Paths configured before migration:' AS message;
            SELECT Path FROM ComposePaths;
        ");

        // Supprimer la table
        migrationBuilder.DropTable(
            name: "ComposePaths");

        // Supprimer les audit logs associés (optionnel)
        migrationBuilder.Sql(@"
            DELETE FROM AuditLogs
            WHERE Action LIKE '%ComposePath%';
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Recréer la table si rollback nécessaire
        migrationBuilder.CreateTable(
            name: "ComposePaths",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Path = table.Column<string>(nullable: false),
                Description = table.Column<string>(nullable: true),
                IsActive = table.Column<bool>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ComposePaths", x => x.Id);
            });
    }
}
```

### Notes de Version (CHANGELOG)

```markdown
## [v0.21.0] - 2026-01-XX

### BREAKING CHANGES
- **Removed ComposePaths functionality**: All compose files must now be in `/app/compose-files`
- **Database migration required**: ComposePaths table will be dropped
- **Docker volume required**: Must mount `./compose-files:/app/compose-files`

### Added
- **Universal compose file discovery**: All `.yml` and `.yaml` files with valid `services` structure are automatically discovered
- Automatic discovery in `/app/compose-files` (recursive scan, max 5 levels depth)
- **Flexible file naming**: No naming convention required - any `.yml`/`.yaml` file with `services` is valid
- Structural validation: Only files with valid compose structure (presence of `services` key) are detected
- **File size limit**: Configurable max file size (default 1 MB) to prevent abuse
- **Background initialization**: Initial scan runs in background after application startup (non-blocking)
- **Label `x-disabled`**: Mark compose files with `x-disabled: true` to exclude them when multiple files share the same project name
- **Smart command routing**: Commands like `start`/`stop`/`restart` work without compose file (using `-p project-name`), while `up`/`build` require the file
- **Degraded mode**: Application runs in read-only mode if `/app/compose-files` is inaccessible
- New endpoint `GET /api/compose/files` to list all discovered compose files
- New endpoint `GET /api/compose/health` for compose discovery diagnostics
- New endpoint `GET /api/compose/conflicts` to list project name conflicts (optional)
- Cache system (10s TTL) for file discovery to improve performance
- Support for projects in "not-started" state (compose file exists but not running)
- Coexistence support: Non-compose YAML files (configs, etc.) are silently ignored

### Changed
- API `/api/compose/projects` now includes `composeFile`, `hasComposeFile`, and `services` fields
- Simplified path validation (single root directory)

### Removed
- Endpoints: `GET/POST/PUT/DELETE /api/compose/paths`
- Table: `ComposePaths`
- Configuration: `ComposePaths` section in appsettings.json

### Migration Guide
See COMPOSE_DISCOVERY_SPECS.md for detailed migration instructions.
```

## Décisions Prises

✅ **Récursivité** : Scanner récursivement avec limite de profondeur de **5 niveaux**

✅ **Dossier unique** : Tous les fichiers dans `/app/compose-files` (pas de liste de chemins)

✅ **Scan universel** : Tous les fichiers `.yml`/`.yaml` avec validation structurelle (présence de `services`)

✅ **Suppression de ComposePaths** : Table et endpoints supprimés (breaking change assumé)

✅ **Cache configurable** : Durée dans `appsettings.json` (`ComposeDiscovery:CacheDurationSeconds`)

✅ **Conflits de noms** : Label `x-disabled: true` pour marquer les fichiers à ignorer (résolu - Cas 1)

✅ **Projets sans fichier** : Distinction des commandes - certaines fonctionnent avec `-p project-name` uniquement (résolu - Cas 2)

✅ **Fichiers invalides** : Ignorés silencieusement avec log debug (résolu - Cas 3)

✅ **Dossier racine inexistant** : Warning + tentative création + mode dégradé si échec - pas de blocage de l'app (résolu - Cas 4)

✅ **Fichiers override** : Ignorés dans le MVP (Phase 2 : support multi-fichiers)

✅ **Rafraîchissement temps réel** : Cache uniquement (TTL 10s), pas de FileSystemWatcher dans le MVP (Phase 2)

✅ **Multi-fichiers environnement** : Un seul fichier principal par projet, pas de gestion des variantes dev/prod/staging dans le MVP

✅ **Initialisation** : Premier scan en arrière-plan après démarrage complet de l'application (pas de lazy loading, pas de timer fixe)

## Questions Ouvertes

Aucune question ouverte restante - Toutes les décisions ont été prises pour le MVP.

## Étapes d'Implémentation Suggérées

### Phase 1 : MVP (Minimum Viable Product)

**1. Préparation - Suppression de ComposePaths**
- [ ] Créer migration EF Core pour supprimer table `ComposePaths`
- [ ] Supprimer `ComposePathController` et `ComposePathService`
- [ ] Supprimer les routes `/api/compose/paths`
- [ ] Retirer l'interface frontend de gestion des paths

**2. Configuration et Initialisation**
- [ ] Ajouter section `ComposeDiscovery` dans `appsettings.json`
- [ ] Créer classe `ComposeDiscoveryOptions` pour les options :
  - [ ] `RootPath` (string)
  - [ ] `ScanDepthLimit` (int, défaut 5)
  - [ ] `CacheDurationSeconds` (int, défaut 10)
  - [ ] `MaxFileSizeKB` (int, défaut 1024 = 1 MB)
- [ ] Implémenter validation des options au démarrage
- [ ] **Gestion du dossier racine (Cas 4)** :
  - [ ] Vérifier existence de `/app/compose-files`
  - [ ] Si inexistant : Logger Warning + tenter création
  - [ ] Si création échoue : Logger Error + activer mode dégradé (`_isDegraded = true`)
  - [ ] Si dossier existe : Tester lecture/écriture
  - [ ] Si inaccessible : Logger Error + activer mode dégradé
  - [ ] Ne jamais bloquer le démarrage de l'application
- [ ] **Premier scan initial en arrière-plan** :
  - [ ] Créer classe `ComposeDiscoveryInitializer : IHostedService`
  - [ ] Dans `StartAsync()`, lancer le scan en arrière-plan avec `Task.Run()`
  - [ ] Retourner immédiatement `Task.CompletedTask` pour ne pas bloquer le démarrage
  - [ ] Logger "Starting initial compose files scan..."
  - [ ] Logger résultat : "Initial compose files scan completed. Found {Count} compose files."
  - [ ] Gérer les erreurs sans crasher l'application
  - [ ] Enregistrer dans `Program.cs` : `builder.Services.AddHostedService<ComposeDiscoveryInitializer>()`

**3. Scanner de Fichiers**
- [ ] Créer `ComposeFileScanner` service
- [ ] Implémenter scan récursif avec limite de profondeur (5 niveaux)
- [ ] Scanner tous les fichiers `*.yml` et `*.yaml`
- [ ] Valider taille des fichiers (max configurable, défaut 1 MB)
  - [ ] Utiliser `_options.MaxFileSizeKB` pour la limite
  - [ ] Logger en KB (plus lisible que bytes)
  - [ ] Log warning : "Compose file exceeds size limit: {Path} ({ActualKB} KB > {MaxKB} KB allowed)"
- [ ] Parser YAML et valider structure (présence de `services`)
- [ ] Note : Pas de validation path traversal dans le scan (redondant)

**4. Extraction Nom de Projet et Gestion Conflits**
- [ ] Parser YAML pour extraire attribut `name`
- [ ] Parser YAML pour extraire attribut `x-disabled`
- [ ] Fallback sur nom du répertoire parent
- [ ] Fallback final sur nom du fichier
- [ ] Implémenter `ResolveProjectNameConflicts()` :
  - [ ] Grouper fichiers par nom de projet
  - [ ] Compter fichiers actifs vs désactivés
  - [ ] Cas A : 1 fichier actif → Utiliser
  - [ ] Cas B : Tous désactivés → Ignorer
  - [ ] Cas C : Plusieurs actifs → Erreur loggée
- [ ] Stocker les erreurs de conflit pour affichage UI (optionnel)

**5. Cache**
- [ ] Implémenter cache avec `IMemoryCache`
- [ ] Durée configurable (défaut 10s)
- [ ] Clé : `"compose_file_discovery"`

**6. Matching Projets ↔ Fichiers**
- [ ] Récupérer projets Docker actifs via API
- [ ] Matcher par nom de projet
- [ ] Créer `ComposeProjectInfo` avec tous les champs
- [ ] Inclure projets "not-started" (fichier sans projet Docker)

**7. API**
- [ ] Enrichir endpoint `GET /api/compose/projects` avec nouveaux champs
- [ ] Créer endpoint `GET /api/compose/files` (liste fichiers découverts)
- [ ] Créer endpoint `POST /api/compose/refresh` (admin uniquement)
- [ ] Créer endpoint `GET /api/compose/conflicts` (liste erreurs de conflit - optionnel)
- [ ] Créer endpoint `GET /api/compose/health` (diagnostic du dossier racine + statut Docker daemon)
- [ ] **Endpoints de gestion de fichiers (si existants ou à créer)** :
  - [ ] `GET /api/compose/files/{*filePath}` - Lire un fichier
  - [ ] `PUT /api/compose/files/{*filePath}` - Modifier un fichier
  - [ ] `DELETE /api/compose/files/{*filePath}` - Supprimer un fichier
  - [ ] ⚠️ **VALIDATION PATH TRAVERSAL OBLIGATOIRE** sur tous ces endpoints
- [ ] Créer service `PathValidator` avec méthode `IsValidComposeFilePath()`
- [ ] Mettre à jour DTOs : `ComposeProjectDto`, `DiscoveredComposeFileDto`, `ConflictError`
- [ ] **Implémentation de l'endpoint refresh :**
  ```csharp
  [HttpPost("refresh")]
  [Authorize(Roles = "Admin")]
  public async Task<IActionResult> RefreshComposeFiles()
  {
      _cache.Remove("compose_file_discovery");
      var files = await _scanner.ScanComposeFiles();

      return Ok(new {
          success = true,
          message = $"Cache refreshed. Found {files.Count} compose files.",
          filesDiscovered = files.Count,
          timestamp = DateTime.UtcNow
      });
  }
  ```

**8. Exécution Commandes**
- [ ] Créer classe `ComposeCommandType` avec listes de commandes
- [ ] **Distinction des commandes (Cas 2)** :
  - [ ] Liste `RequiresFile` : up, create, run, build, pull, push, config
  - [ ] Liste `WorksWithoutFile` : start, stop, restart, pause, logs, ps, down, rm
- [ ] Validation avant exécution :
  - [ ] Si commande nécessite fichier + pas de fichier → Erreur explicite
  - [ ] Si commande ne nécessite pas fichier → Exécution avec `-p project-name`
  - [ ] Si commande nécessite fichier + fichier disponible → Exécution avec `-f file.yml`
- [ ] Enrichir réponse API avec `availableActions` (quelles actions sont possibles)

**9. Tests**
- [ ] Tests unitaires : extraction nom projet, matching, cache
- [ ] Tests d'intégration : scan de fichiers réels
- [ ] Tests de performance : scan de 100+ fichiers
- [ ] Tester commandes up/down avec fichiers découverts

**10. Frontend**
- [ ] Afficher badge "not-started" pour projets disponibles
- [ ] Afficher chemin du fichier compose associé
- [ ] Bouton "Start" pour projets not-started
- [ ] **Projets sans fichier (Cas 2)** :
  - [ ] Warning badge si projet sans fichier compose
  - [ ] Désactiver boutons Up/Build/Recreate avec tooltip explicatif
  - [ ] Garder actifs Start/Stop/Restart/Logs
  - [ ] Utiliser champ `availableActions` de l'API
- [ ] **Mode dégradé (Cas 4)** :
  - [ ] Appeler `/api/compose/health` au chargement
  - [ ] Banner warning si status = "degraded"
  - [ ] Afficher message + suggestions de résolution
  - [ ] Bouton "Retry" pour re-vérifier
- [ ] **Affichage des erreurs de conflit (Cas 1)** (optionnel MVP) :
  - [ ] Appeler `GET /api/compose/conflicts`
  - [ ] Banner/alerte dans l'UI si conflits détectés
  - [ ] Liste des fichiers en conflit avec instructions de résolution
  - [ ] Bouton "Refresh" pour re-scanner après correction

**11. Documentation**
- [ ] Mettre à jour CLAUDE.md
- [ ] Ajouter guide de migration dans README
- [ ] Ajouter section sur `x-disabled` dans README (voir annexe)
- [ ] Mettre à jour CHANGELOG.md
- [ ] **Documentation Swagger/OpenAPI** :
  - [ ] Mettre à jour annotations Swagger pour nouveaux endpoints
  - [ ] Ajouter exemples de réponses dans Swagger (response examples)
  - [ ] Marquer anciens endpoints ComposePaths comme `[Obsolete]` avec message
  - [ ] Documenter nouveaux DTOs avec XML comments
  - [ ] Ajouter schéma pour `ComposeProjectInfo`, `DiscoveredComposeFile`, `ConflictError`

### Phase 2 : Améliorations (Futures)

**1. Support Multi-Fichiers**
- [ ] **Fichiers override** : Détecter `docker-compose.override.yml`
- [ ] Passer automatiquement `-f base.yml -f override.yml`
- [ ] **Fichiers environnement** : Support pour `compose.dev.yml`, `compose.prod.yml`
- [ ] UI pour sélectionner quel fichier/environnement utiliser
- [ ] Configuration de l'environnement actif par projet

**2. Rafraîchissement Temps Réel**
- [ ] Implémenter `FileSystemWatcher` sur `/app/compose-files`
- [ ] Invalider cache sur événement modification/ajout/suppression
- [ ] WebSocket notification au frontend en temps réel
- [ ] Gestion des événements multiples (debouncing)
- [ ] Auto-refresh de l'UI quand fichiers changent

**3. Métriques et Monitoring**
- [ ] Temps de scan des fichiers
- [ ] Nombre de fichiers découverts
- [ ] Taux cache hit/miss
- [ ] Endpoint `/api/metrics/compose-discovery`
- [ ] Dashboard de monitoring dans l'UI

**4. Interface Améliorée**
- [ ] Vue "Compose Files" dédiée avec liste des fichiers
- [ ] Upload de fichiers compose via UI (drag & drop)
- [ ] Éditeur inline pour fichiers découverts
- [ ] Validation YAML en temps réel avec erreurs
- [ ] Prévisualisation des services avant `compose up`

### Phase 3 : Optimisations

**1. Performance**
- [ ] Scan parallèle avec `Parallel.ForEach`
- [ ] Parsing YAML incrémental (stream reader)
- [ ] Index des fichiers en mémoire pour recherche rapide

**2. Multi-Environment**
- [ ] Support `compose.prod.yml`, `compose.dev.yml`
- [ ] Sélecteur d'environnement dans l'UI
- [ ] Profils de déploiement configurables

**3. Robustesse**
- [ ] Retry logic pour opérations I/O
- [ ] Health check dédié pour `/app/compose-files`
- [ ] Alertes si dossier devient inaccessible
- [ ] Mode dégradé si scan échoue

## Tests à Prévoir

### Tests Unitaires
- **Validation structurelle** : Fichiers avec/sans clé `services`
- **Extraction du nom de projet** : Attribut `name`, nom de répertoire, nom de fichier
- **Label x-disabled** : Extraction valeur true/false/absent
- **Résolution des conflits (Cas 1)** :
  - 1 fichier actif parmi plusieurs → OK
  - Tous désactivés → Ignorer
  - Plusieurs actifs → Erreur
  - Pas de conflit (1 seul fichier) → OK
- **Distinction des commandes (Cas 2)** :
  - Commande nécessitant fichier + fichier disponible → OK
  - Commande nécessitant fichier + pas de fichier → Erreur
  - Commande sans fichier + projet existant → OK avec `-p`
  - Vérification listes `RequiresFile` et `WorksWithoutFile`
- **Mode dégradé (Cas 4)** :
  - Dossier inexistant → Warning + tentative création
  - Création réussie → Mode normal
  - Création échouée → Error + mode dégradé activé
  - Dossier inaccessible → Error + mode dégradé
  - Récupération automatique quand dossier disponible
- **Algorithme de matching** : Projets Docker ↔ fichiers compose
- **Gestion du cache** : Hit/miss, expiration, invalidation
- **Validation path traversal** : Tester `PathValidator.IsValidComposeFilePath()` avec chemins malveillants
- **Validation taille fichier** :
  - Fichiers > 1 MB exclus du scan (défaut)
  - Vérifier log warning avec taille en KB
  - Tester avec configuration personnalisée (ex: 2 MB)
- **Parsing YAML** : Fichiers valides/invalides, YAML bombing
- **Découverte récursive** : Profondeur, arrêt à la limite

### Tests d'Intégration
- **Scan de fichiers réels** : Mélange de compose files et autres YAML
- **Différents noms** : `docker-compose.yml`, `myapp.yaml`, `stack.yml`, etc.
- **Fichiers ignorés** : YAML sans `services`, fichiers non-YAML
- **Gestion des conflits réels (Cas 1)** :
  - Créer 2 fichiers avec même `name`, 1 avec `x-disabled: true` → 1 découvert
  - Créer 2 fichiers avec même `name`, aucun `x-disabled` → Erreur loggée
  - Créer 3 fichiers avec même `name`, 2 avec `x-disabled: true` → 1 découvert
- **Exécution de commandes (Cas 2)** :
  - Projet avec fichier : Tester `up`, `down`, `start`, `stop` → Toutes OK
  - Projet sans fichier : Tester `start`, `stop`, `restart` → OK avec `-p`
  - Projet sans fichier : Tester `up`, `build` → Erreur explicite
  - Vérifier champ `availableActions` dans réponse API
- **Mode dégradé (Cas 4)** :
  - Démarrer app sans dossier `/app/compose-files` → Warning + création
  - Bloquer permissions du dossier → Error + mode dégradé
  - Vérifier endpoint `/api/compose/health` retourne "degraded"
  - Rétablir permissions → Vérifier récupération automatique
  - Mode dégradé : Lister projets actifs fonctionne, pas de découverte
- **Sécurité - Path Traversal API** :
  - Tenter `GET /api/compose/files/../../../../etc/passwd` → 400 Bad Request
  - Tenter `DELETE /api/compose/files/../../../file.yml` → 400 Bad Request
  - `GET /api/compose/files/myapp/compose.yml` → 200 OK
  - Vérifier log warning pour tentatives de path traversal
- **Invalidation du cache** : Ajout/modification/suppression de fichiers
- **Cas limites** : Fichiers invalides, permissions, répertoires inaccessibles
- **Profondeur récursive** : Fichiers à différents niveaux (1-5)
- **Coexistence** : Compose files + configs + autres YAML dans même dossier

### Tests de Performance
- Scan de 100+ fichiers
- Cache hit/miss ratio
- Temps de réponse API

## Annexes

### Exemple de Fichier Compose avec `name`

```yaml
name: my-custom-project-name

services:
  web:
    image: nginx:latest
    ports:
      - "80:80"

  db:
    image: postgres:15
    environment:
      POSTGRES_PASSWORD: secret
```

### Exemples de Validation Structurelle

**✅ Fichier découvert - Compose file valide :**
```yaml
# /app/compose-files/myapp/stack.yml
services:
  api:
    image: myapi:latest
  redis:
    image: redis:alpine
```

**✅ Fichier découvert - Avec attribut `name` :**
```yaml
# /app/compose-files/production.yaml
name: prod-environment

services:
  web:
    image: nginx
```

**✗ Fichier ignoré - Pas de clé `services` :**
```yaml
# /app/compose-files/config.yml
app:
  name: myapp
  version: 1.0
database:
  host: localhost
```

**✗ Fichier ignoré - Services vide :**
```yaml
# /app/compose-files/empty.yml
version: "3.8"
services: {}
```

**✗ Fichier ignoré - YAML invalide :**
```yaml
# /app/compose-files/broken.yml
services:
  web:
    image: nginx
  db:
    image: postgres
    ports:
      - invalid syntax here
```

### Scénarios de Test Recommandés

**Test 1 : Découverte Multi-Noms**
```
/app/compose-files/
├── docker-compose.yml    # Nom standard
├── myapp.yml             # Nom personnalisé
├── prod-stack.yaml       # Nom avec tiret
└── app_v2.yml            # Nom avec underscore

Résultat attendu : 4 fichiers découverts
```

**Test 2 : Coexistence Compose + Config**
```
/app/compose-files/project/
├── docker-compose.yml    # ✓ Compose (découvert)
├── config.yaml           # ✗ Config app (ignoré)
├── secrets.yml           # ✗ Secrets (ignoré)
└── .env                  # ✗ Environnement (ignoré)

Résultat attendu : 1 fichier découvert
```

**Test 3 : Profondeur Récursive**
```
/app/compose-files/
├── level1.yml            # Niveau 1 ✓
└── a/
    └── b/
        └── c/
            └── d/
                └── e/
                    └── level5.yml    # Niveau 5 ✓
                    └── f/
                        └── level6.yml    # Niveau 6 ✗ (trop profond)

Résultat attendu : 2 fichiers découverts (level1.yml, level5.yml)
```

### Documentation README - Section à Ajouter

**Section pour le README.md :**

```markdown
## Gestion des Fichiers Compose

### Découverte Automatique

L'application découvre automatiquement tous les fichiers compose (`.yml` et `.yaml`) dans le dossier `/app/compose-files`. Les fichiers n'ont pas besoin de suivre une convention de nommage spécifique - tant qu'ils contiennent une section `services` valide, ils seront détectés.

**Exemples de noms valides :**
- `docker-compose.yml` (standard)
- `production.yaml` (environnement)
- `myapp.yml` (personnalisé)
- `stack-v2.yml` (avec version)

### Montage du Dossier Compose

Le dossier `/app/compose-files` doit être monté lors du démarrage :

```yaml
# docker-compose.yml
services:
  backend:
    volumes:
      - ./compose-files:/app/compose-files  # ← Requis
      - /var/run/docker.sock:/var/run/docker.sock
```

### Gestion des Conflits de Noms de Projet

Si plusieurs fichiers compose ont le **même nom de projet** (attribut `name` ou nom de répertoire identique), vous devez désactiver les fichiers que vous ne souhaitez pas utiliser.

**Utilisation du label `x-disabled` :**

Ajoutez `x-disabled: true` au niveau racine du fichier compose que vous voulez désactiver :

```yaml
# dev.yml - Fichier désactivé
name: myapp
x-disabled: true  # ← Ce fichier sera ignoré

services:
  web:
    image: nginx:latest
```

```yaml
# production.yml - Fichier actif
name: myapp  # ← Même nom de projet

services:
  web:
    image: nginx:stable
  db:
    image: postgres:15
```

**Règles :**
- ✅ **Un seul fichier actif** : Le système utilise ce fichier automatiquement
- ⚠️ **Tous désactivés** : Le projet n'apparaît pas dans l'interface
- ❌ **Plusieurs fichiers actifs** : Erreur - vous devez ajouter `x-disabled: true` à tous les fichiers sauf un

**Exemple d'erreur dans les logs :**

```
[ERROR] Project 'myapp' has 2 active files. Add 'x-disabled: true' to files you want to ignore:
  - /app/compose-files/myapp/dev.yml
  - /app/compose-files/myapp/prod.yml
```

**Solution :** Ajoutez `x-disabled: true` à l'un des deux fichiers.

### Convention Docker Compose

Le préfixe `x-` est une [extension Docker Compose standard](https://docs.docker.com/compose/compose-file/#extension) qui permet d'ajouter des champs personnalisés sans affecter le comportement de Docker Compose. Vos fichiers restent 100% compatibles avec `docker compose` en ligne de commande.
```

---

**Section pour CLAUDE.md :**

```markdown
## Compose File Discovery

### Label x-disabled

When multiple compose files have the same project name (via `name:` attribute or directory name), use the `x-disabled: true` label to mark which files should be ignored:

```yaml
# docker-compose.dev.yml
name: myproject
x-disabled: true  # This file will be ignored during discovery

services:
  app:
    image: myapp:dev
```

**Conflict Resolution Rules:**
- If exactly 1 file is active (no `x-disabled` or `x-disabled: false`) → Use that file
- If all files are disabled (`x-disabled: true` on all) → Project is hidden
- If multiple files are active → **Error logged**, project is excluded until resolved

**Implementation:** See `ResolveProjectNameConflicts()` in `ComposeFileScanner` service.
```

### Exemple de Réponse API Enrichie

**GET /api/compose/projects :**

```json
{
  "success": true,
  "data": [
    {
      "name": "wordpress-site",
      "status": "running",
      "composeFile": "/app/compose-files/wordpress/docker-compose.yml",
      "hasComposeFile": true,
      "services": ["wordpress", "mysql"],
      "containers": [
        {
          "id": "abc123",
          "name": "wordpress-site-wordpress-1",
          "status": "running",
          "image": "wordpress:latest"
        }
      ]
    },
    {
      "name": "nextcloud",
      "status": "not-started",
      "composeFile": "/app/compose-files/nextcloud/compose.yml",
      "hasComposeFile": true,
      "services": ["nextcloud", "postgres", "redis"],
      "containers": []
    }
  ]
}
```

**GET /api/compose/conflicts (optionnel - Cas 1) :**

```json
{
  "success": true,
  "data": [
    {
      "projectName": "my-api",
      "conflictingFiles": [
        "/app/compose-files/api/v1.yml",
        "/app/compose-files/api/v2.yml"
      ],
      "message": "Multiple active compose files found for project 'my-api'. Mark unused files with 'x-disabled: true'.",
      "resolutionSteps": [
        "Open one of the conflicting files",
        "Add 'x-disabled: true' at the root level",
        "Refresh the compose files list"
      ]
    },
    {
      "projectName": "wordpress",
      "conflictingFiles": [
        "/app/compose-files/wordpress/dev.yml",
        "/app/compose-files/wordpress/staging.yml",
        "/app/compose-files/wordpress/prod.yml"
      ],
      "message": "Multiple active compose files found for project 'wordpress'. Mark unused files with 'x-disabled: true'.",
      "resolutionSteps": [
        "Open dev.yml and staging.yml",
        "Add 'x-disabled: true' to both files",
        "Keep prod.yml without the x-disabled label",
        "Refresh the compose files list"
      ]
    }
  ],
  "hasConflicts": true
}
```

**GET /api/compose/health (diagnostic - Cas 4) :**

```json
// Mode normal
{
  "success": true,
  "data": {
    "status": "healthy",
    "composeDiscovery": {
      "status": "healthy",
      "rootPath": "/app/compose-files",
      "exists": true,
      "accessible": true,
      "degradedMode": false
    },
    "dockerDaemon": {
      "status": "healthy",
      "connected": true,
      "version": "24.0.7",
      "apiVersion": "1.43"
    }
  }
}

// Mode dégradé (dossier inaccessible)
{
  "success": true,
  "data": {
    "status": "degraded",
    "composeDiscovery": {
      "status": "degraded",
      "rootPath": "/app/compose-files",
      "exists": false,
      "accessible": false,
      "degradedMode": true,
      "message": "Compose files directory is not accessible. Application is running in degraded mode.",
      "impact": "Only existing Docker projects can be managed. Compose file discovery is disabled."
    },
    "dockerDaemon": {
      "status": "healthy",
      "connected": true,
      "version": "24.0.7",
      "apiVersion": "1.43"
    }
  }
}

// Mode critique (Docker daemon inaccessible)
{
  "success": true,
  "data": {
    "status": "critical",
    "composeDiscovery": {
      "status": "healthy",
      "rootPath": "/app/compose-files",
      "exists": true,
      "accessible": true,
      "degradedMode": false
    },
    "dockerDaemon": {
      "status": "unhealthy",
      "connected": false,
      "error": "Cannot connect to Docker daemon. Is Docker running?"
    }
  }
}
```

**GET /api/compose/projects/{name} (avec availableActions - Cas 2) :**

```json
// Projet AVEC fichier compose
{
  "success": true,
  "data": {
    "name": "myapp",
    "status": "running",
    "composeFile": "/app/compose-files/myapp/docker-compose.yml",
    "hasComposeFile": true,
    "services": ["web", "db"],
    "availableActions": {
      "start": true,
      "stop": true,
      "restart": true,
      "pause": true,
      "unpause": true,
      "logs": true,
      "ps": true,
      "down": true,
      "up": true,
      "build": true,
      "recreate": true,
      "pull": true
    },
    "containers": [...]
  }
}

// Projet SANS fichier compose
{
  "success": true,
  "data": {
    "name": "external-project",
    "status": "running",
    "composeFile": null,
    "hasComposeFile": false,
    "warning": "No compose file found for this project",
    "availableActions": {
      "start": true,       // ✅ Fonctionne avec -p
      "stop": true,        // ✅ Fonctionne avec -p
      "restart": true,     // ✅ Fonctionne avec -p
      "pause": true,       // ✅ Fonctionne avec -p
      "unpause": true,     // ✅ Fonctionne avec -p
      "logs": true,        // ✅ Fonctionne avec -p
      "ps": true,          // ✅ Fonctionne avec -p
      "down": true,        // ✅ Fonctionne avec -p
      "up": false,         // ❌ Nécessite fichier
      "build": false,      // ❌ Nécessite fichier
      "recreate": false,   // ❌ Nécessite fichier
      "pull": false        // ❌ Nécessite fichier
    },
    "containers": [...]
  }
}
```
