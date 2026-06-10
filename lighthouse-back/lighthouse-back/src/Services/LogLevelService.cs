using Lighthouse.Data;
using Lighthouse.Models;
using Microsoft.EntityFrameworkCore;
using Serilog.Core;
using Serilog.Events;

namespace Lighthouse.Services;

/// <summary>
/// Manages the application's minimum log level at runtime.
/// Backed by a Serilog <see cref="LoggingLevelSwitch"/> so changes take effect
/// immediately (no restart) and persisted in the AppSettings table so they
/// survive restarts.
/// </summary>
public class LogLevelService
{
    /// <summary>AppSettings key under which the chosen level is persisted.</summary>
    public const string SettingKey = "Logging:MinimumLevel";

    /// <summary>Allowed level names exposed to the UI (Serilog naming).</summary>
    private static readonly string[] AllowedLevels =
    [
        nameof(LogEventLevel.Verbose),
        nameof(LogEventLevel.Debug),
        nameof(LogEventLevel.Information),
        nameof(LogEventLevel.Warning),
        nameof(LogEventLevel.Error),
        nameof(LogEventLevel.Fatal),
    ];

    private readonly LoggingLevelSwitch _levelSwitch;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LogLevelService> _logger;

    public LogLevelService(
        LoggingLevelSwitch levelSwitch,
        IServiceScopeFactory scopeFactory,
        ILogger<LogLevelService> logger)
    {
        _levelSwitch = levelSwitch;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Current minimum level as a Serilog level name.</summary>
    public string GetCurrentLevel() => _levelSwitch.MinimumLevel.ToString();

    /// <summary>Level names available for selection.</summary>
    public static IReadOnlyList<string> GetAvailableLevels() => AllowedLevels;

    /// <summary>
    /// Validates the supplied level name (case-insensitive) against the allowed set.
    /// </summary>
    public static bool TryParseLevel(string? value, out LogEventLevel level)
    {
        level = LogEventLevel.Information;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string? match = AllowedLevels.FirstOrDefault(
            l => string.Equals(l, value, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return false;
        }

        level = Enum.Parse<LogEventLevel>(match);
        return true;
    }

    /// <summary>
    /// Applies the new level immediately and persists it. Throws
    /// <see cref="ArgumentException"/> if the level name is not valid.
    /// </summary>
    public async Task SetLevelAsync(string value, CancellationToken cancellationToken = default)
    {
        if (!TryParseLevel(value, out LogEventLevel level))
        {
            throw new ArgumentException(
                $"Invalid log level '{value}'. Allowed values: {string.Join(", ", AllowedLevels)}.",
                nameof(value));
        }

        // Effective immediately for the whole Serilog pipeline.
        _levelSwitch.MinimumLevel = level;

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        AppSetting? setting = await context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == SettingKey, cancellationToken);

        if (setting is null)
        {
            context.AppSettings.Add(new AppSetting
            {
                Key = SettingKey,
                Value = level.ToString(),
                Description = "Minimum log level produced by the application",
            });
        }
        else
        {
            setting.Value = level.ToString();
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Application log level changed to {LogLevel}", level);
    }

    /// <summary>
    /// Reads the persisted level from the database (if any) and applies it to the
    /// switch. Called once at startup so the UI choice overrides the env/appsettings
    /// default. If no value is stored, the switch keeps its configured default.
    /// </summary>
    public async Task InitializeFromDatabaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            AppSetting? setting = await context.AppSettings
                .FirstOrDefaultAsync(s => s.Key == SettingKey, cancellationToken);

            if (setting is not null && TryParseLevel(setting.Value, out LogEventLevel level))
            {
                _levelSwitch.MinimumLevel = level;
                _logger.LogInformation("Applied persisted log level from database: {LogLevel}", level);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize log level from database; using configured default");
        }
    }
}
