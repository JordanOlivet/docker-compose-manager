using docker_compose_manager_back.Data;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace docker_compose_manager_back.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ComposeController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly FileService _fileService;
    private readonly ComposeService _composeService;
    private readonly OperationService _operationService;
    private readonly IAuditService _auditService;
    private readonly ILogger<ComposeController> _logger;

    public ComposeController(
        AppDbContext context,
        FileService fileService,
        ComposeService composeService,
        OperationService operationService,
        IAuditService auditService,
        ILogger<ComposeController> logger)
    {
        _context = context;
        _fileService = fileService;
        _composeService = composeService;
        _operationService = operationService;
        _auditService = auditService;
        _logger = logger;
    }

    private int? GetCurrentUserId()
    {
        string? userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdStr, out int userId) ? userId : null;
    }

    private string GetUserIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    // ============================================
    // Compose Files Endpoints
    // ============================================

    /// <summary>
    /// Lists all compose files
    /// </summary>
    [HttpGet("files")]
    public async Task<ActionResult<ApiResponse<List<ComposeFileDto>>>> ListFiles()
    {
        try
        {
            List<ComposeFile> files = await _context.ComposeFiles
                .Include(cf => cf.ComposePath)
                .OrderBy(cf => cf.FullPath)
                .ToListAsync();

            List<ComposeFileDto> fileDtos = new();

            foreach (ComposeFile file in files)
            {
                (bool success, FileInfo fileInfo, string _) = await _fileService.GetFileInfoAsync(file.FullPath);
                if (success && fileInfo != null)
                {
                    string directory = Path.GetDirectoryName(file.FullPath) ?? string.Empty;
                    fileDtos.Add(new ComposeFileDto(
                        file.Id,
                        file.FileName,
                        file.FullPath,
                        directory,
                        fileInfo.Length,
                        file.LastModified,
                        file.LastScanned,
                        file.ComposePathId,
                        file.IsDiscovered
                    ));
                }
            }

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                AuditActions.FileList,
                GetUserIpAddress(),
                "Listed compose files"
            );

            return Ok(ApiResponse.Ok(fileDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing compose files");
            return StatusCode(500, ApiResponse.Fail<List<ComposeFileDto>>("Error listing files", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Gets a compose file by ID with content
    /// </summary>
    [HttpGet("files/{id}")]
    public async Task<ActionResult<ApiResponse<ComposeFileContentDto>>> GetFile(int id)
    {
        try
        {
            ComposeFile? file = await _context.ComposeFiles.FindAsync(id);
            if (file == null)
            {
                return NotFound(ApiResponse.Fail<ComposeFileContentDto>("File not found", "FILE_NOT_FOUND"));
            }

            (bool success, string content, string error) = await _fileService.ReadFileAsync(file.FullPath);
            if (!success || content == null)
            {
                return BadRequest(ApiResponse.Fail<ComposeFileContentDto>(error ?? "Error reading file", "READ_ERROR"));
            }

            string etag = _fileService.CalculateETag(content);

            ComposeFileContentDto dto = new(
                file.Id,
                file.FileName,
                file.FullPath,
                content,
                etag,
                file.LastModified
            );

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                AuditActions.FileRead,
                GetUserIpAddress(),
                resourceType: "compose_file",
                resourceId: id.ToString()
            );

            return Ok(ApiResponse.Ok(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compose file: {Id}", id);
            return StatusCode(500, ApiResponse.Fail<ComposeFileContentDto>("Error getting file", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Gets a compose file by path
    /// </summary>
    [HttpGet("files/by-path")]
    public async Task<ActionResult<ApiResponse<ComposeFileContentDto>>> GetFileByPath([FromQuery] string path)
    {
        try
        {
            ComposeFile? file = await _context.ComposeFiles
                .FirstOrDefaultAsync(cf => cf.FullPath == path);

            if (file == null)
            {
                return NotFound(ApiResponse.Fail<ComposeFileContentDto>("File not found", "FILE_NOT_FOUND"));
            }

            return await GetFile(file.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compose file by path: {Path}", path);
            return StatusCode(500, ApiResponse.Fail<ComposeFileContentDto>("Error getting file", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Creates a new compose file
    /// </summary>
    [HttpPost("files")]
    public async Task<ActionResult<ApiResponse<ComposeFileDto>>> CreateFile([FromBody] CreateComposeFileRequest request)
    {
        try
        {
            // Validate YAML syntax
            (bool isValid, string validationError) = _fileService.ValidateYamlSyntax(request.Content);
            if (!isValid)
            {
                return BadRequest(ApiResponse.Fail<ComposeFileDto>(validationError ?? "Invalid YAML", "INVALID_YAML"));
            }

            // Validate path is in a configured ComposePath
            (bool pathValid, string pathError, ComposePath allowedPath) = await _fileService.ValidateFilePathAsync(request.FilePath);
            if (!pathValid || allowedPath == null)
            {
                return BadRequest(ApiResponse.Fail<ComposeFileDto>(pathError ?? "Invalid file path", "INVALID_PATH"));
            }

            // Check if file already exists in database
            ComposeFile? existingFile = await _context.ComposeFiles
                .FirstOrDefaultAsync(cf => cf.FullPath == request.FilePath);

            if (existingFile != null)
            {
                return Conflict(ApiResponse.Fail<ComposeFileDto>("File already exists", "FILE_EXISTS"));
            }

            // Write file
            (bool success, string error) = await _fileService.WriteFileAsync(request.FilePath, request.Content, createBackup: false);
            if (!success)
            {
                return BadRequest(ApiResponse.Fail<ComposeFileDto>(error ?? "Error writing file", "WRITE_ERROR"));
            }

            // Create database entry manually (not discovered, but manually created)
            string fileName = Path.GetFileName(request.FilePath);
            ComposeFile file = new()
            {
                ComposePathId = allowedPath.Id,
                FileName = fileName,
                FullPath = request.FilePath,
                LastModified = DateTime.UtcNow,
                LastScanned = DateTime.UtcNow,
                IsDiscovered = false // Manually created, not discovered
            };

            _context.ComposeFiles.Add(file);
            await _context.SaveChangesAsync();

            (bool _, FileInfo fileInfo, string _) = await _fileService.GetFileInfoAsync(file.FullPath);

            string directory = Path.GetDirectoryName(file.FullPath) ?? string.Empty;
            ComposeFileDto dto = new(
                file.Id,
                file.FileName,
                file.FullPath,
                directory,
                fileInfo?.Length ?? 0,
                file.LastModified,
                file.LastScanned,
                file.ComposePathId,
                file.IsDiscovered
            );

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                AuditActions.FileCreate,
                GetUserIpAddress(),
                $"Created compose file: {file.FileName}",
                resourceType: "compose_file",
                resourceId: file.Id.ToString(),
                after: request
            );

            return CreatedAtAction(nameof(GetFile), new { id = file.Id }, ApiResponse.Ok(dto, "File created successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating compose file: {FilePath}", request.FilePath);
            return StatusCode(500, ApiResponse.Fail<ComposeFileDto>("Error creating file", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Updates a compose file
    /// </summary>
    [HttpPut("files/{id}")]
    public async Task<ActionResult<ApiResponse<ComposeFileDto>>> UpdateFile(int id, [FromBody] UpdateComposeFileRequest request)
    {
        try
        {
            ComposeFile? file = await _context.ComposeFiles.FindAsync(id);
            if (file == null)
            {
                return NotFound(ApiResponse.Fail<ComposeFileDto>("File not found", "FILE_NOT_FOUND"));
            }

            // Read current content for ETag validation and audit
            (bool readSuccess, string currentContent, string _) = await _fileService.ReadFileAsync(file.FullPath);
            if (!readSuccess || currentContent == null)
            {
                return BadRequest(ApiResponse.Fail<ComposeFileDto>("Error reading current file", "READ_ERROR"));
            }

            string currentETag = _fileService.CalculateETag(currentContent);

            // Validate ETag for optimistic locking
            if (currentETag != request.ETag)
            {
                return Conflict(ApiResponse.Fail<ComposeFileDto>("File has been modified by another user", "ETAG_MISMATCH"));
            }

            // Validate YAML syntax
            (bool isValid, string validationError) = _fileService.ValidateYamlSyntax(request.Content);
            if (!isValid)
            {
                return BadRequest(ApiResponse.Fail<ComposeFileDto>(validationError ?? "Invalid YAML", "INVALID_YAML"));
            }

            // Write file (with backup)
            (bool success, string error) = await _fileService.WriteFileAsync(file.FullPath, request.Content, createBackup: true);
            if (!success)
            {
                return BadRequest(ApiResponse.Fail<ComposeFileDto>(error ?? "Error writing file", "WRITE_ERROR"));
            }

            // Update database record
            file.LastModified = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            (bool _, FileInfo fileInfo, string _) = await _fileService.GetFileInfoAsync(file.FullPath);

            string directory = Path.GetDirectoryName(file.FullPath) ?? string.Empty;
            ComposeFileDto dto = new(
                file.Id,
                file.FileName,
                file.FullPath,
                directory,
                fileInfo?.Length ?? 0,
                file.LastModified,
                file.LastScanned,
                file.ComposePathId,
                file.IsDiscovered
            );

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                AuditActions.FileUpdate,
                GetUserIpAddress(),
                $"Updated compose file: {file.FileName}",
                resourceType: "compose_file",
                resourceId: file.Id.ToString(),
                before: new { content = currentContent },
                after: new { content = request.Content }
            );

            return Ok(ApiResponse.Ok(dto, "File updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating compose file: {Id}", id);
            return StatusCode(500, ApiResponse.Fail<ComposeFileDto>("Error updating file", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Deletes a compose file
    /// </summary>
    [HttpDelete("files/{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteFile(int id)
    {
        try
        {
            ComposeFile? file = await _context.ComposeFiles.FindAsync(id);
            if (file == null)
            {
                return NotFound(ApiResponse.Fail<bool>("File not found", "FILE_NOT_FOUND"));
            }

            string filePath = file.FullPath;
            string fileName = file.FileName;

            // Delete file from filesystem
            (bool success, string error) = await _fileService.DeleteFileAsync(filePath);
            if (!success)
            {
                return BadRequest(ApiResponse.Fail<bool>(error ?? "Error deleting file", "DELETE_ERROR"));
            }

            // Remove from database
            _context.ComposeFiles.Remove(file);
            await _context.SaveChangesAsync();

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                AuditActions.FileDelete,
                GetUserIpAddress(),
                $"Deleted compose file: {fileName}",
                resourceType: "compose_file",
                resourceId: id.ToString()
            );

            return Ok(ApiResponse.Ok(true, "File deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting compose file: {Id}", id);
            return StatusCode(500, ApiResponse.Fail<bool>("Error deleting file", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Validate YAML syntax and docker-compose structure of a compose file
    /// </summary>
    [HttpPost("files/{id}/validate")]
    public async Task<ActionResult<ApiResponse<ComposeValidationResult>>> ValidateFile(int id)
    {
        try
        {
            ComposeFile? file = await _context.ComposeFiles.FindAsync(id);
            if (file == null)
            {
                return NotFound(ApiResponse.Fail<ComposeValidationResult>("File not found", "FILE_NOT_FOUND"));
            }

            // Read file content
            (bool readSuccess, string content, string readError) = await _fileService.ReadFileAsync(file.FullPath);
            if (!readSuccess || content == null)
            {
                return BadRequest(ApiResponse.Fail<ComposeValidationResult>(
                    readError ?? "Error reading file", "READ_ERROR"));
            }

            // Validate YAML syntax
            (bool isValidYaml, string yamlError) = _fileService.ValidateYamlSyntax(content);

            ComposeValidationResult validationResult = new()
            {
                IsValid = isValidYaml,
                YamlValid = isValidYaml,
                YamlError = yamlError,
                Warnings = new List<string>(),
                ServiceCount = 0
            };

            if (isValidYaml)
            {
                // Additional docker-compose specific validation
                try
                {
                    // Check for required 'services' key
                    if (!content.Contains("services:"))
                    {
                        validationResult.IsValid = false;
                        validationResult.Warnings.Add("Missing 'services:' key - not a valid docker-compose file");
                    }
                    else
                    {
                        // Try to count services (basic parsing)
                        string[] lines = content.Split('\n');
                        int serviceCount = 0;
                        bool inServices = false;

                        foreach (string line in lines)
                        {
                            string trimmed = line.TrimStart();
                            if (trimmed.StartsWith("services:"))
                            {
                                inServices = true;
                                continue;
                            }

                            if (inServices && trimmed.Length > 0 && !trimmed.StartsWith("#"))
                            {
                                // Check if this is a service definition (not indented or less indented)
                                if (line.StartsWith("  ") && !line.StartsWith("    "))
                                {
                                    serviceCount++;
                                }
                                // If we hit another top-level key, stop counting
                                else if (!line.StartsWith(" ") && !trimmed.StartsWith("services:"))
                                {
                                    break;
                                }
                            }
                        }

                        validationResult.ServiceCount = serviceCount;

                        if (serviceCount == 0)
                        {
                            validationResult.Warnings.Add("No services defined in compose file");
                        }
                    }

                    // Check for common issues
                    if (content.Contains("version:"))
                    {
                        validationResult.Warnings.Add("Version field is deprecated in docker-compose v2+");
                    }
                }
                catch (Exception ex)
                {
                    validationResult.Warnings.Add($"Could not perform full validation: {ex.Message}");
                }
            }

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                "compose.file_validate",
                GetUserIpAddress(),
                $"Validated compose file: {file.FileName} (valid: {validationResult.IsValid})",
                resourceType: "compose_file",
                resourceId: id.ToString()
            );

            return Ok(ApiResponse.Ok(validationResult, "File validation completed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating compose file: {Id}", id);
            return StatusCode(500, ApiResponse.Fail<ComposeValidationResult>("Error validating file", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Duplicate/clone a compose file
    /// </summary>
    [HttpPost("files/{id}/duplicate")]
    public async Task<ActionResult<ApiResponse<ComposeFileDto>>> DuplicateFile(
        int id,
        [FromBody] DuplicateFileRequest request)
    {
        try
        {
            ComposeFile? sourceFile = await _context.ComposeFiles
                .Include(cf => cf.ComposePath)
                .FirstOrDefaultAsync(cf => cf.Id == id);

            if (sourceFile == null)
            {
                return NotFound(ApiResponse.Fail<ComposeFileDto>("Source file not found", "FILE_NOT_FOUND"));
            }

            // Read source file content
            (bool readSuccess, string content, string readError) = await _fileService.ReadFileAsync(sourceFile.FullPath);
            if (!readSuccess || content == null)
            {
                return BadRequest(ApiResponse.Fail<ComposeFileDto>(
                    readError ?? "Error reading source file", "READ_ERROR"));
            }

            // Determine new file path
            string newFileName = !string.IsNullOrWhiteSpace(request.NewFileName)
                ? request.NewFileName
                : $"{Path.GetFileNameWithoutExtension(sourceFile.FileName)}-copy{Path.GetExtension(sourceFile.FileName)}";

            // Ensure .yml or .yaml extension
            if (!newFileName.EndsWith(".yml") && !newFileName.EndsWith(".yaml"))
            {
                newFileName += ".yml";
            }

            string newFilePath = Path.Combine(sourceFile.ComposePath.Path, newFileName);

            // Check if file already exists
            if (System.IO.File.Exists(newFilePath))
            {
                return Conflict(ApiResponse.Fail<ComposeFileDto>(
                    $"File '{newFileName}' already exists", "FILE_EXISTS"));
            }

            // Validate path
            (bool isValid, string validationError, ComposePath allowedPath) = await _fileService.ValidateFilePathAsync(newFilePath);
            if (!isValid)
            {
                return BadRequest(ApiResponse.Fail<ComposeFileDto>(
                    validationError ?? "Invalid file path", "INVALID_PATH"));
            }

            // Write new file
            (bool writeSuccess, string writeError) = await _fileService.WriteFileAsync(newFilePath, content, createBackup: false);
            if (!writeSuccess)
            {
                return BadRequest(ApiResponse.Fail<ComposeFileDto>(
                    writeError ?? "Error writing file", "WRITE_ERROR"));
            }

            // Create database record
            ComposeFile newFile = new()
            {
                ComposePathId = sourceFile.ComposePathId,
                FileName = newFileName,
                FullPath = newFilePath,
                LastModified = DateTime.UtcNow,
                LastScanned = DateTime.UtcNow
            };

            _context.ComposeFiles.Add(newFile);
            await _context.SaveChangesAsync();

            (bool _, FileInfo fileInfo, string _) = await _fileService.GetFileInfoAsync(newFilePath);

            string directory = Path.GetDirectoryName(newFile.FullPath) ?? string.Empty;
            ComposeFileDto dto = new(
                newFile.Id,
                newFile.FileName,
                newFile.FullPath,
                directory,
                fileInfo?.Length ?? 0,
                newFile.LastModified,
                newFile.LastScanned,
                newFile.ComposePathId,
                newFile.IsDiscovered
            );

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                AuditActions.FileCreate,
                GetUserIpAddress(),
                $"Duplicated compose file: {sourceFile.FileName} -> {newFileName}",
                resourceType: "compose_file",
                resourceId: newFile.Id.ToString(),
                after: new { sourceId = id, newId = newFile.Id }
            );

            _logger.LogInformation("Compose file {SourceFile} duplicated to {NewFile}", sourceFile.FileName, newFileName);

            return CreatedAtAction(nameof(GetFile), new { id = newFile.Id }, ApiResponse.Ok(dto, "File duplicated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error duplicating compose file: {Id}", id);
            return StatusCode(500, ApiResponse.Fail<ComposeFileDto>("Error duplicating file", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Download a compose file
    /// </summary>
    [HttpGet("files/{id}/download")]
    public async Task<IActionResult> DownloadFile(int id)
    {
        try
        {
            ComposeFile? file = await _context.ComposeFiles.FindAsync(id);
            if (file == null)
            {
                return NotFound(ApiResponse.Fail<object>("File not found", "FILE_NOT_FOUND"));
            }

            // Read file content
            (bool success, string content, string error) = await _fileService.ReadFileAsync(file.FullPath);
            if (!success || content == null)
            {
                return BadRequest(ApiResponse.Fail<object>(
                    error ?? "Error reading file", "READ_ERROR"));
            }

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                "compose.file_download",
                GetUserIpAddress(),
                $"Downloaded compose file: {file.FileName}",
                resourceType: "compose_file",
                resourceId: id.ToString()
            );

            // Return file as attachment
            byte[] fileBytes = System.Text.Encoding.UTF8.GetBytes(content);
            return File(fileBytes, "application/x-yaml", file.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading compose file: {Id}", id);
            return StatusCode(500, ApiResponse.Fail<object>("Error downloading file", "SERVER_ERROR"));
        }
    }

    // ============================================
    // Compose Projects Endpoints
    // ============================================

    /// <summary>
    /// Lists all compose projects
    /// </summary>
    [HttpGet("projects")]
    public async Task<ActionResult<ApiResponse<List<ComposeProjectDto>>>> ListProjects()
    {
        try
        {
            List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
            List<ComposeProjectDto> projects = new();

            foreach (string projectPath in projectPaths)
            {
                string projectName = _composeService.GetProjectName(projectPath);
                (bool success, string output, string _) = await _composeService.ListServicesAsync(projectPath);

                List<ComposeServiceDto> services = new();
                string status = "unknown";

                if (success && !string.IsNullOrWhiteSpace(output))
                {
                    try
                    {
                        // Parse NDJSON output from docker compose ps (each line is a separate JSON object)
                        string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (string line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line)) { continue; }

                            try
                            {
                                System.Text.Json.JsonElement svc = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(line);

                                // Extract service information from JSON
                                string serviceName = svc.TryGetProperty("Service", out System.Text.Json.JsonElement svcName)
                                    ? svcName.GetString() ?? "unknown"
                                    : "unknown";

                                string serviceState = svc.TryGetProperty("State", out System.Text.Json.JsonElement state)
                                    ? state.GetString() ?? "unknown"
                                    : "unknown";

                                string serviceImage = svc.TryGetProperty("Image", out System.Text.Json.JsonElement img)
                                    ? img.GetString() ?? "unknown"
                                    : "unknown";

                                // Parse ports
                                List<string> ports = new();
                                if (svc.TryGetProperty("Publishers", out System.Text.Json.JsonElement publishers) && publishers.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (System.Text.Json.JsonElement publisher in publishers.EnumerateArray())
                                    {
                                        if (publisher.TryGetProperty("URL", out System.Text.Json.JsonElement url) &&
                                            publisher.TryGetProperty("PublishedPort", out System.Text.Json.JsonElement publishedPort) &&
                                            publisher.TryGetProperty("TargetPort", out System.Text.Json.JsonElement targetPort))
                                        {
                                            string portMapping = $"{url.GetString()}:{publishedPort.GetInt32()}->{targetPort.GetInt32()}";
                                            ports.Add(portMapping);
                                        }
                                    }
                                }

                                services.Add(new ComposeServiceDto(
                                    Name: serviceName,
                                    Image: serviceImage,
                                    Status: serviceState,
                                    Ports: ports,
                                    Replicas: null,
                                    Health: svc.TryGetProperty("Health", out System.Text.Json.JsonElement health) ? health.GetString() : null
                                ));
                            }
                            catch (System.Text.Json.JsonException lineEx)
                            {
                                _logger.LogWarning(lineEx, "Failed to parse JSON line for project {ProjectName}: {Line}", projectName, line);
                            }
                        }

                        // Determine overall project status based on service states
                        if (services.Count > 0)
                        {
                            status = DetermineProjectStatus(services);
                        }
                        else
                        {
                            // No services found - project is down
                            status = "down";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse docker compose ps output for project: {ProjectName}", projectName);
                        status = "unknown";
                    }
                }
                else
                {
                    // Command failed or no output - project is likely down
                    status = "down";
                }

                projects.Add(new ComposeProjectDto(
                    projectName,
                    projectPath,
                    status,
                    services,
                    _composeService.GetComposeFiles(projectPath),
                    DateTime.UtcNow
                ));
            }

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                AuditActions.ComposeList,
                GetUserIpAddress(),
                "Listed compose projects"
            );

            return Ok(ApiResponse.Ok(projects));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing compose projects");
            return StatusCode(500, ApiResponse.Fail<List<ComposeProjectDto>>("Error listing projects", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Determines the overall status of a project based on its services
    /// </summary>
    private string DetermineProjectStatus(List<ComposeServiceDto> services)
    {
        if (services.Count == 0)
            return "down";

        int runningCount = services.Count(s =>
            s.Status.Equals("running", StringComparison.OrdinalIgnoreCase));

        int exitedCount = services.Count(s =>
            s.Status.Equals("exited", StringComparison.OrdinalIgnoreCase));

        int restartingCount = services.Count(s =>
            s.Status.Equals("restarting", StringComparison.OrdinalIgnoreCase));

        // All services running - project is fully up
        if (runningCount == services.Count)
            return "running";

        // Some services running - project is degraded
        if (runningCount > 0)
            return "degraded";

        // No services running but some are restarting
        if (restartingCount > 0)
            return "restarting";

        // All services exited or stopped
        if (exitedCount > 0)
            return "exited";

        // Default to stopped
        return "stopped";
    }

    /// <summary>
    /// Starts a compose project (docker compose up)
    /// </summary>
    [HttpPost("projects/{projectName}/up")]
    public async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> UpProject(
        string projectName,
        [FromBody] ComposeUpRequest request)
    {
        try
        {
            // Find project path
            List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
            string? projectPath = projectPaths.FirstOrDefault(p => _composeService.GetProjectName(p) == projectName);

            if (projectPath == null)
            {
                return NotFound(ApiResponse.Fail<ComposeOperationResponse>("Project not found", "PROJECT_NOT_FOUND"));
            }

            // Create operation tracking
            Operation operation = await _operationService.CreateOperationAsync(
                OperationType.ComposeUp,
                GetCurrentUserId(),
                projectPath,
                projectName
            );

            // Start compose up in background
            _ = Task.Run(async () =>
            {
                await _operationService.UpdateOperationStatusAsync(operation.OperationId, OperationStatus.Running);

                (bool success, string output, string error) = await _composeService.UpProjectAsync(
                    projectPath,
                    request.Build,
                    request.Detach,
                    request.ForceRecreate
                );

                await _operationService.AppendLogsAsync(operation.OperationId, output);
                if (!string.IsNullOrEmpty(error))
                {
                    await _operationService.AppendLogsAsync(operation.OperationId, $"ERROR: {error}");
                }

                await _operationService.UpdateOperationStatusAsync(
                    operation.OperationId,
                    success ? OperationStatus.Completed : OperationStatus.Failed,
                    100,
                    success ? null : error
                );
            });

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                AuditActions.ComposeUp,
                GetUserIpAddress(),
                $"Started compose up: {projectName}",
                resourceType: "compose_project",
                resourceId: projectName
            );

            ComposeOperationResponse response = new(
                operation.OperationId,
                OperationStatus.Pending,
                $"Compose up started for project: {projectName}"
            );

            return Ok(ApiResponse.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting compose up for project: {ProjectName}", projectName);
            return StatusCode(500, ApiResponse.Fail<ComposeOperationResponse>("Error starting project", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Stops a compose project (docker compose down)
    /// </summary>
    [HttpPost("projects/{projectName}/down")]
    public async Task<ActionResult<ApiResponse<ComposeOperationResponse>>> DownProject(
        string projectName,
        [FromBody] ComposeDownRequest request)
    {
        try
        {
            // Find project path
            List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
            string? projectPath = projectPaths.FirstOrDefault(p => _composeService.GetProjectName(p) == projectName);

            if (projectPath == null)
            {
                return NotFound(ApiResponse.Fail<ComposeOperationResponse>("Project not found", "PROJECT_NOT_FOUND"));
            }

            // Create operation tracking
            Operation operation = await _operationService.CreateOperationAsync(
                OperationType.ComposeDown,
                GetCurrentUserId(),
                projectPath,
                projectName
            );

            // Start compose down in background
            _ = Task.Run(async () =>
            {
                await _operationService.UpdateOperationStatusAsync(operation.OperationId, OperationStatus.Running);

                (bool success, string output, string error) = await _composeService.DownProjectAsync(
                    projectPath,
                    request.RemoveVolumes,
                    request.RemoveImages
                );

                await _operationService.AppendLogsAsync(operation.OperationId, output);
                if (!string.IsNullOrEmpty(error))
                {
                    await _operationService.AppendLogsAsync(operation.OperationId, $"ERROR: {error}");
                }

                await _operationService.UpdateOperationStatusAsync(
                    operation.OperationId,
                    success ? OperationStatus.Completed : OperationStatus.Failed,
                    100,
                    success ? null : error
                );
            });

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                AuditActions.ComposeDown,
                GetUserIpAddress(),
                $"Started compose down: {projectName}",
                resourceType: "compose_project",
                resourceId: projectName
            );

            ComposeOperationResponse response = new(
                operation.OperationId,
                OperationStatus.Pending,
                $"Compose down started for project: {projectName}"
            );

            return Ok(ApiResponse.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting compose down for project: {ProjectName}", projectName);
            return StatusCode(500, ApiResponse.Fail<ComposeOperationResponse>("Error stopping project", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Gets logs from a compose project
    /// </summary>
    [HttpGet("projects/{projectName}/logs")]
    public async Task<ActionResult<ApiResponse<string>>> GetProjectLogs(
        string projectName,
        [FromQuery] string? serviceName = null,
        [FromQuery] int? tail = 100)
    {
        try
        {
            // Find project path
            List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
            string? projectPath = projectPaths.FirstOrDefault(p => _composeService.GetProjectName(p) == projectName);

            if (projectPath == null)
            {
                return NotFound(ApiResponse.Fail<string>("Project not found", "PROJECT_NOT_FOUND"));
            }

            (bool success, string output, string error) = await _composeService.GetLogsAsync(
                projectPath,
                serviceName,
                tail,
                follow: false
            );

            if (!success)
            {
                return BadRequest(ApiResponse.Fail<string>(error ?? "Error getting logs", "LOGS_ERROR"));
            }

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                AuditActions.ComposeLogs,
                GetUserIpAddress(),
                $"Retrieved logs for project: {projectName}",
                resourceType: "compose_project",
                resourceId: projectName
            );

            return Ok(ApiResponse.Ok(output));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting logs for project: {ProjectName}", projectName);
            return StatusCode(500, ApiResponse.Fail<string>("Error getting logs", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Get available compose file templates
    /// </summary>
    [HttpGet("templates")]
    public ActionResult<ApiResponse<List<ComposeTemplateDto>>> GetTemplates()
    {
        try
        {
            List<ComposeTemplateDto> templates = new()
            {
                new ComposeTemplateDto(
                    "wordpress",
                    "WordPress + MySQL",
                    "Complete WordPress installation with MySQL database",
                    @"version: '3.8'

services:
  wordpress:
    image: wordpress:latest
    ports:
      - ""80:80""
    environment:
      WORDPRESS_DB_HOST: db
      WORDPRESS_DB_USER: wordpress
      WORDPRESS_DB_PASSWORD: wordpress
      WORDPRESS_DB_NAME: wordpress
    volumes:
      - wordpress_data:/var/www/html
    depends_on:
      - db

  db:
    image: mysql:8.0
    environment:
      MYSQL_DATABASE: wordpress
      MYSQL_USER: wordpress
      MYSQL_PASSWORD: wordpress
      MYSQL_RANDOM_ROOT_PASSWORD: '1'
    volumes:
      - db_data:/var/lib/mysql

volumes:
  wordpress_data:
  db_data:"
                ),
                new ComposeTemplateDto(
                    "nginx-php",
                    "Nginx + PHP-FPM",
                    "Web server with Nginx and PHP-FPM",
                    @"version: '3.8'

services:
  nginx:
    image: nginx:alpine
    ports:
      - ""80:80""
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ./www:/var/www/html
    depends_on:
      - php

  php:
    image: php:8.2-fpm
    volumes:
      - ./www:/var/www/html"
                ),
                new ComposeTemplateDto(
                    "postgres-redis",
                    "PostgreSQL + Redis",
                    "PostgreSQL database with Redis cache",
                    @"version: '3.8'

services:
  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: mydb
      POSTGRES_USER: myuser
      POSTGRES_PASSWORD: mypassword
    ports:
      - ""5432:5432""
    volumes:
      - postgres_data:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    ports:
      - ""6379:6379""
    volumes:
      - redis_data:/data

volumes:
  postgres_data:
  redis_data:"
                ),
                new ComposeTemplateDto(
                    "traefik",
                    "Traefik Reverse Proxy",
                    "Traefik reverse proxy with Let's Encrypt",
                    @"version: '3.8'

services:
  traefik:
    image: traefik:v2.10
    command:
      - --api.dashboard=true
      - --providers.docker=true
      - --entrypoints.web.address=:80
      - --entrypoints.websecure.address=:443
    ports:
      - ""80:80""
      - ""443:443""
      - ""8080:8080""
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
      - ./acme.json:/acme.json
    labels:
      - traefik.enable=true"
                ),
                new ComposeTemplateDto(
                    "monitoring",
                    "Prometheus + Grafana",
                    "Monitoring stack with Prometheus and Grafana",
                    @"version: '3.8'

services:
  prometheus:
    image: prom/prometheus:latest
    ports:
      - ""9090:9090""
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - prometheus_data:/prometheus

  grafana:
    image: grafana/grafana:latest
    ports:
      - ""3000:3000""
    environment:
      GF_SECURITY_ADMIN_PASSWORD: admin
    volumes:
      - grafana_data:/var/lib/grafana
    depends_on:
      - prometheus

volumes:
  prometheus_data:
  grafana_data:"
                )
            };

            return Ok(ApiResponse.Ok(templates, "Templates retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compose templates");
            return StatusCode(500, ApiResponse.Fail<List<ComposeTemplateDto>>("Error getting templates", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Get detailed information about a specific compose project
    /// </summary>
    [HttpGet("projects/{projectName}")]
    public async Task<ActionResult<ApiResponse<ComposeProjectDetailsDto>>> GetProjectDetails(string projectName)
    {
        try
        {
            // Find project path
            List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
            string? projectPath = projectPaths.FirstOrDefault(p => _composeService.GetProjectName(p) == projectName);

            if (projectPath == null)
            {
                return NotFound(ApiResponse.Fail<ComposeProjectDetailsDto>("Project not found", "PROJECT_NOT_FOUND"));
            }

            // Get project status by running docker compose ps
            (int psExitCode, string psOutput, string psError) = await _composeService.ExecuteComposeCommandAsync(
                projectPath,
                "ps --format json"
            );
            bool psSuccess = psExitCode == 0;

            List<ComposeServiceStatusDto> services = new();
            if (psSuccess && !string.IsNullOrWhiteSpace(psOutput))
            {
                try
                {
                    // Parse JSON output
                    List<System.Text.Json.JsonElement>? jsonServices = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(psOutput);
                    if (jsonServices != null)
                    {
                        foreach (System.Text.Json.JsonElement svc in jsonServices)
                        {
                            services.Add(new ComposeServiceStatusDto(
                                svc.GetProperty("Service").GetString() ?? "unknown",
                                svc.GetProperty("State").GetString() ?? "unknown",
                                svc.GetProperty("Status").GetString() ?? ""
                            ));
                        }
                    }
                }
                catch
                {
                    // Fallback: treat output as text
                    _logger.LogWarning("Could not parse docker compose ps JSON output");
                }
            }

            ComposeProjectDetailsDto projectDetails = new(
                projectName,
                projectPath,
                services.Any(s => s.State == "running"),
                services.Count,
                services.Count(s => s.State == "running"),
                services.Count(s => s.State != "running"),
                services
            );

            return Ok(ApiResponse.Ok(projectDetails, "Project details retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project details for: {ProjectName}", projectName);
            return StatusCode(500, ApiResponse.Fail<ComposeProjectDetailsDto>("Error getting project details", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Start compose services (docker compose start)
    /// </summary>
    [HttpPost("projects/{projectName}/start")]
    public async Task<ActionResult<ApiResponse<bool>>> StartProject(string projectName)
    {
        try
        {
            // Find project path
            List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
            string? projectPath = projectPaths.FirstOrDefault(p => _composeService.GetProjectName(p) == projectName);

            if (projectPath == null)
            {
                return NotFound(ApiResponse.Fail<bool>("Project not found", "PROJECT_NOT_FOUND"));
            }

            (int exitCode, string output, string error) = await _composeService.ExecuteComposeCommandAsync(
                projectPath,
                "start"
            );
            bool success = exitCode == 0;

            if (!success)
            {
                return BadRequest(ApiResponse.Fail<bool>(error ?? "Error starting project", "START_ERROR"));
            }

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                AuditActions.ComposeStart,
                GetUserIpAddress(),
                $"Started compose project: {projectName}",
                resourceType: "compose_project",
                resourceId: projectName
            );

            _logger.LogInformation("Compose project {ProjectName} started", projectName);

            return Ok(ApiResponse.Ok(true, "Project started successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting project: {ProjectName}", projectName);
            return StatusCode(500, ApiResponse.Fail<bool>("Error starting project", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Stop compose services (docker compose stop)
    /// </summary>
    [HttpPost("projects/{projectName}/stop")]
    public async Task<ActionResult<ApiResponse<bool>>> StopProject(string projectName)
    {
        try
        {
            // Find project path
            List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
            string? projectPath = projectPaths.FirstOrDefault(p => _composeService.GetProjectName(p) == projectName);

            if (projectPath == null)
            {
                return NotFound(ApiResponse.Fail<bool>("Project not found", "PROJECT_NOT_FOUND"));
            }

            (int exitCode, string output, string error) = await _composeService.ExecuteComposeCommandAsync(
                projectPath,
                "stop"
            );
            bool success = exitCode == 0;

            if (!success)
            {
                return BadRequest(ApiResponse.Fail<bool>(error ?? "Error stopping project", "STOP_ERROR"));
            }

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                AuditActions.ComposeStop,
                GetUserIpAddress(),
                $"Stopped compose project: {projectName}",
                resourceType: "compose_project",
                resourceId: projectName
            );

            _logger.LogInformation("Compose project {ProjectName} stopped", projectName);

            return Ok(ApiResponse.Ok(true, "Project stopped successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping project: {ProjectName}", projectName);
            return StatusCode(500, ApiResponse.Fail<bool>("Error stopping project", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Restart compose services (docker compose restart)
    /// </summary>
    [HttpPost("projects/{projectName}/restart")]
    public async Task<ActionResult<ApiResponse<bool>>> RestartProject(string projectName)
    {
        try
        {
            // Find project path
            List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
            string? projectPath = projectPaths.FirstOrDefault(p => _composeService.GetProjectName(p) == projectName);

            if (projectPath == null)
            {
                return NotFound(ApiResponse.Fail<bool>("Project not found", "PROJECT_NOT_FOUND"));
            }

            (int exitCode, string output, string error) = await _composeService.ExecuteComposeCommandAsync(
                projectPath,
                "restart"
            );
            bool success = exitCode == 0;

            if (!success)
            {
                return BadRequest(ApiResponse.Fail<bool>(error ?? "Error restarting project", "RESTART_ERROR"));
            }

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                AuditActions.ComposeRestart,
                GetUserIpAddress(),
                $"Restarted compose project: {projectName}",
                resourceType: "compose_project",
                resourceId: projectName
            );

            _logger.LogInformation("Compose project {ProjectName} restarted", projectName);

            return Ok(ApiResponse.Ok(true, "Project restarted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting project: {ProjectName}", projectName);
            return StatusCode(500, ApiResponse.Fail<bool>("Error restarting project", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Get status of all services in a compose project (docker compose ps)
    /// </summary>
    [HttpGet("projects/{projectName}/ps")]
    public async Task<ActionResult<ApiResponse<List<ComposeServiceStatusDto>>>> GetProjectServices(string projectName)
    {
        try
        {
            // Find project path
            List<string> projectPaths = await _composeService.DiscoverComposeProjectsAsync();
            string? projectPath = projectPaths.FirstOrDefault(p => _composeService.GetProjectName(p) == projectName);

            if (projectPath == null)
            {
                return NotFound(ApiResponse.Fail<List<ComposeServiceStatusDto>>("Project not found", "PROJECT_NOT_FOUND"));
            }

            (int exitCode, string output, string error) = await _composeService.ExecuteComposeCommandAsync(
                projectPath,
                "ps --format json"
            );
            bool success = exitCode == 0;

            if (!success)
            {
                return BadRequest(ApiResponse.Fail<List<ComposeServiceStatusDto>>(error ?? "Error getting services", "PS_ERROR"));
            }

            List<ComposeServiceStatusDto> services = new();

            if (!string.IsNullOrWhiteSpace(output))
            {
                try
                {
                    // Parse JSON output
                    List<System.Text.Json.JsonElement>? jsonServices = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(output);
                    if (jsonServices != null)
                    {
                        foreach (System.Text.Json.JsonElement svc in jsonServices)
                        {
                            services.Add(new ComposeServiceStatusDto(
                                svc.GetProperty("Service").GetString() ?? "unknown",
                                svc.GetProperty("State").GetString() ?? "unknown",
                                svc.GetProperty("Status").GetString() ?? ""
                            ));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not parse docker compose ps JSON output");
                    // Return empty list if parsing fails
                }
            }

            await _auditService.LogActionAsync(
                GetCurrentUserId(),
                "compose.ps",
                GetUserIpAddress(),
                $"Retrieved services status for project: {projectName}",
                resourceType: "compose_project",
                resourceId: projectName
            );

            return Ok(ApiResponse.Ok(services, "Services status retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting services for project: {ProjectName}", projectName);
            return StatusCode(500, ApiResponse.Fail<List<ComposeServiceStatusDto>>("Error getting services", "SERVER_ERROR"));
        }
    }
}

public record ComposeTemplateDto(string Id, string Name, string Description, string Content);
