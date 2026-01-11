namespace docker_compose_manager_back.Tests.Services;

public class UserServiceTests
{
    //private AppDbContext GetInMemoryDbContext()
    //{
    //    var options = new DbContextOptionsBuilder<AppDbContext>()
    //        .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
    //        .Options;

    //    var context = new AppDbContext(options);

    //    // Seed default roles
    //    context.Roles.AddRange(
    //        new Role { Id = 1, Name = "admin", Permissions = "[\"all\"]" },
    //        new Role { Id = 2, Name = "user", Permissions = "[\"read\"]" }
    //    );
    //    context.SaveChanges();

    //    return context;
    //}

    //[Fact]
    //public async Task GetAllUsersAsync_ReturnsAllUsers()
    //{
    //    // Arrange
    //    var context = GetInMemoryDbContext();
    //    var logger = new Mock<ILogger<UserService>>();
    //    var auditService = new Mock<IAuditService>();

    //    context.Users.AddRange(
    //        new User { Id = 1, Username = "admin", PasswordHash = "hash1", RoleId = 1, IsEnabled = true, CreatedAt = DateTime.UtcNow },
    //        new User { Id = 2, Username = "user1", PasswordHash = "hash2", RoleId = 2, IsEnabled = true, CreatedAt = DateTime.UtcNow }
    //    );
    //    await context.SaveChangesAsync();

    //    var service = new UserService(context, logger.Object, auditService.Object);

    //    // Act
    //    var users = await service.GetAllUsersAsync();

    //    // Assert
    //    Assert.Equal(2, users.Count);
    //    Assert.Contains(users, u => u.Username == "admin");
    //    Assert.Contains(users, u => u.Username == "user1");
    //}

    //[Fact]
    //public async Task CreateUserAsync_CreatesUserSuccessfully()
    //{
    //    // Arrange
    //    var context = GetInMemoryDbContext();
    //    var logger = new Mock<ILogger<UserService>>();
    //    var auditService = new Mock<IAuditService>();
    //    var service = new UserService(context, logger.Object, auditService.Object);

    //    var request = new CreateUserRequest("testuser", "password123", "user");

    //    // Act
    //    var result = await service.CreateUserAsync(request);

    //    // Assert
    //    Assert.NotNull(result);
    //    Assert.Equal("testuser", result.Username);
    //    Assert.Equal("user", result.Role);
    //    Assert.True(result.IsEnabled);
    //    Assert.True(result.MustChangePassword);

    //    // Verify password was hashed
    //    var user = await context.Users.FirstOrDefaultAsync(u => u.Username == "testuser");
    //    Assert.NotNull(user);
    //    Assert.NotEqual("password123", user.PasswordHash);
    //}

    //[Fact]
    //public async Task CreateUserAsync_ThrowsWhenUsernameExists()
    //{
    //    // Arrange
    //    var context = GetInMemoryDbContext();
    //    var logger = new Mock<ILogger<UserService>>();
    //    var auditService = new Mock<IAuditService>();

    //    context.Users.Add(new User
    //    {
    //        Username = "existinguser",
    //        PasswordHash = "hash",
    //        RoleId = 2,
    //        IsEnabled = true,
    //        CreatedAt = DateTime.UtcNow
    //    });
    //    await context.SaveChangesAsync();

    //    var service = new UserService(context, logger.Object, auditService.Object);
    //    var request = new CreateUserRequest("existinguser", "password123", "user");

    //    // Act & Assert
    //    await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateUserAsync(request));
    //}

    //[Fact]
    //public async Task DeleteUserAsync_PreventsDeletingLastAdmin()
    //{
    //    // Arrange
    //    var context = GetInMemoryDbContext();
    //    var logger = new Mock<ILogger<UserService>>();
    //    var auditService = new Mock<IAuditService>();

    //    var adminUser = new User
    //    {
    //        Id = 1,
    //        Username = "admin",
    //        PasswordHash = "hash",
    //        RoleId = 1,
    //        IsEnabled = true,
    //        CreatedAt = DateTime.UtcNow
    //    };
    //    context.Users.Add(adminUser);
    //    await context.SaveChangesAsync();

    //    var service = new UserService(context, logger.Object, auditService.Object);

    //    // Act & Assert
    //    var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteUserAsync(1));
    //    Assert.Contains("Cannot delete the last admin user", exception.Message);
    //}

    //[Fact]
    //public async Task UpdateUserAsync_UpdatesUserSuccessfully()
    //{
    //    // Arrange
    //    var context = GetInMemoryDbContext();
    //    var logger = new Mock<ILogger<UserService>>();
    //    var auditService = new Mock<IAuditService>();

    //    var user = new User
    //    {
    //        Id = 1,
    //        Username = "testuser",
    //        PasswordHash = "oldhash",
    //        RoleId = 2,
    //        IsEnabled = true,
    //        CreatedAt = DateTime.UtcNow
    //    };
    //    context.Users.Add(user);
    //    await context.SaveChangesAsync();

    //    var service = new UserService(context, logger.Object, auditService.Object);
    //    var request = new UpdateUserRequest(Role: "admin", IsEnabled: null, NewPassword: null);

    //    // Act
    //    var result = await service.UpdateUserAsync(1, request);

    //    // Assert
    //    Assert.NotNull(result);
    //    Assert.Equal("admin", result.Role);
    //}

    //[Fact]
    //public async Task EnableUserAsync_EnablesDisabledUser()
    //{
    //    // Arrange
    //    var context = GetInMemoryDbContext();
    //    var logger = new Mock<ILogger<UserService>>();
    //    var auditService = new Mock<IAuditService>();

    //    var user = new User
    //    {
    //        Id = 1,
    //        Username = "testuser",
    //        PasswordHash = "hash",
    //        RoleId = 2,
    //        IsEnabled = false,
    //        CreatedAt = DateTime.UtcNow
    //    };
    //    context.Users.Add(user);
    //    await context.SaveChangesAsync();

    //    var service = new UserService(context, logger.Object, auditService.Object);

    //    // Act
    //    var result = await service.EnableUserAsync(1);

    //    // Assert
    //    Assert.True(result.IsEnabled);
    //}

    //[Fact]
    //public async Task DisableUserAsync_DisablesEnabledUser()
    //{
    //    // Arrange
    //    var context = GetInMemoryDbContext();
    //    var logger = new Mock<ILogger<UserService>>();
    //    var auditService = new Mock<IAuditService>();

    //    context.Users.AddRange(
    //        new User { Id = 1, Username = "admin", PasswordHash = "hash1", RoleId = 1, IsEnabled = true, CreatedAt = DateTime.UtcNow },
    //        new User { Id = 2, Username = "user1", PasswordHash = "hash2", RoleId = 2, IsEnabled = true, CreatedAt = DateTime.UtcNow }
    //    );
    //    await context.SaveChangesAsync();

    //    var service = new UserService(context, logger.Object, auditService.Object);

    //    // Act
    //    var result = await service.DisableUserAsync(2);

    //    // Assert
    //    Assert.False(result.IsEnabled);
    //}
}
