using docker_compose_manager_back.Data;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Serilog.Core;
using Serilog.Events;

namespace docker_compose_manager_back.Tests.Services;

public class LogLevelServiceTests
{
    private static (LogLevelService service, LoggingLevelSwitch levelSwitch, IServiceProvider provider) CreateService(
        LogEventLevel initial = LogEventLevel.Information)
    {
        string dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
        ServiceProvider provider = services.BuildServiceProvider();

        var levelSwitch = new LoggingLevelSwitch(initial);
        var logger = new Mock<ILogger<LogLevelService>>();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var service = new LogLevelService(levelSwitch, scopeFactory, logger.Object);
        return (service, levelSwitch, provider);
    }

    [Fact]
    public async Task SetLevelAsync_ValidLevel_UpdatesSwitchAndPersists()
    {
        (LogLevelService service, LoggingLevelSwitch levelSwitch, IServiceProvider provider) = CreateService();

        await service.SetLevelAsync("Warning");

        levelSwitch.MinimumLevel.Should().Be(LogEventLevel.Warning);

        using IServiceScope scope = provider.CreateScope();
        AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        AppSetting? setting = await context.AppSettings.FirstOrDefaultAsync(s => s.Key == LogLevelService.SettingKey);
        setting.Should().NotBeNull();
        setting!.Value.Should().Be("Warning");
    }

    [Fact]
    public async Task SetLevelAsync_IsCaseInsensitive()
    {
        (LogLevelService service, LoggingLevelSwitch levelSwitch, _) = CreateService();

        await service.SetLevelAsync("debug");

        levelSwitch.MinimumLevel.Should().Be(LogEventLevel.Debug);
    }

    [Fact]
    public async Task SetLevelAsync_InvalidLevel_ThrowsAndLeavesSwitchUnchanged()
    {
        (LogLevelService service, LoggingLevelSwitch levelSwitch, _) = CreateService(LogEventLevel.Information);

        Func<Task> act = () => service.SetLevelAsync("NotALevel");

        await act.Should().ThrowAsync<ArgumentException>();
        levelSwitch.MinimumLevel.Should().Be(LogEventLevel.Information);
    }

    [Fact]
    public async Task InitializeFromDatabaseAsync_AppliesPersistedValue()
    {
        (LogLevelService service, LoggingLevelSwitch levelSwitch, IServiceProvider provider) = CreateService(LogEventLevel.Information);

        using (IServiceScope scope = provider.CreateScope())
        {
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.AppSettings.Add(new AppSetting { Key = LogLevelService.SettingKey, Value = "Error" });
            await context.SaveChangesAsync();
        }

        await service.InitializeFromDatabaseAsync();

        levelSwitch.MinimumLevel.Should().Be(LogEventLevel.Error);
    }

    [Fact]
    public async Task InitializeFromDatabaseAsync_NoPersistedValue_KeepsDefault()
    {
        (LogLevelService service, LoggingLevelSwitch levelSwitch, _) = CreateService(LogEventLevel.Information);

        await service.InitializeFromDatabaseAsync();

        levelSwitch.MinimumLevel.Should().Be(LogEventLevel.Information);
    }

    [Fact]
    public void GetAvailableLevels_ReturnsAllSixSerilogLevels()
    {
        LogLevelService.GetAvailableLevels().Should()
            .Equal("Verbose", "Debug", "Information", "Warning", "Error", "Fatal");
    }
}
