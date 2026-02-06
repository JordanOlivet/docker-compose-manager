using docker_compose_manager_back.Data;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Hubs;
using docker_compose_manager_back.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace docker_compose_manager_back.Services;

public class OperationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<OperationService> _logger;
    private readonly IHubContext<OperationsHub> _hubContext;

    public OperationService(
        AppDbContext context,
        ILogger<OperationService> logger,
        IHubContext<OperationsHub> hubContext)
    {
        _context = context;
        _logger = logger;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Creates a new operation
    /// </summary>
    public async Task<Operation> CreateOperationAsync(
        string type,
        int? userId,
        string? projectPath = null,
        string? projectName = null)
    {
        try
        {
            Operation operation = new()
            {
                OperationId = Guid.NewGuid().ToString(),
                Type = type,
                Status = OperationStatus.Pending,
                UserId = userId,
                ProjectPath = projectPath,
                ProjectName = projectName,
                StartedAt = DateTime.UtcNow
            };

            _context.Operations.Add(operation);
            await _context.SaveChangesAsync();

            _logger.LogDebug(
                "Created operation: {OperationId}, Type: {Type}, User: {UserId}",
                operation.OperationId,
                type,
                userId
            );

            return operation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating operation");
            throw;
        }
    }

    /// <summary>
    /// Updates operation status
    /// </summary>
    public async Task<bool> UpdateOperationStatusAsync(
        string operationId,
        string status,
        int? progress = null,
        string? errorMessage = null)
    {
        try
        {
            Operation? operation = await _context.Operations
                .FirstOrDefaultAsync(o => o.OperationId == operationId);

            if (operation == null)
            {
                _logger.LogWarning("Operation not found: {OperationId}", operationId);
                return false;
            }

            // Log if a final status is being set again
            bool isFinalStatus = status == OperationStatus.Completed || status == OperationStatus.Failed || status == OperationStatus.Cancelled;
            bool wasFinalStatus = operation.Status == OperationStatus.Completed || operation.Status == OperationStatus.Failed || operation.Status == OperationStatus.Cancelled;

            if (isFinalStatus && wasFinalStatus)
            {
                _logger.LogWarning("[DUPLICATE NOTIFICATION] Operation {OperationId} already had final status {OldStatus}, now set again to {NewStatus}", operationId, operation.Status, status);
            }

            operation.Status = status;

            if (progress.HasValue)
            {
                operation.Progress = progress.Value;
            }

            if (errorMessage != null)
            {
                operation.ErrorMessage = errorMessage;
            }

            if (isFinalStatus)
            {
                operation.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogDebug(
                "Updated operation {OperationId}: Status={Status}, Progress={Progress}",
                operationId,
                status,
                progress
            );

            // Send SignalR notification
            try
            {
                var notification = new
                {
                    operationId,
                    status,
                    progress = progress ?? operation.Progress,
                    errorMessage,
                    type = operation.Type,
                    projectName = operation.ProjectName,
                    projectPath = operation.ProjectPath
                };

                _logger.LogDebug(
                    "Sending SignalR notification - OperationId: {OperationId}, Type: {Type}, Status: {Status}, ProjectName: {ProjectName}",
                    operationId, operation.Type, status, operation.ProjectName
                );

                await _hubContext.Clients.All.SendAsync("OperationUpdate", notification);

                // Also send to specific operation group
                string groupName = $"operation-{operationId}";
                await _hubContext.Clients.Group(groupName).SendAsync("OperationUpdate", notification);

                _logger.LogDebug("SignalR notification sent successfully for operation {OperationId}", operationId);
            }
            catch (Exception signalREx)
            {
                _logger.LogWarning(signalREx, "Failed to send SignalR notification for operation {OperationId}", operationId);
                // Don't fail the operation if SignalR notification fails
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating operation status: {OperationId}", operationId);
            return false;
        }
    }

    /// <summary>
    /// Appends logs to an operation
    /// </summary>
    public async Task<bool> AppendLogsAsync(string operationId, string logs)
    {
        try
        {
            Operation? operation = await _context.Operations
                .FirstOrDefaultAsync(o => o.OperationId == operationId);

            if (operation == null)
            {
                _logger.LogWarning("Operation not found: {OperationId}", operationId);
                return false;
            }

            operation.Logs = string.IsNullOrEmpty(operation.Logs)
                ? logs
                : operation.Logs + "\n" + logs;

            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error appending logs to operation: {OperationId}", operationId);
            return false;
        }
    }

    /// <summary>
    /// Gets an operation by ID
    /// </summary>
    public async Task<Operation?> GetOperationAsync(string operationId)
    {
        try
        {
            return await _context.Operations
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.OperationId == operationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving operation: {OperationId}", operationId);
            return null;
        }
    }

    /// <summary>
    /// Gets an operation by database ID
    /// </summary>
    public async Task<Operation?> GetOperationByIdAsync(int id)
    {
        try
        {
            return await _context.Operations
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving operation: {Id}", id);
            return null;
        }
    }

    /// <summary>
    /// Lists operations with optional filtering
    /// </summary>
    public async Task<List<Operation>> ListOperationsAsync(
        string? status = null,
        int? userId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 100)
    {
        try
        {
            IQueryable<Operation> query = _context.Operations
                .Include(o => o.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.Status == status);
            }

            if (userId.HasValue)
            {
                query = query.Where(o => o.UserId == userId.Value);
            }

            if (startDate.HasValue)
            {
                query = query.Where(o => o.StartedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(o => o.StartedAt <= endDate.Value);
            }

            return await query
                .OrderByDescending(o => o.StartedAt)
                .Take(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing operations");
            return new List<Operation>();
        }
    }

    /// <summary>
    /// Cancels an operation
    /// </summary>
    public async Task<bool> CancelOperationAsync(string operationId)
    {
        try
        {
            Operation? operation = await _context.Operations
                .FirstOrDefaultAsync(o => o.OperationId == operationId);

            if (operation == null)
            {
                _logger.LogWarning("Operation not found: {OperationId}", operationId);
                return false;
            }

            if (operation.Status != OperationStatus.Pending &&
                operation.Status != OperationStatus.Running)
            {
                _logger.LogWarning(
                    "Cannot cancel operation {OperationId} with status {Status}",
                    operationId,
                    operation.Status
                );
                return false;
            }

            operation.Status = OperationStatus.Cancelled;
            operation.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogDebug("Cancelled operation: {OperationId}", operationId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling operation: {OperationId}", operationId);
            return false;
        }
    }

    /// <summary>
    /// Cleans up old completed operations (for maintenance)
    /// </summary>
    public async Task<int> CleanupOldOperationsAsync(DateTime beforeDate)
    {
        try
        {
            List<Operation> oldOperations = await _context.Operations
                .Where(o => o.CompletedAt != null && o.CompletedAt < beforeDate)
                .ToListAsync();

            int count = oldOperations.Count;

            if (count > 0)
            {
                _context.Operations.RemoveRange(oldOperations);
                await _context.SaveChangesAsync();

                _logger.LogDebug("Cleaned up {Count} old operations before {Date}", count, beforeDate);
            }

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old operations");
            return 0;
        }
    }

    /// <summary>
    /// Gets active (running) operations count
    /// </summary>
    public async Task<int> GetActiveOperationsCountAsync()
    {
        try
        {
            return await _context.Operations
                .CountAsync(o => o.Status == OperationStatus.Running || o.Status == OperationStatus.Pending);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active operations count");
            return 0;
        }
    }

    /// <summary>
    /// Sends a pull progress update via SignalR for real-time UI updates.
    /// </summary>
    public async Task SendPullProgressAsync(UpdateProgressEvent progress)
    {
        try
        {
            _logger.LogDebug(
                "Sending pull progress - Operation: {OperationId}, Project: {ProjectName}, Phase: {Phase}, Progress: {Progress}%",
                progress.OperationId,
                progress.ProjectName,
                progress.Phase,
                progress.OverallProgress
            );

            // Send to all connected clients
            await _hubContext.Clients.All.SendAsync("PullProgressUpdate", progress);

            // Also send to operation-specific group
            string groupName = $"operation-{progress.OperationId}";
            await _hubContext.Clients.Group(groupName).SendAsync("PullProgressUpdate", progress);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send pull progress update for operation {OperationId}", progress.OperationId);
            // Don't fail the operation if SignalR notification fails
        }
    }
}
