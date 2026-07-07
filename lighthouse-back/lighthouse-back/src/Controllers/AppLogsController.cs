using Lighthouse.DTOs;
using Lighthouse.Services.LogStreaming;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lighthouse.Controllers;

/// <summary>
/// Application log endpoints (admin only): paged history read from the CLEF log files
/// and a live SSE tail fed by the in-process Serilog broadcast sink. This is the
/// diagnostic surface that replaced the audit-log system — "who did what" is answered
/// by the Username property enriched on every authenticated request's events.
/// </summary>
[ApiController]
[Route("api/app-logs")]
[Authorize(Roles = "admin")]
public class AppLogsController : BaseController
{
    private const int MaxTail = 1000;

    private readonly IAppLogService _appLogService;
    private readonly ILogger<AppLogsController> _logger;

    public AppLogsController(IAppLogService appLogService, ILogger<AppLogsController> logger)
    {
        _appLogService = appLogService;
        _logger = logger;
    }

    /// <summary>
    /// Get a page of historical application logs, for infinite scroll-up pagination.
    /// Entries are sorted ascending by timestamp.
    /// </summary>
    /// <param name="tail">Number of lines per page (default 200, max 1000)</param>
    /// <param name="until">ISO-8601 cursor — return lines strictly before this timestamp (default: now)</param>
    /// <param name="levels">Comma-separated level names to include (e.g. "Warning,Error"); empty = all</param>
    /// <param name="category">Case-insensitive substring filter on the logger category</param>
    /// <param name="user">Case-insensitive exact filter on the username</param>
    /// <param name="search">Case-insensitive substring filter on message + exception</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<AppLogPageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AppLogPageDto>>> GetHistory(
        [FromQuery] int tail = 200,
        [FromQuery] string? until = null,
        [FromQuery] string? levels = null,
        [FromQuery] string? category = null,
        [FromQuery] string? user = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        tail = Math.Clamp(tail, 1, MaxTail);

        try
        {
            AppLogPageDto page = await _appLogService.GetHistoryAsync(
                BuildFilter(levels, category, user, search), tail, until, cancellationToken);
            return Ok(ApiResponse.Ok(page, $"Retrieved {page.Entries.Count} log lines"));
        }
        catch (FormatException)
        {
            return BadRequest(ApiResponse.Fail<AppLogPageDto>("Invalid 'until' timestamp", "VALIDATION_ERROR"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading application log history");
            return StatusCode(500, ApiResponse.Fail<AppLogPageDto>(
                "Failed to read application logs", "SERVER_ERROR"));
        }
    }

    /// <summary>
    /// Stream application logs in real-time via SSE. Sends the last
    /// <paramref name="tail"/> matching lines first, then follows new events.
    /// Events: connected, logs (batched JSON), error. See AppLogEntryDto for the entry shape.
    /// </summary>
    /// <param name="tail">Number of historical lines (default 200, max 1000)</param>
    /// <param name="levels">Comma-separated level names to include; empty = all</param>
    /// <param name="category">Case-insensitive substring filter on the logger category</param>
    /// <param name="user">Case-insensitive exact filter on the username</param>
    /// <param name="search">Case-insensitive substring filter on message + exception</param>
    /// <param name="cancellationToken">Cancels the stream when the client disconnects.</param>
    [HttpGet("stream")]
    public async Task StreamLogs(
        [FromQuery] int tail = 200,
        [FromQuery] string? levels = null,
        [FromQuery] string? category = null,
        [FromQuery] string? user = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        tail = Math.Clamp(tail, 1, MaxTail);

        await SseLogStreamWriter.RunAsync(
            HttpContext,
            _appLogService.StreamAsync(BuildFilter(levels, category, user, search), tail, cancellationToken),
            _logger,
            cancellationToken);
    }

    private static AppLogFilter BuildFilter(string? levels, string? category, string? user, string? search)
    {
        HashSet<string>? levelSet = null;
        if (!string.IsNullOrWhiteSpace(levels))
        {
            levelSet = levels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return new AppLogFilter(levelSet, category, user, search);
    }
}
