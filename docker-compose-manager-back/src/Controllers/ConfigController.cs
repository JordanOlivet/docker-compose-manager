using docker_compose_manager_back.Data;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace docker_compose_manager_back.Controllers;

/// <summary>
/// Configuration management endpoints (Admin only)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class ConfigController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(AppDbContext context, ILogger<ConfigController> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region Compose Paths Management

    /// <summary>
    /// Get all configured compose file paths
    /// </summary>
    [HttpGet("paths")]
    [ProducesResponseType(typeof(ApiResponse<List<ComposePath>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ComposePath>>>> GetComposePaths()
    {
        try
        {
            var paths = await _context.ComposePaths
                .OrderBy(p => p.Path)
                .ToListAsync();

            return Ok(ApiResponse.Ok(paths, "Compose paths retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving compose paths");
            return StatusCode(500, ApiResponse.Fail<List<ComposePath>>("Failed to retrieve compose paths"));
        }
    }

    /// <summary>
    /// Add new compose file path
    /// </summary>
    [HttpPost("paths")]
    [ProducesResponseType(typeof(ApiResponse<ComposePath>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<ComposePath>>> AddComposePath([FromBody] AddComposePathRequest request)
    {
        try
        {
            // Validate path exists
            if (!Directory.Exists(request.Path))
            {
                return BadRequest(ApiResponse.Fail<ComposePath>($"Directory '{request.Path}' does not exist"));
            }

            // Check if path already exists
            var existing = await _context.ComposePaths
                .FirstOrDefaultAsync(p => p.Path == request.Path);

            if (existing != null)
            {
                return Conflict(ApiResponse.Fail<ComposePath>($"Path '{request.Path}' is already configured"));
            }

            var composePath = new ComposePath
            {
                Path = request.Path,
                IsReadOnly = request.IsReadOnly,
                IsEnabled = true
            };

            _context.ComposePaths.Add(composePath);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Compose path added: {Path} (ReadOnly: {IsReadOnly})", composePath.Path, composePath.IsReadOnly);

            return CreatedAtAction(
                nameof(GetComposePaths),
                new { },
                ApiResponse.Ok(composePath, "Compose path added successfully")
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding compose path");
            return StatusCode(500, ApiResponse.Fail<ComposePath>("Failed to add compose path"));
        }
    }

    /// <summary>
    /// Update compose path settings
    /// </summary>
    [HttpPut("paths/{id}")]
    [ProducesResponseType(typeof(ApiResponse<ComposePath>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ComposePath>>> UpdateComposePath(int id, [FromBody] UpdateComposePathRequest request)
    {
        try
        {
            var composePath = await _context.ComposePaths.FindAsync(id);
            if (composePath == null)
            {
                return NotFound(ApiResponse.Fail<ComposePath>($"Compose path with ID {id} not found"));
            }

            if (request.IsReadOnly.HasValue)
            {
                composePath.IsReadOnly = request.IsReadOnly.Value;
            }

            if (request.IsEnabled.HasValue)
            {
                composePath.IsEnabled = request.IsEnabled.Value;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Compose path updated: {Path}", composePath.Path);

            return Ok(ApiResponse.Ok(composePath, "Compose path updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating compose path {Id}", id);
            return StatusCode(500, ApiResponse.Fail<ComposePath>("Failed to update compose path"));
        }
    }

    /// <summary>
    /// Delete compose path
    /// </summary>
    [HttpDelete("paths/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteComposePath(int id)
    {
        try
        {
            var composePath = await _context.ComposePaths.FindAsync(id);
            if (composePath == null)
            {
                return NotFound(ApiResponse.Fail<object>($"Compose path with ID {id} not found"));
            }

            // Delete associated compose files first
            var files = await _context.ComposeFiles
                .Where(f => f.ComposePathId == id)
                .ToListAsync();

            _context.ComposeFiles.RemoveRange(files);
            _context.ComposePaths.Remove(composePath);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Compose path deleted: {Path} (with {FileCount} associated files)", composePath.Path, files.Count);

            return Ok(ApiResponse.Ok<object>(null, "Compose path deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting compose path {Id}", id);
            return StatusCode(500, ApiResponse.Fail<object>("Failed to delete compose path"));
        }
    }

    #endregion

    #region Directory Browser

    /// <summary>
    /// Browse filesystem directories (for folder picker)
    /// </summary>
    [HttpGet("browse")]
    [ProducesResponseType(typeof(ApiResponse<DirectoryBrowseResult>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<DirectoryBrowseResult>> BrowseDirectories([FromQuery] string? path = null)
    {
        try
        {
            // Default to root directories if no path provided
            string currentPath = path ?? string.Empty;

            // Validate and normalize path
            if (!string.IsNullOrEmpty(currentPath))
            {
                try
                {
                    currentPath = Path.GetFullPath(currentPath);
                    if (!Directory.Exists(currentPath))
                    {
                        return BadRequest(ApiResponse.Fail<DirectoryBrowseResult>("Directory does not exist"));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Invalid path provided: {Path}", currentPath);
                    return BadRequest(ApiResponse.Fail<DirectoryBrowseResult>("Invalid path"));
                }
            }

            var result = new DirectoryBrowseResult
            {
                CurrentPath = currentPath,
                Directories = new List<DirectoryBrowseInfo>()
            };

            if (string.IsNullOrEmpty(currentPath))
            {
                // Return root drives/directories
                if (OperatingSystem.IsWindows())
                {
                    var drives = DriveInfo.GetDrives()
                        .Where(d => d.IsReady)
                        .Select(d => new DirectoryBrowseInfo
                        {
                            Name = d.Name,
                            Path = d.RootDirectory.FullName,
                            IsAccessible = true
                        })
                        .ToList();
                    result.Directories = drives;
                }
                else
                {
                    // Unix-like systems start from root
                    result.CurrentPath = "/";
                    currentPath = "/";
                }
            }

            if (!string.IsNullOrEmpty(currentPath))
            {
                try
                {
                    var dirInfo = new System.IO.DirectoryInfo(currentPath);

                    // Get parent directory info
                    if (dirInfo.Parent != null)
                    {
                        result.ParentPath = dirInfo.Parent.FullName;
                    }

                    // Get subdirectories
                    var directories = dirInfo.GetDirectories()
                        .OrderBy(d => d.Name)
                        .Select(d =>
                        {
                            bool isAccessible = true;
                            try
                            {
                                // Test if we can access the directory
                                _ = d.GetDirectories();
                            }
                            catch
                            {
                                isAccessible = false;
                            }

                            return new DirectoryBrowseInfo
                            {
                                Name = d.Name,
                                Path = d.FullName,
                                IsAccessible = isAccessible
                            };
                        })
                        .ToList();

                    result.Directories = directories;
                }
                catch (UnauthorizedAccessException)
                {
                    return StatusCode(403, ApiResponse.Fail<DirectoryBrowseResult>("Access denied to this directory"));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error browsing directory: {Path}", currentPath);
                    return StatusCode(500, ApiResponse.Fail<DirectoryBrowseResult>("Error reading directory"));
                }
            }

            return Ok(ApiResponse.Ok(result, "Directory contents retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in directory browser");
            return StatusCode(500, ApiResponse.Fail<DirectoryBrowseResult>("Failed to browse directories"));
        }
    }

    #endregion

    #region Application Settings

    /// <summary>
    /// Get all application settings
    /// </summary>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, string>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, string>>>> GetSettings()
    {
        try
        {
            var settings = await _context.AppSettings.ToListAsync();
            var settingsDict = settings.ToDictionary(s => s.Key, s => s.Value);

            return Ok(ApiResponse.Ok(settingsDict, "Settings retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving settings");
            return StatusCode(500, ApiResponse.Fail<Dictionary<string, string>>("Failed to retrieve settings"));
        }
    }

    /// <summary>
    /// Update application setting
    /// </summary>
    [HttpPut("settings/{key}")]
    [ProducesResponseType(typeof(ApiResponse<AppSetting>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AppSetting>>> UpdateSetting(string key, [FromBody] UpdateSettingRequest request)
    {
        try
        {
            var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);

            if (setting == null)
            {
                // Create new setting
                setting = new AppSetting
                {
                    Key = key,
                    Value = request.Value,
                    Description = request.Description
                };
                _context.AppSettings.Add(setting);
            }
            else
            {
                // Update existing setting
                setting.Value = request.Value;
                if (request.Description != null)
                {
                    setting.Description = request.Description;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Setting updated: {Key} = {Value}", key, request.Value);

            return Ok(ApiResponse.Ok(setting, "Setting updated successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating setting {Key}", key);
            return StatusCode(500, ApiResponse.Fail<AppSetting>("Failed to update setting"));
        }
    }

    /// <summary>
    /// Delete application setting
    /// </summary>
    [HttpDelete("settings/{key}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteSetting(string key)
    {
        try
        {
            var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (setting == null)
            {
                return NotFound(ApiResponse.Fail<object>($"Setting with key '{key}' not found"));
            }

            _context.AppSettings.Remove(setting);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Setting deleted: {Key}", key);

            return Ok(ApiResponse.Ok<object>(null, "Setting deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting setting {Key}", key);
            return StatusCode(500, ApiResponse.Fail<object>("Failed to delete setting"));
        }
    }

    #endregion
}

// DTOs for Config endpoints
public record AddComposePathRequest(string Path, bool IsReadOnly = false);
public record UpdateComposePathRequest(bool? IsReadOnly, bool? IsEnabled);
public record UpdateSettingRequest(string Value, string? Description = null);

// DTOs for Directory Browser
public class DirectoryBrowseResult
{
    public string CurrentPath { get; set; } = string.Empty;
    public string? ParentPath { get; set; }
    public List<DirectoryBrowseInfo> Directories { get; set; } = new();
}

public class DirectoryBrowseInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsAccessible { get; set; }
}
