using docker_compose_manager_back.Data;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Extensions;
using docker_compose_manager_back.Models;
using DockerComposeManager.Services.Security;
using Microsoft.EntityFrameworkCore;

namespace docker_compose_manager_back.Services;

public interface IUserService
{
    Task<List<UserDto>> GetAllUsersAsync();
    Task<PaginatedResponse<UserDto>> GetAllUsersAsync(
        int pageNumber,
        int pageSize,
        string? search,
        string? role,
        bool? isEnabled,
        string? sortBy,
        bool sortDescending);
    Task<UserDto?> GetUserByIdAsync(int id);
    Task<UserDto> CreateUserAsync(CreateUserRequest request);
    Task<UserDto> UpdateUserAsync(int id, UpdateUserRequest request);
    Task DeleteUserAsync(int id);
    Task<UserDto> EnableUserAsync(int id);
    Task<UserDto> DisableUserAsync(int id);
}

public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly ILogger<UserService> _logger;
    private readonly IAuditService _auditService;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(
        AppDbContext context,
        ILogger<UserService> logger,
        IAuditService auditService,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _logger = logger;
        _auditService = auditService;
        _passwordHasher = passwordHasher;
    }

    public async Task<List<UserDto>> GetAllUsersAsync()
    {
        var users = await _context.Users
            .Include(u => u.Role)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        return users.ToDtoList();
    }

    public async Task<PaginatedResponse<UserDto>> GetAllUsersAsync(
        int pageNumber,
        int pageSize,
        string? search,
        string? role,
        bool? isEnabled,
        string? sortBy,
        bool sortDescending)
    {
        // Start with base query
        IQueryable<User> query = _context.Users.Include(u => u.Role);

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            string searchLower = search.ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(searchLower));
        }

        // Apply role filter
        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role != null && u.Role.Name == role);
        }

        // Apply enabled status filter
        if (isEnabled.HasValue)
        {
            query = query.Where(u => u.IsEnabled == isEnabled.Value);
        }

        // Apply sorting
        query = sortBy?.ToLower() switch
        {
            "username" => sortDescending
                ? query.OrderByDescending(u => u.Username)
                : query.OrderBy(u => u.Username),
            "createdat" => sortDescending
                ? query.OrderByDescending(u => u.CreatedAt)
                : query.OrderBy(u => u.CreatedAt),
            "lastloginat" => sortDescending
                ? query.OrderByDescending(u => u.LastLoginAt)
                : query.OrderBy(u => u.LastLoginAt),
            _ => sortDescending
                ? query.OrderByDescending(u => u.Username)
                : query.OrderBy(u => u.Username)
        };

        // Get total count before pagination
        int totalItems = await query.CountAsync();

        // Apply pagination
        List<User> users = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Map to DTOs
        List<UserDto> userDtos = users.ToDtoList();

        // Calculate pagination metadata
        int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        bool hasNext = pageNumber < totalPages;
        bool hasPrevious = pageNumber > 1;

        return new PaginatedResponse<UserDto>(
            userDtos,
            pageNumber,
            pageSize,
            totalPages,
            totalItems,
            hasNext,
            hasPrevious
        );
    }

    public async Task<UserDto?> GetUserByIdAsync(int id)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        return user?.ToDto();
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
    {
        // Check if username already exists
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (existingUser != null)
            throw new InvalidOperationException($"User with username '{request.Username}' already exists");

        // Get role (case-insensitive)
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name.ToLower() == request.Role.ToLower());
        if (role == null)
            throw new InvalidOperationException($"Role '{request.Role}' not found");

        // Hash password
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var user = new User
        {
            Username = request.Username,
            PasswordHash = passwordHash,
            RoleId = role.Id,
            IsEnabled = true,
            MustChangePassword = true, // Force password change on first login
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Create permissions if provided
        if (request.Permissions != null && request.Permissions.Any())
        {
            foreach (var permInput in request.Permissions)
            {
                // Check for duplicate permissions
                var existingPerm = await _context.ResourcePermissions
                    .FirstOrDefaultAsync(p =>
                        p.UserId == user.Id &&
                        p.ResourceType == permInput.ResourceType &&
                        p.ResourceName == permInput.ResourceName);

                if (existingPerm == null)
                {
                    var permission = new ResourcePermission
                    {
                        UserId = user.Id,
                        ResourceType = permInput.ResourceType,
                        ResourceName = permInput.ResourceName,
                        Permissions = permInput.Permissions,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.ResourcePermissions.Add(permission);
                }
            }
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("User {Username} created with role {Role} and {PermCount} permissions",
            user.Username, role.Name, request.Permissions?.Count ?? 0);

        // Audit log
        await _auditService.LogAsync(
            userId: null, // System action, no specific user
            action: "UserCreated",
            resourceType: "User",
            resourceId: user.Id.ToString(),
            details: $"User '{user.Username}' created with role '{role.Name}' and {request.Permissions?.Count ?? 0} permissions",
            ipAddress: null,
            userAgent: null
        );

        return user.ToDto();
    }

    public async Task<UserDto> UpdateUserAsync(int id, UpdateUserRequest request)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            throw new InvalidOperationException($"User with ID {id} not found");

        var changes = new List<string>();

        // Update username if provided
        if (request.Username != null && request.Username != user.Username)
        {
            // Check if username is already taken
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower() && u.Id != id);
            if (existingUser != null)
                throw new InvalidOperationException($"Username '{request.Username}' is already taken");

            changes.Add($"Username changed from '{user.Username}' to '{request.Username}'");
            user.Username = request.Username;
        }

        // Update role if provided (case-insensitive)
        if (request.Role != null && request.Role != user.Role?.Name)
        {
            var newRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name.ToLower() == request.Role.ToLower());
            if (newRole == null)
                throw new InvalidOperationException($"Role '{request.Role}' not found");

            changes.Add($"Role changed from '{user.Role?.Name}' to '{newRole.Name}'");
            user.RoleId = newRole.Id;
        }

        // Update email if provided
        if (request.Email != null && request.Email != user.Email)
        {
            // Check if email is already in use by another user
            var existingEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email && u.Id != id);
            if (existingEmail != null)
                throw new InvalidOperationException($"Email '{request.Email}' is already in use");

            changes.Add($"Email changed from '{user.Email ?? "none"}' to '{request.Email}'");
            user.Email = request.Email;
        }

        // Update mustAddEmail flag if provided
        if (request.MustAddEmail.HasValue && request.MustAddEmail.Value != user.MustAddEmail)
        {
            changes.Add($"MustAddEmail changed from {user.MustAddEmail} to {request.MustAddEmail.Value}");
            user.MustAddEmail = request.MustAddEmail.Value;
        }

        // Update enabled status if provided
        if (request.IsEnabled.HasValue && request.IsEnabled.Value != user.IsEnabled)
        {
            changes.Add($"IsEnabled changed from {user.IsEnabled} to {request.IsEnabled.Value}");
            user.IsEnabled = request.IsEnabled.Value;

            // If disabling user, invalidate all sessions
            if (!request.IsEnabled.Value)
            {
                var sessions = await _context.Sessions.Where(s => s.UserId == id).ToListAsync();
                _context.Sessions.RemoveRange(sessions);
                changes.Add("All sessions invalidated");
            }
        }

        // Update password if provided
        if (request.NewPassword != null)
        {
            user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
            user.MustChangePassword = false;
            changes.Add("Password updated");

            // Invalidate all sessions on password change
            var sessions = await _context.Sessions.Where(s => s.UserId == id).ToListAsync();
            _context.Sessions.RemoveRange(sessions);
            changes.Add("All sessions invalidated due to password change");
        }

        // Update permissions if provided
        if (request.Permissions != null)
        {
            // Remove all existing user permissions
            var existingPermissions = await _context.ResourcePermissions
                .Where(p => p.UserId == id)
                .ToListAsync();
            _context.ResourcePermissions.RemoveRange(existingPermissions);

            // Add new permissions
            foreach (var permInput in request.Permissions)
            {
                var permission = new ResourcePermission
                {
                    UserId = id,
                    ResourceType = permInput.ResourceType,
                    ResourceName = permInput.ResourceName,
                    Permissions = permInput.Permissions,
                    CreatedAt = DateTime.UtcNow
                };
                _context.ResourcePermissions.Add(permission);
            }

            changes.Add($"Permissions updated ({request.Permissions.Count} permissions set)");
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Username} (ID: {UserId}) updated: {Changes}",
            user.Username, user.Id, string.Join(", ", changes));

        // Audit log
        await _auditService.LogAsync(
            userId: null,
            action: "UserUpdated",
            resourceType: "User",
            resourceId: user.Id.ToString(),
            details: $"User '{user.Username}' updated: {string.Join(", ", changes)}",
            ipAddress: null,
            userAgent: null
        );

        // Reload role after changes
        await _context.Entry(user).Reference(u => u.Role).LoadAsync();

        return user.ToDto();
    }

    public async Task DeleteUserAsync(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            throw new InvalidOperationException($"User with ID {id} not found");

        // Prevent deletion of last admin
        var isAdmin = await _context.Users
            .Include(u => u.Role)
            .Where(u => u.Id == id && u.Role != null && u.Role.Name == "admin")
            .AnyAsync();

        if (isAdmin)
        {
            var adminCount = await _context.Users
                .Include(u => u.Role)
                .CountAsync(u => u.Role != null && u.Role.Name == "admin");

            if (adminCount <= 1)
                throw new InvalidOperationException("Cannot delete the last admin user");
        }

        var username = user.Username;

        // Delete related sessions
        var sessions = await _context.Sessions.Where(s => s.UserId == id).ToListAsync();
        _context.Sessions.RemoveRange(sessions);

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Username} (ID: {UserId}) deleted", username, id);

        // Audit log
        await _auditService.LogAsync(
            userId: null,
            action: "UserDeleted",
            resourceType: "User",
            resourceId: id.ToString(),
            details: $"User '{username}' deleted",
            ipAddress: null,
            userAgent: null
        );
    }

    public async Task<UserDto> EnableUserAsync(int id)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            throw new InvalidOperationException($"User with ID {id} not found");

        if (user.IsEnabled)
            throw new InvalidOperationException($"User '{user.Username}' is already enabled");

        user.IsEnabled = true;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Username} (ID: {UserId}) enabled", user.Username, user.Id);

        await _auditService.LogAsync(
            userId: null,
            action: "UserEnabled",
            resourceType: "User",
            resourceId: user.Id.ToString(),
            details: $"User '{user.Username}' enabled",
            ipAddress: null,
            userAgent: null
        );

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

    public async Task<UserDto> DisableUserAsync(int id)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            throw new InvalidOperationException($"User with ID {id} not found");

        if (!user.IsEnabled)
            throw new InvalidOperationException($"User '{user.Username}' is already disabled");

        // Prevent disabling last admin
        var isAdmin = user.Role?.Name == "admin";
        if (isAdmin)
        {
            var enabledAdminCount = await _context.Users
                .Include(u => u.Role)
                .CountAsync(u => u.IsEnabled && u.Role != null && u.Role.Name == "admin");

            if (enabledAdminCount <= 1)
                throw new InvalidOperationException("Cannot disable the last enabled admin user");
        }

        user.IsEnabled = false;

        // Invalidate all sessions
        var sessions = await _context.Sessions.Where(s => s.UserId == id).ToListAsync();
        _context.Sessions.RemoveRange(sessions);

        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Username} (ID: {UserId}) disabled", user.Username, user.Id);

        await _auditService.LogAsync(
            userId: null,
            action: "UserDisabled",
            resourceType: "User",
            resourceId: user.Id.ToString(),
            details: $"User '{user.Username}' disabled",
            ipAddress: null,
            userAgent: null
        );

        return user.ToDto();
    }
}
