using docker_compose_manager_back.Data;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace docker_compose_manager_back.Tests.Services;

public class OperationServiceTests
{
    private readonly SseConnectionManagerService _sseManager;

    public OperationServiceTests()
    {
        _sseManager = new SseConnectionManagerService(new NullLogger<SseConnectionManagerService>());
    }

    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private OperationService CreateService(AppDbContext context)
    {
        return new OperationService(context, new NullLogger<OperationService>(), _sseManager);
    }

    private SseClient AddFakeSseClient()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var client = new SseClient
        {
            Response = httpContext.Response,
            ConnectionId = Guid.NewGuid().ToString(),
            CancellationToken = CancellationToken.None
        };
        _sseManager.AddClient(client);
        return client;
    }

    private static string ReadResponseBody(SseClient client)
    {
        client.Response.Body.Position = 0;
        using var reader = new StreamReader(client.Response.Body, leaveOpen: true);
        return reader.ReadToEnd();
    }

    // --- CreateOperationAsync ---

    [Fact]
    public async Task CreateOperationAsync_ValidInput_CreatesOperation()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        var operation = await service.CreateOperationAsync("compose_up", 1, "/path/to/project", "myapp");

        operation.Should().NotBeNull();
        operation.Type.Should().Be("compose_up");
        operation.UserId.Should().Be(1);
        operation.ProjectPath.Should().Be("/path/to/project");
        operation.ProjectName.Should().Be("myapp");

        var saved = await context.Operations.FirstOrDefaultAsync(o => o.OperationId == operation.OperationId);
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateOperationAsync_SetsDefaultValues()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        var operation = await service.CreateOperationAsync("compose_down", null);

        operation.Status.Should().Be(OperationStatus.Pending);
        operation.Progress.Should().Be(0);
        operation.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        operation.OperationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateOperationAsync_WithOptionalFields_SetsProjectInfo()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        var operation = await service.CreateOperationAsync("compose_up", 1, "/compose/myapp", "myapp");

        operation.ProjectPath.Should().Be("/compose/myapp");
        operation.ProjectName.Should().Be("myapp");
    }

    // --- UpdateOperationStatusAsync ---

    [Fact]
    public async Task UpdateOperationStatusAsync_ExistingOperation_UpdatesStatus()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        var operation = await service.CreateOperationAsync("compose_up", 1);

        bool result = await service.UpdateOperationStatusAsync(operation.OperationId, OperationStatus.Running);

        result.Should().BeTrue();
        var updated = await context.Operations.FirstAsync(o => o.OperationId == operation.OperationId);
        updated.Status.Should().Be(OperationStatus.Running);
    }

    [Fact]
    public async Task UpdateOperationStatusAsync_NonExistentOperation_ReturnsFalse()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        bool result = await service.UpdateOperationStatusAsync("non-existent-id", OperationStatus.Running);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateOperationStatusAsync_WithProgress_UpdatesProgress()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        var operation = await service.CreateOperationAsync("compose_up", 1);

        await service.UpdateOperationStatusAsync(operation.OperationId, OperationStatus.Running, progress: 50);

        var updated = await context.Operations.FirstAsync(o => o.OperationId == operation.OperationId);
        updated.Progress.Should().Be(50);
    }

    [Fact]
    public async Task UpdateOperationStatusAsync_CompletedStatus_SetsCompletedAt()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        var operation = await service.CreateOperationAsync("compose_up", 1);

        await service.UpdateOperationStatusAsync(operation.OperationId, OperationStatus.Completed, progress: 100);

        var updated = await context.Operations.FirstAsync(o => o.OperationId == operation.OperationId);
        updated.CompletedAt.Should().NotBeNull();
        updated.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateOperationStatusAsync_FailedStatus_SetsCompletedAt()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        var operation = await service.CreateOperationAsync("compose_up", 1);

        await service.UpdateOperationStatusAsync(operation.OperationId, OperationStatus.Failed);

        var updated = await context.Operations.FirstAsync(o => o.OperationId == operation.OperationId);
        updated.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateOperationStatusAsync_WithErrorMessage_SetsError()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        var operation = await service.CreateOperationAsync("compose_up", 1);

        await service.UpdateOperationStatusAsync(operation.OperationId, OperationStatus.Failed, errorMessage: "Build failed");

        var updated = await context.Operations.FirstAsync(o => o.OperationId == operation.OperationId);
        updated.ErrorMessage.Should().Be("Build failed");
    }

    [Fact]
    public async Task UpdateOperationStatusAsync_BroadcastsSseNotification()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        var sseClient = AddFakeSseClient();
        var operation = await service.CreateOperationAsync("compose_up", 1, projectName: "myapp");

        await service.UpdateOperationStatusAsync(operation.OperationId, OperationStatus.Running, progress: 50);

        string body = ReadResponseBody(sseClient);
        body.Should().Contain("event: OperationUpdate");
        body.Should().Contain(operation.OperationId);
        body.Should().Contain("\"status\":\"running\"");
    }

    [Fact]
    public async Task UpdateOperationStatusAsync_SseFails_StillReturnsTrue()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        var operation = await service.CreateOperationAsync("compose_up", 1);

        // Add a bad SSE client that will cause broadcast to fail for that client
        var badContext = new DefaultHttpContext();
        var closedStream = new MemoryStream();
        closedStream.Close();
        badContext.Response.Body = closedStream;
        _sseManager.AddClient(new SseClient
        {
            Response = badContext.Response,
            ConnectionId = "bad",
            CancellationToken = CancellationToken.None
        });

        bool result = await service.UpdateOperationStatusAsync(operation.OperationId, OperationStatus.Running);

        result.Should().BeTrue();
    }

    // --- AppendLogsAsync ---

    [Fact]
    public async Task AppendLogsAsync_ExistingOperation_AppendsLogs()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        var operation = await service.CreateOperationAsync("compose_up", 1);

        await service.AppendLogsAsync(operation.OperationId, "Line 1");
        await service.AppendLogsAsync(operation.OperationId, "Line 2");

        var updated = await context.Operations.FirstAsync(o => o.OperationId == operation.OperationId);
        updated.Logs.Should().Be("Line 1\nLine 2");
    }

    [Fact]
    public async Task AppendLogsAsync_EmptyExistingLogs_SetsLogs()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        var operation = await service.CreateOperationAsync("compose_up", 1);

        await service.AppendLogsAsync(operation.OperationId, "First log line");

        var updated = await context.Operations.FirstAsync(o => o.OperationId == operation.OperationId);
        updated.Logs.Should().Be("First log line");
    }

    [Fact]
    public async Task AppendLogsAsync_NonExistentOperation_ReturnsFalse()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        bool result = await service.AppendLogsAsync("non-existent-id", "some logs");

        result.Should().BeFalse();
    }

    // --- GetOperationAsync ---

    [Fact]
    public async Task GetOperationAsync_ExistingId_ReturnsOperation()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        var created = await service.CreateOperationAsync("compose_up", 1);

        var result = await service.GetOperationAsync(created.OperationId);

        result.Should().NotBeNull();
        result!.OperationId.Should().Be(created.OperationId);
        result.Type.Should().Be("compose_up");
    }

    [Fact]
    public async Task GetOperationAsync_NonExistentId_ReturnsNull()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        var result = await service.GetOperationAsync("non-existent-id");

        result.Should().BeNull();
    }

    // --- CancelOperationAsync ---

    [Fact]
    public async Task CancelOperationAsync_PendingOperation_Cancels()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        var operation = await service.CreateOperationAsync("compose_up", 1);

        bool result = await service.CancelOperationAsync(operation.OperationId);

        result.Should().BeTrue();
        var updated = await context.Operations.FirstAsync(o => o.OperationId == operation.OperationId);
        updated.Status.Should().Be(OperationStatus.Cancelled);
        updated.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelOperationAsync_RunningOperation_Cancels()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        var operation = await service.CreateOperationAsync("compose_up", 1);
        await service.UpdateOperationStatusAsync(operation.OperationId, OperationStatus.Running);

        bool result = await service.CancelOperationAsync(operation.OperationId);

        result.Should().BeTrue();
        var updated = await context.Operations.FirstAsync(o => o.OperationId == operation.OperationId);
        updated.Status.Should().Be(OperationStatus.Cancelled);
    }

    [Fact]
    public async Task CancelOperationAsync_CompletedOperation_ReturnsFalse()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        var operation = await service.CreateOperationAsync("compose_up", 1);
        await service.UpdateOperationStatusAsync(operation.OperationId, OperationStatus.Completed);

        bool result = await service.CancelOperationAsync(operation.OperationId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CancelOperationAsync_NonExistentId_ReturnsFalse()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        bool result = await service.CancelOperationAsync("non-existent-id");

        result.Should().BeFalse();
    }

    // --- ListOperationsAsync ---

    [Fact]
    public async Task ListOperationsAsync_NoFilters_ReturnsAll()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        await service.CreateOperationAsync("compose_up", 1);
        await service.CreateOperationAsync("compose_down", 1);
        await service.CreateOperationAsync("compose_build", 2);

        var result = await service.ListOperationsAsync();

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task ListOperationsAsync_FilterByStatus_ReturnsFiltered()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        var op1 = await service.CreateOperationAsync("compose_up", 1);
        await service.CreateOperationAsync("compose_down", 1);
        await service.UpdateOperationStatusAsync(op1.OperationId, OperationStatus.Running);

        var result = await service.ListOperationsAsync(status: OperationStatus.Running);

        result.Should().HaveCount(1);
        result[0].Status.Should().Be(OperationStatus.Running);
    }

    [Fact]
    public async Task ListOperationsAsync_OrderedByStartedAtDescending()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);

        var op1 = await service.CreateOperationAsync("compose_up", 1);
        await Task.Delay(50); // Ensure different timestamps
        var op2 = await service.CreateOperationAsync("compose_down", 1);

        var result = await service.ListOperationsAsync();

        result[0].OperationId.Should().Be(op2.OperationId);
        result[1].OperationId.Should().Be(op1.OperationId);
    }

    [Fact]
    public async Task ListOperationsAsync_RespectsLimit()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        await service.CreateOperationAsync("compose_up", 1);
        await service.CreateOperationAsync("compose_down", 1);
        await service.CreateOperationAsync("compose_build", 1);

        var result = await service.ListOperationsAsync(limit: 2);

        result.Should().HaveCount(2);
    }

    // --- CleanupOldOperationsAsync ---

    [Fact]
    public async Task CleanupOldOperationsAsync_RemovesOldCompleted()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        var operation = await service.CreateOperationAsync("compose_up", 1);
        await service.UpdateOperationStatusAsync(operation.OperationId, OperationStatus.Completed);

        // Set CompletedAt to 30 days ago
        var op = await context.Operations.FirstAsync(o => o.OperationId == operation.OperationId);
        op.CompletedAt = DateTime.UtcNow.AddDays(-30);
        await context.SaveChangesAsync();

        int count = await service.CleanupOldOperationsAsync(DateTime.UtcNow.AddDays(-7));

        count.Should().Be(1);
        (await context.Operations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CleanupOldOperationsAsync_KeepsRecentOperations()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        var operation = await service.CreateOperationAsync("compose_up", 1);
        await service.UpdateOperationStatusAsync(operation.OperationId, OperationStatus.Completed);

        int count = await service.CleanupOldOperationsAsync(DateTime.UtcNow.AddDays(-7));

        count.Should().Be(0);
        (await context.Operations.CountAsync()).Should().Be(1);
    }

    // --- GetActiveOperationsCountAsync ---

    [Fact]
    public async Task GetActiveOperationsCountAsync_CountsPendingAndRunning()
    {
        using var context = CreateInMemoryDbContext();
        var service = CreateService(context);
        var op1 = await service.CreateOperationAsync("compose_up", 1); // Pending
        var op2 = await service.CreateOperationAsync("compose_down", 1); // Pending
        var op3 = await service.CreateOperationAsync("compose_build", 1); // Will be Running
        var op4 = await service.CreateOperationAsync("compose_pull", 1); // Will be Completed
        await service.UpdateOperationStatusAsync(op3.OperationId, OperationStatus.Running);
        await service.UpdateOperationStatusAsync(op4.OperationId, OperationStatus.Completed);

        int count = await service.GetActiveOperationsCountAsync();

        count.Should().Be(3); // 2 pending + 1 running
    }
}
