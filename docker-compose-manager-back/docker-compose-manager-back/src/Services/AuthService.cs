using Microsoft.EntityFrameworkCore;
using docker_compose_manager_back.Data;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.DTOs;
using DockerComposeManager.Services.Security;

namespace docker_compose_manager_back.Services;

public class AuthService
{
    private readonly AppDbContext _context;
    private readonly JwtTokenService _jwtService;
    private readonly IConfiguration _configuration;
    private readonly IPasswordHasher _passwordHasher;

    public AuthService(
        AppDbContext context,
        JwtTokenService jwtService,
        IConfiguration configuration,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _jwtService = jwtService;
        _configuration = configuration;
        _passwordHasher = passwordHasher;
    }

    public async Task<(bool Success, LoginResponse? Response, string? Error)> LoginAsync(LoginRequest request, string ipAddress, string deviceInfo)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user == null || !user.IsEnabled)
        {
            return (false, null, "Invalid credentials");
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return (false, null, "Invalid credentials");
        }

        // Generate tokens
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        // Calculate refresh token expiration based on RememberMe
        var refreshExpirationDays = request.RememberMe
            ? int.Parse(_configuration["Jwt:RefreshExpirationDaysExtended"] ?? "30")
            : int.Parse(_configuration["Jwt:RefreshExpirationDays"] ?? "1");
        var expiresAt = DateTime.UtcNow.AddDays(refreshExpirationDays);

        // Create session
        var session = new Session
        {
            UserId = user.Id,
            RefreshToken = refreshToken,
            DeviceInfo = deviceInfo,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            LastUsedAt = DateTime.UtcNow
        };

        _context.Sessions.Add(session);

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var response = new LoginResponse(
            accessToken,
            refreshToken,
            user.Username,
            user.Role?.Name ?? "user",
            user.MustChangePassword
        );

        return (true, response, null);
    }

    public async Task<(bool Success, LoginResponse? Response, string? Error)> RefreshTokenAsync(string refreshToken, string ipAddress)
    {
        var session = await _context.Sessions
            .Include(s => s.User)
                .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(s => s.RefreshToken == refreshToken);

        if (session == null || session.ExpiresAt < DateTime.UtcNow || !session.User.IsEnabled)
        {
            return (false, null, "Invalid or expired refresh token");
        }

        // Update session
        session.LastUsedAt = DateTime.UtcNow;

        // Generate new tokens
        var accessToken = _jwtService.GenerateAccessToken(session.User);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        // Update refresh token in session
        session.RefreshToken = newRefreshToken;

        await _context.SaveChangesAsync();

        var response = new LoginResponse(
            accessToken,
            newRefreshToken,
            session.User.Username,
            session.User.Role?.Name ?? "user",
            session.User.MustChangePassword
        );

        return (true, response, null);
    }

    public async Task<bool> LogoutAsync(string refreshToken)
    {
        var session = await _context.Sessions.FirstOrDefaultAsync(s => s.RefreshToken == refreshToken);

        if (session != null)
        {
            _context.Sessions.Remove(session);
            await _context.SaveChangesAsync();
            return true;
        }

        return false;
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var user = await _context.Users.FindAsync(userId);

        if (user == null || !_passwordHasher.VerifyPassword(currentPassword, user.PasswordHash))
        {
            return false;
        }

        user.PasswordHash = _passwordHasher.HashPassword(newPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        // Invalidate all sessions for security
        var sessions = await _context.Sessions.Where(s => s.UserId == userId).ToListAsync();
        _context.Sessions.RemoveRange(sessions);

        await _context.SaveChangesAsync();
        return true;
    }
}
