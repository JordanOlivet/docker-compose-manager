# Refonte du Système de Découverte des Projets Compose

## Problématique Actuelle

Le système actuel de découverte des projets Compose repose sur une approche **hybride complexe** :

### Architecture Actuelle (voir `ComposeService.cs`)

- **Base de données** : Tables `ComposePaths` et `ComposeFiles` qui stockent les chemins des fichiers compose
- **Découverte hybride** :
  - Priorité 1 : Scan du système de fichiers via chemins configurés dans `ComposePaths`
  - Priorité 2 : Extraction des directories depuis `docker compose ls -a --format json` (ligne 435-592)
- **Gestion** : Mapping entre chemins de fichiers et IDs numériques pour contourner les problèmes d'URL
- **Édition** : Fonctionnalité d'édition de fichiers compose via Monaco Editor (actuellement peu robuste)
- **Templates** : Bibliothèque de templates de projets Compose (LAMP, MEAN, etc.)

### Limitations de l'Approche Actuelle

1. **Incompatibilité Cross-Platform Majeure** :
   - Les chemins de fichiers Windows (`C:\path\to\compose`) ne correspondent pas aux chemins Linux montés dans le container (`/app/data/compose`)
   - `docker compose ls` retourne des chemins de l'hôte qui peuvent être inaccessibles depuis le container backend
   - Problème récurrent lors du développement (Windows host + Linux container)

2. **Complexité de Mapping** :
   - Nécessite une synchronisation constante entre filesystem et base de données
   - Deux sources de vérité qui peuvent diverger

3. **Sécurité : Path Traversal** :
   - Nécessite une validation complexe des chemins
   - Surface d'attaque importante

4. **Divergence d'État Possible** :
   - Le système de fichiers et l'état Docker peuvent diverger
   - Fichier supprimé mais projet toujours actif (ou inversement)

5. **Configuration Obligatoire** :
   - Nécessite de configurer manuellement les `ComposePaths`
   - Impossible de gérer automatiquement des projets créés en dehors de ces chemins

## Nouvelle Approche : Docker-Only Sans Persistance

### Principe Fondamental

**Source de vérité unique : `docker compose ls --all` - Sans base de données**

Au lieu de persister l'état des projets en base de données, on utilise **exclusivement** la commande `docker compose ls --all --format json` comme source de données, avec un cache mémoire pour la performance.

### Pourquoi Sans Base de Données ?

**Avantages** :
- ✅ **Simplicité maximale** : Pas de synchronisation, pas de divergence possible
- ✅ **Cohérence totale** : Docker est LA source, toujours à jour
- ✅ **Moins de code** : Pas de migrations, pas de background service de sync, pas de DbContext pour les projets
- ✅ **Stateless** : Plus facile à scaler horizontalement
- ✅ **Fiabilité** : Impossible d'avoir un état incohérent entre DB et Docker
- ✅ **Aligné avec la philosophie** : Docker est la source de vérité, pas besoin de dupliquer

**Inconvénient unique** :
- Performance : Appeler `docker compose ls` à chaque fois (~100-200ms)
  - **Solution** : Cache en mémoire (10 secondes) → problème résolu ✅

### Sortie de `docker compose ls --all --format json`

```json
[
  {
    "Name": "myapp",
    "Status": "running(3)",
    "ConfigFiles": "/app/docker-compose.yml"
  },
  {
    "Name": "database",
    "Status": "exited(2)",
    "ConfigFiles": "/app/db/docker-compose.yml,/app/db/docker-compose.prod.yml"
  },
  {
    "Name": "old-project",
    "Status": "exited(0)",
    "ConfigFiles": "/projects/old/docker-compose.yml"
  }
]
```

**Informations disponibles** :
- `Name` : Nom du projet Compose (utilisé pour les permissions et les URLs)
- `Status` : État du projet avec nombre de containers (ex: "running(3)" = 3 containers actifs)
- `ConfigFiles` : Chemin(s) des fichiers compose (séparés par `,` ou `;`)

### Avantages Critiques

| Approche | Projets avec containers | Projets sans containers (après `down`) | Accès fichiers requis |
|----------|------------------------|---------------------------------------|----------------------|
| Labels Docker (API) | ✅ Découvert | ❌ Non découvert | ❌ Non |
| `docker compose ls --all` | ✅ Découvert | ✅ Découvert | ❌ Non |

**Point crucial** : Les opérations (up, down, restart, ps) utilisent le flag `-p projectName`, donc **aucun accès aux fichiers n'est requis**. Docker maintient son propre registre des projets avec leurs configurations d'origine.

**Exemple** :
```bash
# Créer et arrêter un projet
docker compose -f /app/test/docker-compose.yml up -d
docker compose -f /app/test/docker-compose.yml down

# Les containers sont supprimés, mais Docker garde la trace du projet
docker compose ls --all
# Retourne toujours le projet "test" avec Status: "exited(0)"

# On peut toujours gérer le projet par son nom, même sans accès au fichier
docker compose -p test up -d        # ✅ Fonctionne
docker compose -p test restart      # ✅ Fonctionne
docker compose -p test ps           # ✅ Fonctionne
```

## Architecture Simplifiée

### Flux de Données

```
┌─────────────┐
│  Frontend   │
└──────┬──────┘
       │ GET /api/compose/projects
       ▼
┌─────────────────────────────────────────┐
│  ComposeProjectsController              │
│  - Récupère userId du token JWT        │
└──────┬──────────────────────────────────┘
       │
       ▼
┌─────────────────────────────────────────┐
│  ComposeDiscoveryService                │
│  - GetProjectsForUserAsync()            │
│  - Cache mémoire (10 secondes)          │
└──────┬──────────────────────────────────┘
       │
       ├──────────────────────────────────┐
       ▼                                  ▼
┌────────────────┐              ┌──────────────────┐
│ docker compose │              │ PermissionService│
│ ls --all       │              │ FilterAuthorized │
└────────────────┘              └──────────────────┘
                                         │
                                         ▼
                                 ┌──────────────────┐
                                 │ ResourcePermission│
                                 │ (table DB)       │
                                 └──────────────────┘
```

### Modèle de Données : DTO Uniquement

**Pas de table `ComposeProjects` en DB !** On utilise uniquement un DTO pour l'API.

```csharp
// DTOs/ComposeProjectDto.cs
public record ComposeProjectDto
{
    // Identifiant du projet (nom Docker)
    public string Name { get; init; }

    // Informations découvertes depuis docker compose ls
    public string RawStatus { get; init; }  // Ex: "running(3)", "exited(2)"
    public string[] ConfigFiles { get; init; }  // Informatif uniquement (pour affichage)

    // État parsé
    public ProjectStatus Status { get; init; }
    public int ContainerCount { get; init; }

    // Permissions de l'utilisateur actuel sur ce projet
    // MUTABLE pour permettre l'enrichissement après création
    public PermissionFlags UserPermissions { get; set; }

    // Propriétés calculées pour l'UI
    public bool CanStart => Status is ProjectStatus.Stopped or ProjectStatus.Removed;
    public bool CanStop => Status == ProjectStatus.Running;
    public string StatusColor => Status switch
    {
        ProjectStatus.Running => "green",
        ProjectStatus.Stopped => "orange",
        ProjectStatus.Removed => "gray",
        _ => "red"
    };
}

public enum ProjectStatus
{
    Running,    // Status contient "running"
    Stopped,    // Status contient "exited" avec count > 0
    Removed,    // Status contient "exited(0)" - projet down sans containers
    Unknown     // Status non parsable
}
```

### Classe Utilitaire : DockerCommandExecutor

Pour centraliser l'exécution des commandes Docker Compose :

```csharp
// Services/Utils/DockerCommandExecutor.cs
public class DockerCommandExecutor
{
    private readonly ILogger<DockerCommandExecutor> _logger;
    private bool? _isComposeV2;

    public DockerCommandExecutor(ILogger<DockerCommandExecutor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Détecte si docker compose v2 est disponible
    /// </summary>
    public async Task<bool> IsComposeV2Available()
    {
        if (_isComposeV2.HasValue)
            return _isComposeV2.Value;

        try
        {
            // Try docker compose version (v2)
            ProcessStartInfo psi = new()
            {
                FileName = "docker",
                Arguments = "compose version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    _isComposeV2 = true;
                    _logger.LogInformation("Docker Compose v2 détecté");
                    return true;
                }
            }

            // Fall back to docker-compose (v1)
            psi.FileName = "docker-compose";
            psi.Arguments = "version";

            using Process? processV1 = Process.Start(psi);
            if (processV1 != null)
            {
                await processV1.WaitForExitAsync();
                if (processV1.ExitCode == 0)
                {
                    _isComposeV2 = false;
                    _logger.LogInformation("Docker Compose v1 détecté");
                    return false;
                }
            }

            throw new InvalidOperationException("Docker Compose non trouvé");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la détection de la version Docker Compose");
            throw;
        }
    }

    /// <summary>
    /// Exécute une commande docker compose
    /// </summary>
    public async Task<(int ExitCode, string Output, string Error)> ExecuteComposeCommandAsync(
        string workingDirectory,
        string arguments,
        string? composeFile = null,
        CancellationToken cancellationToken = default)
    {
        bool isV2 = await IsComposeV2Available();

        // Add -f option if compose file is specified
        string fileArg = "";
        if (!string.IsNullOrEmpty(composeFile))
        {
            fileArg = $"-f \"{Path.GetFileName(composeFile)}\" ";
        }

        ProcessStartInfo psi = new()
        {
            FileName = isV2 ? "docker" : "docker-compose",
            Arguments = isV2 ? $"compose {fileArg}{arguments}" : $"{fileArg}{arguments}",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        StringBuilder output = new();
        StringBuilder error = new();

        using Process? process = Process.Start(psi);
        if (process == null)
        {
            return (-1, "", "Failed to start process");
        }

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null) output.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null) error.AppendLine(e.Data);
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        string outputStr = output.ToString();
        string errorStr = error.ToString();

        _logger.LogDebug(
            "Commande Compose exécutée: {Command}, Exit Code: {ExitCode}",
            arguments,
            process.ExitCode
        );

        return (process.ExitCode, outputStr, errorStr);
    }
}
```

### Service de Découverte

```csharp
public interface IComposeDiscoveryService
{
    /// <summary>
    /// Récupère tous les projets Compose accessibles à l'utilisateur
    /// </summary>
    Task<List<ComposeProjectDto>> GetProjectsForUserAsync(int userId, bool bypassCache = false);

    /// <summary>
    /// Récupère un projet spécifique par son nom
    /// </summary>
    Task<ComposeProjectDto?> GetProjectByNameAsync(string projectName, int userId);

    /// <summary>
    /// Force le rafraîchissement du cache
    /// </summary>
    void InvalidateCache();
}

public class ComposeDiscoveryService : IComposeDiscoveryService
{
    private readonly IMemoryCache _cache;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<ComposeDiscoveryService> _logger;

    private const string CACHE_KEY = "docker_compose_projects_all";
    private const int CACHE_SECONDS = 10;

    public async Task<List<ComposeProjectDto>> GetProjectsForUserAsync(int userId, bool bypassCache = false)
    {
        // 1. Récupérer tous les projets (avec cache)
        var allProjects = await GetAllProjectsAsync(bypassCache);

        // 2. Vérifier si l'utilisateur est admin (accès complet)
        bool isAdmin = await _permissionService.IsAdminAsync(userId);
        if (isAdmin)
        {
            return allProjects;
        }

        // 3. Filtrer par permissions
        var projectNames = allProjects.Select(p => p.Name).ToList();
        var authorizedNames = await _permissionService.FilterAuthorizedResourcesAsync(
            userId,
            ResourceType.ComposeProject,
            projectNames
        );

        var authorizedNamesSet = authorizedNames.ToHashSet();
        return allProjects.Where(p => authorizedNamesSet.Contains(p.Name)).ToList();
    }

    public async Task<ComposeProjectDto?> GetProjectByNameAsync(string projectName, int userId)
    {
        var projects = await GetProjectsForUserAsync(userId);
        return projects.FirstOrDefault(p => p.Name == projectName);
    }

    public void InvalidateCache()
    {
        _cache.Remove(CACHE_KEY);
        _logger.LogInformation("Cache des projets Compose invalidé");
    }

    private async Task<List<ComposeProjectDto>> GetAllProjectsAsync(bool bypassCache)
    {
        if (bypassCache)
        {
            _cache.Remove(CACHE_KEY);
        }

        return await _cache.GetOrCreateAsync(CACHE_KEY, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CACHE_SECONDS);
            _logger.LogDebug("Récupération des projets depuis Docker (cache miss)");
            return await FetchProjectsFromDockerAsync();
        }) ?? new List<ComposeProjectDto>();
    }

    private async Task<List<ComposeProjectDto>> FetchProjectsFromDockerAsync()
    {
        var projects = new List<ComposeProjectDto>();

        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = "docker",
                Arguments = "compose ls --all --format json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                _logger.LogWarning("Impossible de démarrer docker compose ls");
                return projects;
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("docker compose ls a échoué: {Error}", error);
                return projects;
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                _logger.LogDebug("Aucun projet Compose découvert");
                return projects;
            }

            using JsonDocument doc = JsonDocument.Parse(output);
            JsonElement root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Format JSON inattendu de docker compose ls");
                return projects;
            }

            foreach (JsonElement element in root.EnumerateArray())
            {
                try
                {
                    string name = element.GetProperty("Name").GetString() ?? "unknown";
                    string rawStatus = element.GetProperty("Status").GetString() ?? "unknown";
                    string configFilesStr = element.GetProperty("ConfigFiles").GetString() ?? "";

                    string[] configFiles = configFilesStr
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(f => f.Trim())
                        .ToArray();

                    (ProjectStatus status, int containerCount) = ParseStatus(rawStatus);

                    projects.Add(new ComposeProjectDto
                    {
                        Name = name,
                        RawStatus = rawStatus,
                        ConfigFiles = configFiles,  // Informatif uniquement
                        Status = status,
                        ContainerCount = containerCount,
                        UserPermissions = PermissionFlags.None
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erreur lors du parsing d'un projet");
                }
            }

            _logger.LogInformation("Découvert {Count} projets Compose", projects.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la découverte des projets Compose");
        }

        return projects;
    }

    private (ProjectStatus Status, int ContainerCount) ParseStatus(string rawStatus)
    {
        try
        {
            var match = Regex.Match(rawStatus, @"^(\w+)\((\d+)\)$");
            if (match.Success)
            {
                string state = match.Groups[1].Value.ToLowerInvariant();
                int count = int.Parse(match.Groups[2].Value);

                return state switch
                {
                    "running" => (ProjectStatus.Running, count),
                    "exited" when count > 0 => (ProjectStatus.Stopped, count),
                    "exited" when count == 0 => (ProjectStatus.Removed, count),
                    _ => (ProjectStatus.Unknown, count)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de parser le status: {Status}", rawStatus);
        }

        return (ProjectStatus.Unknown, 0);
    }
}
```

### Service d'Opérations

```csharp
public interface IComposeOperationService
{
    Task<OperationResult> UpAsync(string projectName, bool build = false);
    Task<OperationResult> DownAsync(string projectName, bool removeVolumes = false);
    Task<OperationResult> RestartAsync(string projectName);
}

public class ComposeOperationService : IComposeOperationService
{
    private readonly IComposeDiscoveryService _discoveryService;
    private readonly DockerCommandExecutor _dockerExecutor;
    private readonly ILogger<ComposeOperationService> _logger;

    public async Task<OperationResult> UpAsync(string projectName, bool build = false)
    {
        try
        {
            // Récupérer le projet depuis la découverte (pas de userId ici car appelé après vérif permissions)
            // Pour obtenir les infos sans filtre de permissions, on pourrait ajouter une méthode dans le service
            // Ou passer directement par docker compose avec -p projectName

            var buildArg = build ? "--build" : "";
            var arguments = $"-p {projectName} up -d {buildArg}".Trim();

            // Exécuter sans spécifier les fichiers, docker compose utilisera son contexte
            var (exitCode, output, error) = await _dockerExecutor.ExecuteComposeCommandAsync(
                workingDirectory: "/tmp", // Peu importe car on utilise -p
                arguments: arguments
            );

            return new OperationResult
            {
                Success = exitCode == 0,
                Message = exitCode == 0 ? $"Projet '{projectName}' démarré" : $"Erreur lors du démarrage",
                Output = output,
                Error = error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du démarrage du projet {ProjectName}", projectName);
            return new OperationResult
            {
                Success = false,
                Message = "Erreur inattendue",
                Error = ex.Message
            };
        }
    }

    public async Task<OperationResult> DownAsync(string projectName, bool removeVolumes = false)
    {
        try
        {
            var volumesArg = removeVolumes ? "--volumes" : "";
            var arguments = $"-p {projectName} down {volumesArg}".Trim();

            var (exitCode, output, error) = await _dockerExecutor.ExecuteComposeCommandAsync(
                workingDirectory: "/tmp",
                arguments: arguments
            );

            return new OperationResult
            {
                Success = exitCode == 0,
                Message = exitCode == 0 ? $"Projet '{projectName}' arrêté" : $"Erreur lors de l'arrêt",
                Output = output,
                Error = error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'arrêt du projet {ProjectName}", projectName);
            return new OperationResult
            {
                Success = false,
                Message = "Erreur inattendue",
                Error = ex.Message
            };
        }
    }

    public async Task<OperationResult> RestartAsync(string projectName)
    {
        try
        {
            var arguments = $"-p {projectName} restart";

            var (exitCode, output, error) = await _dockerExecutor.ExecuteComposeCommandAsync(
                workingDirectory: "/tmp",
                arguments: arguments
            );

            return new OperationResult
            {
                Success = exitCode == 0,
                Message = exitCode == 0 ? $"Projet '{projectName}' redémarré" : $"Erreur lors du redémarrage",
                Output = output,
                Error = error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du redémarrage du projet {ProjectName}", projectName);
            return new OperationResult
            {
                Success = false,
                Message = "Erreur inattendue",
                Error = ex.Message
            };
        }
    }
}

public class OperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
}
```

### Contrôleur d'API

```csharp
[ApiController]
[Route("api/compose/projects")]
[Authorize]
public class ComposeProjectsController : ControllerBase
{
    private readonly IComposeDiscoveryService _discoveryService;
    private readonly IPermissionService _permissionService;
    private readonly IComposeOperationService _operationService;
    private readonly IHubContext<OperationsHub> _hubContext;

    // Liste tous les projets Compose accessibles à l'utilisateur
    [HttpGet]
    public async Task<ActionResult<List<ComposeProjectDto>>> GetProjects([FromQuery] bool refresh = false)
    {
        int userId = GetCurrentUserId();
        var projects = await _discoveryService.GetProjectsForUserAsync(userId, bypassCache: refresh);

        // Enrichir avec les permissions spécifiques de l'utilisateur
        foreach (var project in projects)
        {
            project.UserPermissions = await _permissionService.GetUserPermissionsAsync(
                userId,
                ResourceType.ComposeProject,
                project.Name
            );
        }

        return Ok(projects);
    }

    // Détails d'un projet spécifique
    [HttpGet("{projectName}")]
    public async Task<ActionResult<ComposeProjectDto>> GetProject(string projectName)
    {
        projectName = Uri.UnescapeDataString(projectName);
        int userId = GetCurrentUserId();

        var project = await _discoveryService.GetProjectByNameAsync(projectName, userId);

        if (project == null)
            return NotFound($"Projet '{projectName}' introuvable ou accès refusé");

        project.UserPermissions = await _permissionService.GetUserPermissionsAsync(
            userId,
            ResourceType.ComposeProject,
            project.Name
        );

        return Ok(project);
    }

    // Liste des services d'un projet (via docker compose ps)
    // PAS DE CACHE - Toujours temps réel
    [HttpGet("{projectName}/services")]
    public async Task<ActionResult<List<ComposeServiceDto>>> GetProjectServices(string projectName)
    {
        projectName = Uri.UnescapeDataString(projectName);
        int userId = GetCurrentUserId();

        // Vérifier les permissions
        bool hasPermission = await _permissionService.HasPermissionAsync(
            userId,
            ResourceType.ComposeProject,
            projectName,
            PermissionFlags.View
        );

        if (!hasPermission)
            return Forbid();

        try
        {
            // Utiliser docker compose ps pour lister les services
            var executor = new DockerCommandExecutor(_logger);
            var (exitCode, output, error) = await executor.ExecuteComposeCommandAsync(
                "/tmp",
                $"-p {projectName} ps --format json"
            );

            if (exitCode != 0)
                return BadRequest($"Erreur lors de la récupération des services: {error}");

            // Parser le JSON et retourner les services
            // (Code de parsing similaire à l'existant)
            var services = ParseServicesJson(output);
            return Ok(services);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des services du projet {ProjectName}", projectName);
            return StatusCode(500, "Erreur inattendue");
        }
    }

    // Démarrer un projet
    [HttpPost("{projectName}/up")]
    public async Task<ActionResult> StartProject(string projectName, [FromQuery] bool build = false)
    {
        projectName = Uri.UnescapeDataString(projectName);
        int userId = GetCurrentUserId();

        bool hasPermission = await _permissionService.HasPermissionAsync(
            userId,
            ResourceType.ComposeProject,
            projectName,
            PermissionFlags.Start
        );

        if (!hasPermission)
            return Forbid();

        var result = await _operationService.UpAsync(projectName, build);

        // Invalider le cache
        _discoveryService.InvalidateCache();

        // Notifier les clients via SignalR (système existant)
        await _hubContext.Clients.All.SendAsync("ComposeProjectStateChanged", new
        {
            projectName,
            action = "started",
            timestamp = DateTime.UtcNow
        });

        return result.Success ? Ok(result) : BadRequest(result);
    }

    // Arrêter un projet
    [HttpPost("{projectName}/down")]
    public async Task<ActionResult> StopProject(string projectName, [FromQuery] bool removeVolumes = false)
    {
        projectName = Uri.UnescapeDataString(projectName);
        int userId = GetCurrentUserId();

        bool hasPermission = await _permissionService.HasPermissionAsync(
            userId,
            ResourceType.ComposeProject,
            projectName,
            PermissionFlags.Stop
        );

        if (!hasPermission)
            return Forbid();

        var result = await _operationService.DownAsync(projectName, removeVolumes);

        _discoveryService.InvalidateCache();

        await _hubContext.Clients.All.SendAsync("ComposeProjectStateChanged", new
        {
            projectName,
            action = "stopped",
            timestamp = DateTime.UtcNow
        });

        return result.Success ? Ok(result) : BadRequest(result);
    }

    // Redémarrer un projet
    [HttpPost("{projectName}/restart")]
    public async Task<ActionResult> RestartProject(string projectName)
    {
        projectName = Uri.UnescapeDataString(projectName);
        int userId = GetCurrentUserId();

        bool hasPermission = await _permissionService.HasPermissionAsync(
            userId,
            ResourceType.ComposeProject,
            projectName,
            PermissionFlags.Restart
        );

        if (!hasPermission)
            return Forbid();

        var result = await _operationService.RestartAsync(projectName);

        _discoveryService.InvalidateCache();

        await _hubContext.Clients.All.SendAsync("ComposeProjectStateChanged", new
        {
            projectName,
            action = "restarted",
            timestamp = DateTime.UtcNow
        });

        return result.Success ? Ok(result) : BadRequest(result);
    }

    // Forcer un refresh du cache
    [HttpPost("refresh")]
    public IActionResult RefreshCache()
    {
        _discoveryService.InvalidateCache();
        return Ok(new { message = "Cache rafraîchi" });
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdClaim ?? "0");
    }

    private List<ComposeServiceDto> ParseServicesJson(string json)
    {
        // Implémentation du parsing (similaire au code existant)
        // À adapter selon le format de sortie
        return new List<ComposeServiceDto>();
    }
}
```

## Configuration des Services (Program.cs)

Ajouter la configuration des nouveaux services dans `Program.cs` :

```csharp
// Cache mémoire
builder.Services.AddMemoryCache();

// Services Docker
builder.Services.AddSingleton<DockerCommandExecutor>();
builder.Services.AddScoped<IComposeDiscoveryService, ComposeDiscoveryService>();
builder.Services.AddScoped<IComposeOperationService, ComposeOperationService>();

// Les services existants restent inchangés
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IUserService, UserService>();
// ...

// SignalR (déjà configuré)
builder.Services.AddSignalR();

// Middleware de gestion d'erreurs (déjà existant)
app.UseMiddleware<ErrorHandlingMiddleware>();

// Hubs SignalR (déjà configurés)
app.MapHub<OperationsHub>("/hubs/operations");
app.MapHub<LogsHub>("/hubs/logs");
```

## Gestion des Permissions

### Architecture Permissions (Inchangée)

Le système de permissions **reste identique** et **fonctionne parfaitement** avec l'approche sans DB :

```csharp
// Modèle existant (gardé en DB)
public class ResourcePermission
{
    public int Id { get; set; }
    public ResourceType ResourceType { get; set; }  // ComposeProject
    public string ResourceName { get; set; }        // Nom du projet (ex: "myapp")
    public int? UserId { get; set; }
    public int? UserGroupId { get; set; }
    public PermissionFlags Permissions { get; set; }
}
```

**Point clé** : Les permissions utilisent le **nom du projet** (`ResourceName`), pas un ID !

### Workflow Permissions

1. **Utilisateur demande la liste des projets** :
   ```
   GET /api/compose/projects
   ```

2. **Backend** :
   ```
   a) Récupérer tous les projets depuis docker compose ls (cache 10s)
      → ["myapp", "database", "webapp"]

   b) Filtrer par permissions via PermissionService.FilterAuthorizedResourcesAsync()
      → Query SQL: SELECT ResourceName FROM ResourcePermission
        WHERE ResourceType = ComposeProject
        AND ResourceName IN ("myapp", "database", "webapp")
        AND (UserId = X OR UserGroupId IN (groupes de l'user))
      → Retourne ["myapp", "webapp"]

   c) Retourner uniquement les projets autorisés
   ```

3. **Join en mémoire** : Projets (Docker) + Permissions (DB) → Projets autorisés

### Avantages

- ✅ **Séparation des préoccupations** : État (Docker) vs Permissions (DB)
- ✅ **Pas de synchronisation** : Les permissions utilisent le nom (stable)
- ✅ **Robustesse** : Si un projet disparaît de Docker, les permissions restent
- ✅ **Performance** : 1 seule query SQL avec `IN`

## Gestion de l'Édition et des Templates

### Fonctionnalités Désactivées Temporairement

Les fonctionnalités suivantes sont **temporairement désactivées** pour les raisons indiquées :

#### 1. Édition de Fichiers Compose

**Raisons** :
- Problèmes cross-platform non résolus (mapping chemins hôte/container)
- Incompatible avec l'approche "Docker comme source de vérité"
- Fonctionnalité peu robuste (pas de validation YAML, pas de backup)

**Comment désactiver** :

```csharp
// Backend - Endpoints d'édition
[HttpGet("files/{id}/content")]
[Obsolete("Édition désactivée - voir COMPOSE_DISCOVERY_REFACTOR.md")]
public IActionResult GetFileContent(int id)
{
    return StatusCode(501, new
    {
        success = false,
        message = "La fonctionnalité d'édition est temporairement désactivée.",
        reason = "Problèmes cross-platform. Utilisez un éditeur externe.",
        documentation = "Voir COMPOSE_DISCOVERY_REFACTOR.md"
    });
}

[HttpPut("files/{id}/content")]
[Obsolete("Édition désactivée - voir COMPOSE_DISCOVERY_REFACTOR.md")]
public IActionResult UpdateFileContent(int id, [FromBody] string content)
{
    return StatusCode(501, new
    {
        success = false,
        message = "La fonctionnalité d'édition est temporairement désactivée."
    });
}

// Conserver FileService.cs avec commentaire
/// <summary>
/// ⚠️ ATTENTION: Service actuellement DÉSACTIVÉ dans l'API.
/// Raison: Problèmes cross-platform (Windows host + Linux container).
/// Pour réactiver: Voir COMPOSE_DISCOVERY_REFACTOR.md
/// </summary>
public class FileService { /* ... code conservé ... */ }
```

```typescript
// Frontend - Feature flag
export const FEATURES = {
  COMPOSE_FILE_EDITING: false,
};

// Dans l'UI
{!FEATURES.COMPOSE_FILE_EDITING && (
  <Tooltip content="Fonctionnalité temporairement indisponible">
    <Button disabled>Éditer</Button>
  </Tooltip>
)}
```

#### 2. Templates de Projets Compose

**Raisons** :
- Dépend de l'édition de fichiers (actuellement désactivée)
- Nécessite de créer des fichiers sur le filesystem (incompatible avec approche Docker-only)

**Comment désactiver** :

```csharp
// Backend - Endpoints de templates
[HttpGet("templates")]
[Obsolete("Templates désactivés - voir COMPOSE_DISCOVERY_REFACTOR.md")]
public IActionResult GetTemplates()
{
    return StatusCode(501, new
    {
        success = false,
        message = "Les templates sont temporairement désactivés.",
        reason = "Dépend de la fonctionnalité d'édition de fichiers (actuellement désactivée).",
        documentation = "Voir COMPOSE_DISCOVERY_REFACTOR.md"
    });
}

[HttpPost("projects/from-template")]
[Obsolete("Templates désactivés - voir COMPOSE_DISCOVERY_REFACTOR.md")]
public IActionResult CreateFromTemplate([FromBody] CreateFromTemplateRequest request)
{
    return StatusCode(501, new
    {
        success = false,
        message = "Les templates sont temporairement désactivés."
    });
}

// Conserver le dossier Resources/Templates/ et le code associé
/// <summary>
/// ⚠️ ATTENTION: Service de templates actuellement DÉSACTIVÉ.
/// Raison: Nécessite l'édition de fichiers (actuellement désactivée).
/// Les fichiers de templates sont conservés dans Resources/Templates/
/// Pour réactiver: Voir COMPOSE_DISCOVERY_REFACTOR.md
/// </summary>
public class TemplateService { /* ... code conservé ... */ }
```

```typescript
// Frontend - Masquer l'UI de création depuis template
{!FEATURES.COMPOSE_FILE_EDITING && (
  <Alert variant="info">
    <AlertTitle>Création de projets depuis templates</AlertTitle>
    <AlertDescription>
      Fonctionnalité temporairement indisponible.
      Créez vos fichiers docker-compose.yml manuellement sur l'hôte,
      puis démarrez-les avec `docker compose up`.
      Ils apparaîtront automatiquement dans l'interface.
    </AlertDescription>
  </Alert>
)}
```

### Plan de Réactivation

Ces fonctionnalités pourront être réactivées après :

1. **Résolution du mapping de chemins** : Configuration explicite des volumes montés
2. **Amélioration de la robustesse** : Validation YAML, backup, gestion des conflits
3. **Tests cross-platform** : Validation Windows/Linux

## Logs des Projets

**Les logs sont déjà gérés via SignalR** (système existant).

- **Hub** : `LogsHub` (déjà existant dans `src/Hubs/LogsHub.cs`)
- **Endpoint WebSocket** : `/hubs/logs`
- **Pas besoin d'endpoint HTTP** : Le streaming temps réel via SignalR est plus adapté

**Le système actuel reste inchangé** pour les logs.

## Gestion des Erreurs

### Middleware Global Existant

Le système dispose déjà d'un **middleware de gestion d'erreurs global** :

```csharp
// src/Middleware/ErrorHandlingMiddleware.cs (EXISTANT)
public class ErrorHandlingMiddleware
{
    // Capture toutes les exceptions non catchées
    // Retourne des ApiResponse standardisés
    // Gère ValidationException, UnauthorizedAccessException, etc.
}
```

**Configuration dans Program.cs** (déjà fait) :
```csharp
app.UseMiddleware<ErrorHandlingMiddleware>();
```

### Stratégie Recommandée

**Combiner try/catch locaux + middleware global** :

- **Try/catch dans les méthodes** : Pour les erreurs attendues, avec contexte spécifique
- **Middleware global** : Filet de sécurité pour les erreurs inattendues

**Exemple** :

```csharp
[HttpPost("{projectName}/up")]
public async Task<ActionResult> StartProject(string projectName)
{
    try
    {
        // Logique métier avec gestion d'erreurs spécifiques
        var result = await _operationService.UpAsync(projectName);

        if (!result.Success)
        {
            // Erreur métier attendue
            return BadRequest(new { message = result.Message, details = result.Error });
        }

        return Ok(result);
    }
    catch (NotFoundException ex)
    {
        // Exception spécifique avec contexte
        _logger.LogWarning("Projet {ProjectName} introuvable", projectName);
        return NotFound($"Projet '{projectName}' introuvable");
    }
    // Les autres exceptions sont catchées par le middleware global
}
```

## Notifications Temps Réel (SignalR)

### Système Existant

Le système dispose déjà d'une **infrastructure SignalR complète** :

**Hubs** :
- `OperationsHub` : Notifications d'opérations (`/hubs/operations`)
- `LogsHub` : Streaming de logs (`/hubs/logs`)

**Events existants** :
- `ContainerStateChanged` : Changement d'état d'un container
- `ComposeProjectStateChanged` : Changement d'état d'un projet
- `OperationUpdate` : Mise à jour d'opération

### Intégration avec la Nouvelle Approche

**Utiliser les events existants** dans les opérations :

```csharp
// Après une opération (up, down, restart)
await _hubContext.Clients.All.SendAsync("ComposeProjectStateChanged", new
{
    projectName,
    action = "started", // ou "stopped", "restarted"
    timestamp = DateTime.UtcNow
});
```

**Frontend** (déjà connecté aux hubs) :

```typescript
// Le frontend écoute déjà ces events
connection.on("ComposeProjectStateChanged", (data) => {
  // Rafraîchir la liste des projets
  queryClient.invalidateQueries(['compose-projects']);
});
```

**Pas besoin de modifications majeures** : Le système de notifications existant s'adapte parfaitement à la nouvelle approche.

## Gestion du Mapping de Chemins et Accès aux Fichiers

### Principe : Accès aux Fichiers Non Requis pour les Opérations

**Point crucial** : Les opérations sur les projets (up, down, restart) **ne nécessitent PAS** que les fichiers compose soient accessibles au backend.

#### Pourquoi ?

Docker Compose utilise le flag `-p projectName` qui permet de gérer un projet par son nom, **indépendamment** de l'emplacement des fichiers :

```bash
# Ces commandes fonctionnent même si les fichiers ne sont pas accessibles au backend
docker compose -p myapp up -d
docker compose -p myapp down
docker compose -p myapp restart
docker compose -p myapp ps
```

Docker maintient un **registre interne** des projets qui inclut :
- Le nom du projet
- Les services et containers associés
- L'état de chaque service
- **Les chemins des fichiers d'origine** (stockés par Docker)

#### Quand l'Accès aux Fichiers Est-il Nécessaire ?

L'accès aux fichiers compose depuis le backend n'est requis **que pour** :

1. **Édition de fichiers** (fonctionnalité actuellement désactivée)
2. **Création de projets depuis templates** (fonctionnalité actuellement désactivée)
3. **Validation YAML** (non implémentée)

#### Propriété ConfigFiles dans le DTO

La propriété `ConfigFiles` est **purement informative** :

```csharp
public string[] ConfigFiles { get; init; }  // Informatif uniquement (pour affichage)
```

Elle permet à l'utilisateur de **voir** où se trouvent les fichiers compose sur l'hôte, mais le backend n'a **pas besoin** d'y accéder pour gérer le projet.

#### Affichage dans l'UI

```tsx
{/* Affichage informatif des chemins de fichiers */}
<div className="text-sm text-muted-foreground">
  <strong>Fichiers compose :</strong>
  <ul className="mt-1">
    {project.ConfigFiles.map(f => (
      <li key={f} className="font-mono text-xs">{f}</li>
    ))}
  </ul>
  <p className="mt-2 italic">
    Note : Ces chemins sont ceux de l'hôte Docker.
    Le backend n'a pas besoin d'y accéder pour gérer le projet.
  </p>
</div>
```

### Configuration des Volumes (Optionnelle)

Le montage des chemins de fichiers dans le backend est **optionnel** et **uniquement requis** si vous souhaitez réactiver l'édition de fichiers dans le futur :

```yaml
# docker-compose.yml du backend
services:
  backend:
    volumes:
      # OPTIONNEL : Nécessaire uniquement pour l'édition de fichiers (fonctionnalité désactivée)
      - /home/user/compose-projects:/compose-projects:ro
      - /opt/docker-stacks:/docker-stacks:ro
```

**Actuellement, ces volumes ne sont PAS nécessaires pour le fonctionnement de l'application.**

## Performance et Optimisations

### 1. Cache en Mémoire (IMemoryCache)

```csharp
// Configuration
builder.Services.AddMemoryCache();

// Utilisation
_cache.GetOrCreateAsync(CACHE_KEY, async entry =>
{
    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);
    return await FetchProjectsFromDockerAsync();
});
```

**Avantages** :
- ✅ Max 10 secondes de décalage (acceptable)
- ✅ Pas de background service nécessaire
- ✅ Invalidation manuelle après opérations

### 2. Invalidation du Cache

```csharp
// Après chaque opération
_discoveryService.InvalidateCache();
```

**Effet** : La prochaine requête GET récupérera les données fraîches depuis Docker.

### 3. Notifications SignalR

Les clients sont notifiés en temps réel via SignalR, ce qui leur permet de rafraîchir leur UI immédiatement sans attendre l'expiration du cache.

## Considérations Techniques

### Cas Limites et Gestion des Erreurs

| Cas | Comportement | Solution |
|-----|-------------|----------|
| `docker compose ls` échoue | Retourner liste vide, logger warning | ErrorHandlingMiddleware + retry |
| JSON malformé | Parser élément par élément | Try/catch par élément |
| Projet avec nom spécial | Peut causer des problèmes dans les URLs | Encoder avec `Uri.EscapeDataString` |
| Docker Compose v1 | `docker compose ls` non supporté | Fallback via labels (code existant ligne 435-592) |
| ConfigFiles vide/null | Peut arriver si projet créé autrement | Initialiser tableau vide, afficher "Aucun fichier" dans l'UI |

### Sécurité

**Validation des noms de projets** :

```csharp
[HttpGet("{projectName}")]
public async Task<ActionResult> GetProject(string projectName)
{
    projectName = Uri.UnescapeDataString(projectName);

    if (string.IsNullOrWhiteSpace(projectName) || projectName.Length > 255)
        return BadRequest("Nom de projet invalide");

    // Docker accepte les caractères spéciaux, pas de regex stricte nécessaire
    // ...
}
```

**Middleware de gestion d'erreurs** : Déjà en place (ErrorHandlingMiddleware)

## Avantages de la Nouvelle Approche

### Comparaison Avant/Après

| Aspect | Avant | Après |
|--------|-------|-------|
| **Source de vérité** | Filesystem + Docker (hybride) | Docker uniquement |
| **Tables en DB** | 3 (ComposePaths + ComposeFiles + ResourcePermission) | 1 (ResourcePermission) |
| **Synchronisation** | Background service toutes les 30s | Aucune (cache 10s) |
| **Cross-platform** | ❌ Problèmes récurrents | ✅ Résolu |
| **Découverte** | Manuelle (configuration requise) | Automatique |
| **Projets "down"** | ❌ Non découverts (selon config) | ✅ Toujours découverts |
| **Complexité code** | Élevée (~500 lignes) | Réduite (~150 lignes) |
| **Risque de divergence** | Élevé (DB vs Docker) | Inexistant |
| **Performance** | DB query (~5ms) | Cache mémoire (~1ms) ou docker ls (~150ms) |

### Bénéfices Concrets

- ✅ **-60% de code** : Suppression de ComposePaths, ComposeFiles, background service, migrations
- ✅ **0 divergence** : Docker est la source unique, impossible d'être out-of-sync
- ✅ **0 configuration** : Pas besoin de configurer des ComposePaths
- ✅ **0 montage de volumes** : Les opérations fonctionnent via `-p projectName`, sans accès aux fichiers
- ✅ **Cross-platform natif** : Docker gère les chemins, pas nous
- ✅ **Permissions inchangées** : Fonctionne avec le système existant
- ✅ **Stateless** : Facilite le scaling horizontal
- ✅ **Réutilisation maximale** : SignalR, ErrorHandling, Permissions déjà en place

## Plan d'Implémentation

### Phase 1 : Préparation (1-2h)
- [ ] Créer le DTO `ComposeProjectDto`
- [ ] Créer l'enum `ProjectStatus`
- [ ] Créer la classe `OperationResult`

### Phase 2 : Classe Utilitaire Docker (2h)
- [ ] Créer `DockerCommandExecutor`
- [ ] Extraire `IsComposeV2Available()` du ComposeService existant
- [ ] Extraire `ExecuteComposeCommandAsync()` du ComposeService existant
- [ ] Enregistrer comme Singleton dans Program.cs

### Phase 3 : Service de Découverte (3-4h)
- [ ] Créer `IComposeDiscoveryService` et `ComposeDiscoveryService`
- [ ] Implémenter `FetchProjectsFromDockerAsync()`
- [ ] Implémenter `ParseStatus()`
- [ ] Implémenter le cache avec IMemoryCache
- [ ] Implémenter l'intégration avec PermissionService
- [ ] Enregistrer dans Program.cs

### Phase 4 : Service d'Opérations (2-3h)
- [ ] Créer `IComposeOperationService` et `ComposeOperationService`
- [ ] Implémenter `UpAsync()`, `DownAsync()`, `RestartAsync()`
- [ ] Utiliser `DockerCommandExecutor` pour les commandes
- [ ] Enregistrer dans Program.cs

### Phase 5 : API Controller (3-4h)
- [ ] Créer/adapter `ComposeProjectsController`
- [ ] Endpoints : GET /projects, GET /projects/{name}, GET /projects/{name}/services
- [ ] Endpoints : POST /projects/{name}/up, /down, /restart
- [ ] Invalidation du cache après opérations
- [ ] Notifications SignalR après opérations
- [ ] Enrichissement avec permissions utilisateur

### Phase 6 : Frontend (2-3h)
- [ ] Adapter les appels API (`/projects/{name}` au lieu de `/files/{id}`)
- [ ] Afficher les chemins de fichiers compose de manière informative (propriété `ConfigFiles`)
- [ ] S'assurer que les listeners SignalR sont actifs

### Phase 7 : Désactivation Fonctionnalités (1-2h)
- [ ] Marquer endpoints d'édition comme `[Obsolete]` → retourner HTTP 501
- [ ] Marquer endpoints de templates comme `[Obsolete]` → retourner HTTP 501
- [ ] Ajouter commentaires dans `FileService.cs` et `TemplateService.cs`
- [ ] Désactiver UI d'édition (feature flag frontend)
- [ ] Désactiver UI de templates (feature flag frontend)

### Phase 8 : Nettoyage (2-3h)
- [ ] Marquer `ComposePaths` et `ComposeFiles` comme `[Obsolete]`
- [ ] Créer une migration pour supprimer ces tables (à appliquer après validation)
- [ ] Mettre à jour `CLAUDE.md` avec la nouvelle architecture
- [ ] Tests manuels avec différents rôles/permissions
- [ ] Vérifier les notifications SignalR

### Phase 9 : Documentation (1h)
- [ ] Mettre à jour le README
- [ ] Documenter que les volumes Docker sont optionnels (uniquement pour édition de fichiers)
- [ ] Guide utilisateur : "Comment ajouter un projet Compose" (via docker compose up sur l'hôte)

### Estimation Totale : **17-23 heures**

## Conclusion

Cette refonte transforme radicalement l'architecture en éliminant complètement la persistance des projets en base de données.

**Résumé** :
- ❌ **Plus de** : ComposePaths, ComposeFiles, synchronisation, background service, migrations, divergence DB/Docker
- ✅ **Reste** : ResourcePermission (nécessaire pour les permissions)
- ✅ **Ajout** : Cache mémoire simple et efficace, classe utilitaire Docker
- ✅ **Réutilisation** : SignalR, ErrorHandling, Permissions existants

**Point crucial** :
> Les opérations Docker Compose (up, down, restart, ps) fonctionnent via le flag `-p projectName` **sans nécessiter d'accès aux fichiers compose**. Docker maintient son propre registre interne des projets avec leurs configurations. L'accès aux fichiers n'est requis que pour l'édition (fonctionnalité actuellement désactivée).

**Philosophie** :
> Docker est la source de vérité. La base de données ne sert qu'à stocker ce que Docker ne peut pas stocker : les permissions.

Cette approche est **plus simple, plus fiable, plus performante** et **parfaitement alignée** avec les principes de containerisation.
