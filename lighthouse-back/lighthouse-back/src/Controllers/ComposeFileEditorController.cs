using Lighthouse.DTOs;
using Lighthouse.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lighthouse.Controllers;

/// <summary>
/// Editing endpoints for a compose project's files (compose file + adjacent .env). Split out of
/// ComposeController to keep the write concern focused; shares the <c>api/compose</c> route
/// prefix. Files are resolved server-side from the project name — no path ever crosses the API.
/// Permission and domain failures are thrown as <see cref="Exceptions.AppException"/> subclasses
/// and mapped by ErrorHandlingMiddleware.
/// </summary>
[ApiController]
[Route("api/compose")]
[Authorize]
public class ComposeFileEditorController : BaseController
{
    private readonly IComposeFileEditorService _editorService;
    private readonly ILogger<ComposeFileEditorController> _logger;

    public ComposeFileEditorController(
        IComposeFileEditorService editorService,
        ILogger<ComposeFileEditorController> logger)
    {
        _editorService = editorService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the editable files of a compose project (compose file content + adjacent .env).
    /// Requires View permission on the project.
    /// </summary>
    [HttpGet("projects/{projectName}/files")]
    public async Task<ActionResult<ApiResponse<ProjectFilesResponseDto>>> GetProjectFiles(string projectName)
    {
        projectName = Uri.UnescapeDataString(projectName);
        int userId = GetCurrentUserIdRequired();

        ProjectFilesResponseDto response = await _editorService.GetProjectFilesAsync(userId, projectName);
        return Ok(ApiResponse.Ok(response));
    }

    /// <summary>
    /// Updates one editable file of a compose project (optimistic locking via ETag).
    /// Requires Edit permission on the project.
    /// </summary>
    [HttpPut("projects/{projectName}/files")]
    public async Task<ActionResult<ApiResponse<ProjectFileDto>>> UpdateProjectFile(
        string projectName,
        [FromBody] UpdateProjectFileRequest request)
    {
        projectName = Uri.UnescapeDataString(projectName);
        int userId = GetCurrentUserIdRequired();

        ProjectFileDto updated = await _editorService.UpdateProjectFileAsync(userId, projectName, request);

        _logger.LogInformation("Compose file edited: {Kind} file {FileName} of project {ProjectName}",
            request.Kind, updated.FileName, projectName);

        return Ok(ApiResponse.Ok(updated));
    }
}
