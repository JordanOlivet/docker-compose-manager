using Lighthouse.DTOs;
using Lighthouse.Models;

namespace Lighthouse.Services;

/// <summary>
/// Tracks long-running operations (compose/container actions, updates) and their logs.
/// Extracted as an interface so consumers (controllers, background services) can be
/// unit-tested with a mock instead of the concrete DB-backed implementation.
/// </summary>
public interface IOperationService
{
    Task<Operation> CreateOperationAsync(
        string type,
        int? userId,
        string? projectPath = null,
        string? projectName = null,
        string? containerId = null,
        string? containerName = null,
        string? operationId = null);

    Task<bool> UpdateOperationStatusAsync(
        string operationId,
        string status,
        int? progress = null,
        string? errorMessage = null);

    Task<bool> AppendLogsAsync(string operationId, string logs);

    Task<Operation?> GetOperationAsync(string operationId);

    Task<Operation?> GetOperationByIdAsync(int id);

    Task<List<Operation>> ListOperationsAsync(
        string? status = null,
        int? userId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 100);

    Task<bool> CancelOperationAsync(string operationId);

    Task<bool> AcknowledgeOperationAsync(string operationId);

    Task<int> AcknowledgeAllFailedAsync();

    Task<int> CleanupOldOperationsAsync(DateTime beforeDate);

    Task<Dictionary<string, Operation>> GetLastOperationByEntitiesAsync();

    Task<List<Operation>> ListOperationsFilteredAsync(
        string? status = null,
        string? projectName = null,
        string? containerId = null,
        int limit = 50);

    Task<int> GetActiveOperationsCountAsync();

    Task<int> ClearAllOperationsAsync();

    Task SendPullProgressAsync(UpdateProgressEvent progress);
}
