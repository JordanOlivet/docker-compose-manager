using Lighthouse.Data;
using Lighthouse.Models;
using Lighthouse.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lighthouse.Tests.Services;

public class PermissionServiceTests
{
    private static AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new AppDbContext(options);

        ctx.Roles.AddRange(
            new Role { Id = 1, Name = "admin", Permissions = "[]" },
            new Role { Id = 2, Name = "user", Permissions = "[]" });
        ctx.Users.AddRange(
            new User { Id = 1, Username = "admin", PasswordHash = "h", RoleId = 1, SecurityStamp = "a" },
            new User { Id = 2, Username = "bob", PasswordHash = "h", RoleId = 2, SecurityStamp = "b" });
        ctx.SaveChanges();
        return ctx;
    }

    private static PermissionService NewService(AppDbContext ctx) =>
        new(ctx, NullLogger<PermissionService>.Instance);

    [Fact]
    public async Task IsAdmin_TrueForAdmin_FalseForUser()
    {
        using var ctx = NewContext();
        var svc = NewService(ctx);
        (await svc.IsAdminAsync(1)).Should().BeTrue();
        (await svc.IsAdminAsync(2)).Should().BeFalse();
    }

    [Fact]
    public async Task Admin_HasAllPermissions_WithoutExplicitGrants()
    {
        using var ctx = NewContext();
        var svc = NewService(ctx);
        (await svc.HasPermissionAsync(1, ResourceType.ComposeProject, "anything", PermissionFlags.Start))
            .Should().BeTrue();
    }

    [Fact]
    public async Task DirectPermission_GrantsOnlyItsFlags()
    {
        using var ctx = NewContext();
        ctx.ResourcePermissions.Add(new ResourcePermission
        {
            UserId = 2,
            ResourceType = ResourceType.ComposeProject,
            ResourceName = "proj1",
            Permissions = PermissionFlags.View,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
        var svc = NewService(ctx);

        (await svc.HasPermissionAsync(2, ResourceType.ComposeProject, "proj1", PermissionFlags.View)).Should().BeTrue();
        (await svc.HasPermissionAsync(2, ResourceType.ComposeProject, "proj1", PermissionFlags.Start)).Should().BeFalse();
        (await svc.HasPermissionAsync(2, ResourceType.ComposeProject, "other", PermissionFlags.View)).Should().BeFalse();
    }

    [Fact]
    public async Task GroupPermission_IsInherited()
    {
        using var ctx = NewContext();
        ctx.UserGroups.Add(new UserGroup { Id = 10, Name = "team" });
        ctx.UserGroupMemberships.Add(new UserGroupMembership { UserId = 2, UserGroupId = 10 });
        ctx.ResourcePermissions.Add(new ResourcePermission
        {
            UserGroupId = 10,
            ResourceType = ResourceType.ComposeProject,
            ResourceName = "shared",
            Permissions = PermissionFlags.View | PermissionFlags.Start,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
        var svc = NewService(ctx);

        (await svc.HasPermissionAsync(2, ResourceType.ComposeProject, "shared", PermissionFlags.Start)).Should().BeTrue();
    }

    [Fact]
    public async Task FilterAuthorizedResources_ReturnsOnlyViewable()
    {
        using var ctx = NewContext();
        ctx.ResourcePermissions.Add(new ResourcePermission
        {
            UserId = 2,
            ResourceType = ResourceType.ComposeProject,
            ResourceName = "a",
            Permissions = PermissionFlags.View,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
        var svc = NewService(ctx);

        var result = await svc.FilterAuthorizedResourcesAsync(2, ResourceType.ComposeProject, new[] { "a", "b", "c" });
        result.Should().BeEquivalentTo(new[] { "a" });

        // Admin sees everything passed in.
        var adminResult = await svc.FilterAuthorizedResourcesAsync(1, ResourceType.ComposeProject, new[] { "a", "b", "c" });
        adminResult.Should().BeEquivalentTo(new[] { "a", "b", "c" });
    }

    [Fact]
    public async Task FilterAuthorizedContainers_InheritsProjectPermission()
    {
        using var ctx = NewContext();
        ctx.ResourcePermissions.Add(new ResourcePermission
        {
            UserId = 2,
            ResourceType = ResourceType.ComposeProject,
            ResourceName = "p1",
            Permissions = PermissionFlags.View,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();
        var svc = NewService(ctx);

        var containers = new List<(string, string?)>
        {
            ("c1", "p1"),   // authorized via project p1
            ("c2", "p2"),   // no permission on p2
            ("c3", null)    // no project, no direct permission
        };

        var result = await svc.FilterAuthorizedContainersAsync(2, containers);
        result.Should().BeEquivalentTo(new[] { "c1" });
    }
}
