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
public class ConfigController : BaseController
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
    [Obsolete("ComposePath management disabled - see COMPOSE_DISCOVERY_REFACTOR.md")]
    public IActionResult GetComposePaths()
    {
        return StatusCode(501, new
        {
            success = false,
            message = "ComposePath management is disabled.",
            reason = "Projects discovered automatically via 'docker compose ls'. No path configuration needed.",
            documentation = "See COMPOSE_DISCOVERY_REFACTOR.md"
        });
    }

    /// <summary>
    /// Add new compose file path
    /// </summary>
    [HttpPost("paths")]
    [Obsolete("ComposePath management disabled - see COMPOSE_DISCOVERY_REFACTOR.md")]
    public IActionResult AddComposePath([FromBody] AddComposePathRequest request)
    {
        return StatusCode(501, new
        {
            success = false,
            message = "ComposePath management is disabled.",
            reason = "Projects discovered automatically via 'docker compose ls'. No path configuration needed.",
            documentation = "See COMPOSE_DISCOVERY_REFACTOR.md"
        });
    }

    /// <summary>
    /// Update compose path settings
    /// </summary>
    [HttpPut("paths/{id}")]
    [Obsolete("ComposePath management disabled - see COMPOSE_DISCOVERY_REFACTOR.md")]
    public IActionResult UpdateComposePath(int id, [FromBody] UpdateComposePathRequest request)
    {
        return StatusCode(501, new
        {
            success = false,
            message = "ComposePath management is disabled.",
            reason = "Projects discovered automatically via 'docker compose ls'. No path configuration needed.",
            documentation = "See COMPOSE_DISCOVERY_REFACTOR.md"
        });
    }

    /// <summary>
    /// Delete compose path
    /// </summary>
    [HttpDelete("paths/{id}")]
    [Obsolete("ComposePath management disabled - see COMPOSE_DISCOVERY_REFACTOR.md")]
    public IActionResult DeleteComposePath(int id)
    {
        return StatusCode(501, new
        {
            success = false,
            message = "ComposePath management is disabled.",
            reason = "Projects discovered automatically via 'docker compose ls'. No path configuration needed.",
            documentation = "See COMPOSE_DISCOVERY_REFACTOR.md"
        });
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
