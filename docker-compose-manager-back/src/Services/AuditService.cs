using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using docker_compose_manager_back.Data;
using docker_compose_manager_back.Models;

namespace docker_compose_manager_back.Services;

public class AuditService
{
    private readonly AppDbContext _context;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AppDbContext context, ILogger<AuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Logs an audit entry
    /// </summary>
    public async Task LogActionAsync(
        int? userId,
        string action,
        string ipAddress,
        string? details = null,
        string? resourceType = null,
        string? resourceId = null,
        object? before = null,
        object? after = null)
    {
        try
        {
            AuditLog auditLog = new AuditLog
            {
                UserId = userId,
                Action = action,
                IpAddress = ipAddress,
                Details = details,
                ResourceType = resourceType,
                ResourceId = resourceId,
                Timestamp = DateTime.UtcNow
            };

            // Serialize before/after states if provided
            if (before != null)
            {
                auditLog.BeforeState = JsonSerializer.Serialize(before);
            }

            if (after != null)
            {
                auditLog.AfterState = JsonSerializer.Serialize(after);
            }

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Audit log created: User={UserId}, Action={Action}, Resource={ResourceType}/{ResourceId}",
                userId,
                action,
                resourceType,
                resourceId
            );
        }
        catch (Exception ex)
        {
            // Don't throw exceptions from audit logging - log the error but continue
            _logger.LogError(ex, "Error creating audit log entry");
        }
    }

    /// <summary>
    /// Gets audit logs with optional filtering
    /// </summary>
    public async Task<(List<AuditLog> Logs, int TotalCount)> GetAuditLogsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? userId = null,
        string? action = null,
        string? resourceType = null,
        int pageNumber = 1,
        int pageSize = 50)
    {
        try
        {
            IQueryable<AuditLog> query = _context.AuditLogs
                .Include(al => al.User)
                .AsQueryable();

            // Apply filters
            if (startDate.HasValue)
            {
                query = query.Where(al => al.Timestamp >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(al => al.Timestamp <= endDate.Value);
            }

            if (userId.HasValue)
            {
                query = query.Where(al => al.UserId == userId.Value);
            }

            if (!string.IsNullOrEmpty(action))
            {
                query = query.Where(al => al.Action == action);
            }

            if (!string.IsNullOrEmpty(resourceType))
            {
                query = query.Where(al => al.ResourceType == resourceType);
            }

            int totalCount = await query.CountAsync();

            // Apply pagination
            List<AuditLog> logs = await query
                .OrderByDescending(al => al.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (logs, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs");
            return (new List<AuditLog>(), 0);
        }
    }

    /// <summary>
    /// Gets a specific audit log by ID
    /// </summary>
    public async Task<AuditLog?> GetAuditLogByIdAsync(int id)
    {
        try
        {
            return await _context.AuditLogs
                .Include(al => al.User)
                .FirstOrDefaultAsync(al => al.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit log: {Id}", id);
            return null;
        }
    }

    /// <summary>
    /// Gets audit logs for a specific user
    /// </summary>
    public async Task<List<AuditLog>> GetUserAuditLogsAsync(int userId, int limit = 100)
    {
        try
        {
            return await _context.AuditLogs
                .Where(al => al.UserId == userId)
                .OrderByDescending(al => al.Timestamp)
                .Take(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user audit logs: {UserId}", userId);
            return new List<AuditLog>();
        }
    }

    /// <summary>
    /// Gets audit logs for a specific resource
    /// </summary>
    public async Task<List<AuditLog>> GetResourceAuditLogsAsync(
        string resourceType,
        string resourceId,
        int limit = 100)
    {
        try
        {
            return await _context.AuditLogs
                .Include(al => al.User)
                .Where(al => al.ResourceType == resourceType && al.ResourceId == resourceId)
                .OrderByDescending(al => al.Timestamp)
                .Take(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving resource audit logs: {ResourceType}/{ResourceId}",
                resourceType,
                resourceId
            );
            return new List<AuditLog>();
        }
    }

    /// <summary>
    /// Purges old audit logs (admin only operation)
    /// </summary>
    public async Task<int> PurgeOldLogsAsync(DateTime beforeDate)
    {
        try
        {
            List<AuditLog> oldLogs = await _context.AuditLogs
                .Where(al => al.Timestamp < beforeDate)
                .ToListAsync();

            int count = oldLogs.Count;

            if (count > 0)
            {
                _context.AuditLogs.RemoveRange(oldLogs);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Purged {Count} old audit logs before {Date}", count, beforeDate);
            }

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error purging old audit logs");
            return 0;
        }
    }

    /// <summary>
    /// Gets distinct action types from audit logs
    /// </summary>
    public async Task<List<string>> GetDistinctActionsAsync()
    {
        try
        {
            return await _context.AuditLogs
                .Select(al => al.Action)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving distinct actions");
            return new List<string>();
        }
    }

    /// <summary>
    /// Gets distinct resource types from audit logs
    /// </summary>
    public async Task<List<string>> GetDistinctResourceTypesAsync()
    {
        try
        {
            return await _context.AuditLogs
                .Where(al => al.ResourceType != null)
                .Select(al => al.ResourceType!)
                .Distinct()
                .OrderBy(rt => rt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving distinct resource types");
            return new List<string>();
        }
    }
}

/// <summary>
/// Audit action constants
/// </summary>
public static class AuditActions
{
    // Authentication
    public const string Login = "auth.login";
    public const string Logout = "auth.logout";
    public const string PasswordChange = "auth.password_change";
    public const string TokenRefresh = "auth.token_refresh";
    public const string LoginFailed = "auth.login_failed";

    // Container actions
    public const string ContainerStart = "container.start";
    public const string ContainerStop = "container.stop";
    public const string ContainerRestart = "container.restart";
    public const string ContainerRemove = "container.remove";
    public const string ContainerList = "container.list";
    public const string ContainerInspect = "container.inspect";

    // Compose file actions
    public const string FileCreate = "file.create";
    public const string FileUpdate = "file.update";
    public const string FileDelete = "file.delete";
    public const string FileRead = "file.read";
    public const string FileList = "file.list";

    // Compose project actions
    public const string ComposeUp = "compose.up";
    public const string ComposeDown = "compose.down";
    public const string ComposeLogs = "compose.logs";
    public const string ComposeList = "compose.list";

    // User management
    public const string UserCreate = "user.create";
    public const string UserUpdate = "user.update";
    public const string UserDelete = "user.delete";
    public const string UserList = "user.list";

    // Settings
    public const string SettingsUpdate = "settings.update";
    public const string ComposePathAdd = "compose_path.add";
    public const string ComposePathRemove = "compose_path.remove";

    // Audit
    public const string AuditView = "audit.view";
    public const string AuditPurge = "audit.purge";
}
