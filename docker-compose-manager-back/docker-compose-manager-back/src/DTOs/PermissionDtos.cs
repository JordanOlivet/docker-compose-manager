using docker_compose_manager_back.Models;

namespace docker_compose_manager_back.DTOs;

// User Group DTOs

public class UserGroupDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int MemberCount { get; set; }
    public List<int> MemberIds { get; set; } = new();
}

public class CreateUserGroupRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public List<int> MemberIds { get; set; } = new();
    public List<ResourcePermissionInput>? Permissions { get; set; }
}

public class UpdateUserGroupRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public List<ResourcePermissionInput>? Permissions { get; set; }
}

public class AddUserToGroupRequest
{
    public required int UserId { get; set; }
}

public class RemoveUserFromGroupRequest
{
    public required int UserId { get; set; }
}

// Permission DTOs

public class ResourcePermissionDto
{
    public int Id { get; set; }
    public ResourceType ResourceType { get; set; }
    public required string ResourceName { get; set; }
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public int? UserGroupId { get; set; }
    public string? UserGroupName { get; set; }
    public PermissionFlags Permissions { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreatePermissionRequest
{
    public required ResourceType ResourceType { get; set; }
    public required string ResourceName { get; set; }
    public int? UserId { get; set; }
    public int? UserGroupId { get; set; }
    public required PermissionFlags Permissions { get; set; }
}

public class UpdatePermissionRequest
{
    public required PermissionFlags Permissions { get; set; }
}

public class BulkCreatePermissionsRequest
{
    public required List<CreatePermissionRequest> Permissions { get; set; }
}

public class CheckPermissionRequest
{
    public required ResourceType ResourceType { get; set; }
    public required string ResourceName { get; set; }
    public required PermissionFlags RequiredPermission { get; set; }
}

public class CheckPermissionResponse
{
    public bool HasPermission { get; set; }
    public PermissionFlags UserPermissions { get; set; }
}

public class UserPermissionsResponse
{
    public int UserId { get; set; }
    public bool IsAdmin { get; set; }
    public List<ResourcePermissionDto> DirectPermissions { get; set; } = new();
    public List<ResourcePermissionDto> GroupPermissions { get; set; } = new();
}

// Input DTO for creating/updating permissions within user/group requests
public record ResourcePermissionInput(
    ResourceType ResourceType,
    string ResourceName,
    PermissionFlags Permissions
);

// Request for copying permissions from one user/group to another
public class CopyPermissionsRequest
{
    public int? SourceUserId { get; set; }
    public int? SourceUserGroupId { get; set; }
    public int? TargetUserId { get; set; }
    public int? TargetUserGroupId { get; set; }
}
