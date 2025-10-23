using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using docker_compose_manager_back.Data;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.Services;

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
    private readonly AuditService _auditService;
    private readonly ILogger<ComposeController> _logger;

    public ComposeController(
        AppDbContext context,
        FileService fileService,
        ComposeService composeService,
        OperationService operationService,
        AuditService auditService,
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
                var (success, fileInfo, _) = await _fileService.GetFileInfoAsync(file.FullPath);
                if (success && fileInfo != null)
                {
                    fileDtos.Add(new ComposeFileDto(
                        file.Id,
                        file.FileName,
                        file.FullPath,
                        fileInfo.Length,
                        file.LastModified,
                        file.LastScanned
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

            var (success, content, error) = await _fileService.ReadFileAsync(file.FullPath);
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
            var (isValid, validationError) = _fileService.ValidateYamlSyntax(request.Content);
            if (!isValid)
            {
                return BadRequest(ApiResponse.Fail<ComposeFileDto>(validationError ?? "Invalid YAML", "INVALID_YAML"));
            }

            // Write file
            var (success, error) = await _fileService.WriteFileAsync(request.FilePath, request.Content, createBackup: false);
            if (!success)
            {
                return BadRequest(ApiResponse.Fail<ComposeFileDto>(error ?? "Error writing file", "WRITE_ERROR"));
            }

            // Sync database
            await _fileService.SyncDatabaseWithDiscoveredFilesAsync();

            // Get the created file
            ComposeFile? file = await _context.ComposeFiles
                .FirstOrDefaultAsync(cf => cf.FullPath == request.FilePath);

            if (file == null)
            {
                return StatusCode(500, ApiResponse.Fail<ComposeFileDto>("File created but not found in database", "SYNC_ERROR"));
            }

            var (_, fileInfo, _) = await _fileService.GetFileInfoAsync(file.FullPath);

            ComposeFileDto dto = new(
                file.Id,
                file.FileName,
                file.FullPath,
                fileInfo?.Length ?? 0,
                file.LastModified,
                file.LastScanned
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
            var (readSuccess, currentContent, _) = await _fileService.ReadFileAsync(file.FullPath);
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
            var (isValid, validationError) = _fileService.ValidateYamlSyntax(request.Content);
            if (!isValid)
            {
                return BadRequest(ApiResponse.Fail<ComposeFileDto>(validationError ?? "Invalid YAML", "INVALID_YAML"));
            }

            // Write file (with backup)
            var (success, error) = await _fileService.WriteFileAsync(file.FullPath, request.Content, createBackup: true);
            if (!success)
            {
                return BadRequest(ApiResponse.Fail<ComposeFileDto>(error ?? "Error writing file", "WRITE_ERROR"));
            }

            // Update database record
            file.LastModified = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var (_, fileInfo, _) = await _fileService.GetFileInfoAsync(file.FullPath);

            ComposeFileDto dto = new(
                file.Id,
                file.FileName,
                file.FullPath,
                fileInfo?.Length ?? 0,
                file.LastModified,
                file.LastScanned
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
            var (success, error) = await _fileService.DeleteFileAsync(filePath);
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
                var (success, output, _) = await _composeService.ListServicesAsync(projectPath);

                List<ComposeServiceDto> services = new();
                string status = "unknown";

                if (success)
                {
                    // Parse services from output (simplified - would need proper JSON parsing in production)
                    status = "up";
                }

                projects.Add(new ComposeProjectDto(
                    projectName,
                    projectPath,
                    status,
                    services,
                    _composeService.GetComposeFiles(projectPath),
                    null
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

                var (success, output, error) = await _composeService.UpProjectAsync(
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

                var (success, output, error) = await _composeService.DownProjectAsync(
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

            var (success, output, error) = await _composeService.GetLogsAsync(
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
}
