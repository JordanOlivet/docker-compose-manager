using Lighthouse.Data;
using Lighthouse.DTOs;
using Lighthouse.Models;
using Lighthouse.Services;
using DockerComposeManager.Services.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Lighthouse.Tests.Services;

public class AuthServiceTests
{
    private static AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new AppDbContext(options);
        ctx.Roles.Add(new Role { Id = 1, Name = "user", Permissions = "[]" });
        ctx.Users.Add(new User
        {
            Id = 1,
            Username = "alice",
            PasswordHash = "stored-hash",
            RoleId = 1,
            IsEnabled = true,
            SecurityStamp = "stamp-1"
        });
        ctx.SaveChanges();
        return ctx;
    }

    private static AuthService NewService(AppDbContext ctx, IMemoryCache? cache = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-key-at-least-32-characters-long",
                ["Jwt:Issuer"] = "lighthouse",
                ["Jwt:Audience"] = "lighthouse-client",
                ["Jwt:ExpirationMinutes"] = "15",
                ["Jwt:RefreshExpirationDays"] = "1"
            })
            .Build();

        var jwt = new JwtTokenService(config);

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.VerifyPassword("correct", "stored-hash")).Returns(true);
        hasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns<string>(p => "hashed:" + p);

        return new AuthService(
            ctx, jwt, config, hasher.Object,
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task Login_StoresHashedRefreshToken_NotRaw()
    {
        using var ctx = NewContext();
        var svc = NewService(ctx);

        var (success, response, _, _) = await svc.LoginAsync(
            new LoginRequest("alice", "correct"), "ip", "device");

        success.Should().BeTrue();
        var raw = response!.RefreshToken;
        raw.Should().NotBeNullOrEmpty();

        var stored = await ctx.Sessions.SingleAsync();
        stored.RefreshToken.Should().NotBe(raw, "the raw token must not be stored in clear");
        stored.FamilyId.Should().NotBeNullOrEmpty();
        stored.IsUsed.Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndConsumesOld()
    {
        using var ctx = NewContext();
        var svc = NewService(ctx);

        var login = (await svc.LoginAsync(new LoginRequest("alice", "correct"), "ip", "d")).Response!;
        var (success, refreshed, _, _) = await svc.RefreshTokenAsync(login.RefreshToken, "ip");

        success.Should().BeTrue();
        refreshed!.RefreshToken.Should().NotBe(login.RefreshToken);

        // Old token row is consumed; a new one exists in the same family.
        var families = await ctx.Sessions.Select(s => s.FamilyId).Distinct().ToListAsync();
        families.Should().HaveCount(1);
        (await ctx.Sessions.CountAsync(s => s.IsUsed)).Should().Be(1);
    }

    [Fact]
    public async Task Refresh_ReusedOldToken_RevokesEntireFamily()
    {
        using var ctx = NewContext();
        var svc = NewService(ctx);

        var login = (await svc.LoginAsync(new LoginRequest("alice", "correct"), "ip", "d")).Response!;
        var rotated = (await svc.RefreshTokenAsync(login.RefreshToken, "ip")).Response!;

        // Replay the ORIGINAL (now consumed) token -> reuse detected.
        var (reuseSuccess, _, _, _) = await svc.RefreshTokenAsync(login.RefreshToken, "ip");
        reuseSuccess.Should().BeFalse();

        // The whole family is revoked: the legitimately rotated token no longer works.
        var (afterRevoke, _, _, _) = await svc.RefreshTokenAsync(rotated.RefreshToken, "ip");
        afterRevoke.Should().BeFalse();

        (await ctx.Sessions.CountAsync(s => s.RevokedAt == null)).Should().Be(0);
    }

    [Fact]
    public async Task Logout_RevokesFamily_TokenNoLongerRefreshable()
    {
        using var ctx = NewContext();
        var svc = NewService(ctx);

        var login = (await svc.LoginAsync(new LoginRequest("alice", "correct"), "ip", "d")).Response!;

        (await svc.LogoutAsync(login.RefreshToken)).Should().BeTrue();

        var (success, _, _, _) = await svc.RefreshTokenAsync(login.RefreshToken, "ip");
        success.Should().BeFalse();
    }

    [Fact]
    public async Task ChangePassword_RotatesSecurityStamp_AndInvalidatesCache()
    {
        using var ctx = NewContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(Lighthouse.Services.Security.SecurityStampCache.Key(1), "stale");
        var svc = NewService(ctx, cache);

        var (success, _, _, _) = await svc.ChangePasswordAsync(1, "correct", "newpass", "ip", "ua");

        success.Should().BeTrue();
        var user = await ctx.Users.SingleAsync();
        user.SecurityStamp.Should().NotBe("stamp-1");
        cache.TryGetValue(Lighthouse.Services.Security.SecurityStampCache.Key(1), out _).Should().BeFalse();
    }
}
