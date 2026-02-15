using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Models;

namespace docker_compose_manager_back.Extensions;

/// <summary>
/// Extension methods for mapping domain models to DTOs
/// </summary>
public static class DtoMappingExtensions
{
    /// <summary>
    /// Convert User entity to UserDto
    /// </summary>
    public static UserDto ToDto(this User user)
    {
        return new UserDto(
            user.Id,
            user.Username,
            user.Email,
            user.Role?.Name ?? "user",
            user.IsEnabled,
            user.MustChangePassword,
            user.MustAddEmail,
            user.CreatedAt,
            user.LastLoginAt
        );
    }

    /// <summary>
    /// Convert AuditLog entity to AuditLogDto
    /// </summary>
    public static AuditLogDto ToDto(this AuditLog log)
    {
        return new AuditLogDto(
            log.Id,
            log.UserId,
            log.User?.Username,
            log.Action,
            log.ResourceType,
            log.ResourceId,
            log.Details,
            log.IpAddress,
            log.UserAgent,
            log.Timestamp,
            log.Success,
            log.ErrorMessage
        );
    }

    /// <summary>
    /// Convert AuditLog entity to AuditLogDetailsDto (with full details)
    /// </summary>
    public static AuditLogDetailsDto ToDetailsDto(this AuditLog log)
    {
        return new AuditLogDetailsDto(
            log.Id,
            log.UserId,
            log.User?.Username,
            log.Action,
            log.ResourceType,
            log.ResourceId,
            log.Details,
            log.BeforeState,
            log.AfterState,
            log.IpAddress,
            log.UserAgent,
            log.Timestamp,
            log.Success,
            log.ErrorMessage
        );
    }

    /// <summary>
    /// Convert Operation entity to OperationDto
    /// </summary>
    public static OperationDto ToDto(this Operation operation)
    {
        return new OperationDto(
            operation.OperationId,
            operation.Type,
            operation.Status,
            operation.Progress,
            operation.ProjectName,
            operation.ProjectPath,
            operation.User?.Username,
            operation.StartedAt,
            operation.CompletedAt,
            operation.ErrorMessage
        );
    }

    /// <summary>
    /// Convert list of Users to list of UserDtos
    /// </summary>
    public static List<UserDto> ToDtoList(this IEnumerable<User> users)
    {
        return users.Select(u => u.ToDto()).ToList();
    }

    /// <summary>
    /// Convert list of AuditLogs to list of AuditLogDtos
    /// </summary>
    public static List<AuditLogDto> ToDtoList(this IEnumerable<AuditLog> logs)
    {
        return logs.Select(l => l.ToDto()).ToList();
    }

    /// <summary>
    /// Convert list of Operations to list of OperationDtos
    /// </summary>
    public static List<OperationDto> ToDtoList(this IEnumerable<Operation> operations)
    {
        return operations.Select(o => o.ToDto()).ToList();
    }
}
