using Lighthouse.BackgroundServices;
using Lighthouse.Data;
using Lighthouse.Models;
using Lighthouse.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Lighthouse.Tests.Services;

public class AuditWriterTests
{
    private static AuditLog Entry(string action) =>
        new() { Action = action, IpAddress = "127.0.0.1", Timestamp = DateTime.UtcNow };

    [Fact]
    public async Task Writer_PersistsAllEnqueuedEntries()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var queue = new AuditQueue();
        queue.Enqueue(Entry("a1"));
        queue.Enqueue(Entry("a2"));
        queue.Enqueue(Entry("a3"));
        queue.Complete(); // let the drain loop finish once buffered entries are read

        var writer = new AuditWriterBackgroundService(
            queue, scopeFactory, NullLogger<AuditWriterBackgroundService>.Instance);

        await writer.StartAsync(CancellationToken.None);
        await writer.ExecuteTask!; // completes after the completed queue is fully drained

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actions = await db.AuditLogs.Select(a => a.Action).OrderBy(a => a).ToListAsync();
        actions.Should().BeEquivalentTo(new[] { "a1", "a2", "a3" });
    }

    [Fact]
    public async Task AuditService_LogAction_EnqueuesWithoutWritingToDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var ctx = new AppDbContext(options);

        var queue = new Mock<IAuditQueue>();
        var service = new AuditService(ctx, queue.Object, NullLogger<AuditService>.Instance);

        await service.LogActionAsync(userId: 1, action: "user.login", ipAddress: "10.0.0.1",
            details: "logged in");

        queue.Verify(q => q.Enqueue(It.Is<AuditLog>(a =>
            a.Action == "user.login" && a.UserId == 1 && a.IpAddress == "10.0.0.1")), Times.Once);
        // Nothing is written to the database on the request path.
        (await ctx.AuditLogs.CountAsync()).Should().Be(0);
    }
}
