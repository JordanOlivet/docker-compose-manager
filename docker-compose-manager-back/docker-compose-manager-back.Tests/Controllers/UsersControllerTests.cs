using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using docker_compose_manager_back.Controllers;
using docker_compose_manager_back.Services;
using docker_compose_manager_back.DTOs;

namespace docker_compose_manager_back.Tests.Controllers;

public class UsersControllerTests
{
    [Fact]
    public async Task GetAllUsers_ReturnsOkWithUsers()
    {
        // Arrange
        var mockUserService = new Mock<IUserService>();
        var mockLogger = new Mock<ILogger<UsersController>>();

        var users = new List<UserDto>
        {
            new UserDto(1, "admin", null, "admin", true, false, false, DateTime.UtcNow, null),
            new UserDto(2, "user1", null, "user", true, false, false, DateTime.UtcNow, null)
        };

        var paginatedResponse = new PaginatedResponse<UserDto>(
            Items: users,
            PageNumber: 1,
            PageSize: 20,
            TotalPages: 1,
            TotalItems: 2,
            HasNext: false,
            HasPrevious: false
        );

        mockUserService.Setup(s => s.GetAllUsersAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<string>(),
            It.IsAny<bool>())).ReturnsAsync(paginatedResponse);

        var controller = new UsersController(mockUserService.Object, mockLogger.Object);

        // Act
        var result = await controller.GetAllUsers();

        // Assert
        var okResult = Assert.IsType<ActionResult<ApiResponse<PaginatedResponse<UserDto>>>>(result);
        var objectResult = Assert.IsType<OkObjectResult>(okResult.Result);
        var response = Assert.IsType<ApiResponse<PaginatedResponse<UserDto>>>(objectResult.Value);
        Assert.True(response.Success);
        Assert.Equal(2, response.Data?.TotalItems);
        Assert.Equal(2, response.Data?.Items.Count);
    }

    [Fact]
    public async Task GetUser_ReturnsNotFoundWhenUserDoesNotExist()
    {
        // Arrange
        var mockUserService = new Mock<IUserService>();
        var mockLogger = new Mock<ILogger<UsersController>>();

        mockUserService.Setup(s => s.GetUserByIdAsync(It.IsAny<int>())).ReturnsAsync((UserDto?)null);

        var controller = new UsersController(mockUserService.Object, mockLogger.Object);

        // Act
        var result = await controller.GetUser(999);

        // Assert
        var actionResult = Assert.IsType<ActionResult<ApiResponse<UserDto>>>(result);
        Assert.IsType<NotFoundObjectResult>(actionResult.Result);
    }

    [Fact]
    public async Task CreateUser_ReturnsCreatedWhenValid()
    {
        // Arrange
        var mockUserService = new Mock<IUserService>();
        var mockLogger = new Mock<ILogger<UsersController>>();

        var createdUser = new UserDto(1, "newuser", null, "user", true, true, false, DateTime.UtcNow, null);
        mockUserService.Setup(s => s.CreateUserAsync(It.IsAny<CreateUserRequest>()))
            .ReturnsAsync(createdUser);

        var controller = new UsersController(mockUserService.Object, mockLogger.Object);
        var request = new CreateUserRequest("newuser", "password123", "user");

        // Act
        var result = await controller.CreateUser(request);

        // Assert
        var actionResult = Assert.IsType<ActionResult<ApiResponse<UserDto>>>(result);
        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<UserDto>>(createdResult.Value);
        Assert.True(response.Success);
        Assert.Equal("newuser", response.Data?.Username);
    }

    [Fact]
    public async Task CreateUser_ReturnsBadRequestWhenUsernameEmpty()
    {
        // Arrange
        var mockUserService = new Mock<IUserService>();
        var mockLogger = new Mock<ILogger<UsersController>>();
        var controller = new UsersController(mockUserService.Object, mockLogger.Object);
        var request = new CreateUserRequest("", "password123", "user");

        // Act
        var result = await controller.CreateUser(request);

        // Assert
        var actionResult = Assert.IsType<ActionResult<ApiResponse<UserDto>>>(result);
        Assert.IsType<BadRequestObjectResult>(actionResult.Result);
    }

    [Fact]
    public async Task CreateUser_ReturnsBadRequestWhenPasswordTooShort()
    {
        // Arrange
        var mockUserService = new Mock<IUserService>();
        var mockLogger = new Mock<ILogger<UsersController>>();
        var controller = new UsersController(mockUserService.Object, mockLogger.Object);
        var request = new CreateUserRequest("testuser", "short", "user");

        // Act
        var result = await controller.CreateUser(request);

        // Assert
        var actionResult = Assert.IsType<ActionResult<ApiResponse<UserDto>>>(result);
        Assert.IsType<BadRequestObjectResult>(actionResult.Result);
    }

    [Fact]
    public async Task DeleteUser_ReturnsOkWhenSuccessful()
    {
        // Arrange
        var mockUserService = new Mock<IUserService>();
        var mockLogger = new Mock<ILogger<UsersController>>();

        mockUserService.Setup(s => s.DeleteUserAsync(It.IsAny<int>())).Returns(Task.CompletedTask);

        var controller = new UsersController(mockUserService.Object, mockLogger.Object);

        // Act
        var result = await controller.DeleteUser(1);

        // Assert
        var actionResult = Assert.IsType<ActionResult<ApiResponse<object>>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task EnableUser_ReturnsOkWithEnabledUser()
    {
        // Arrange
        var mockUserService = new Mock<IUserService>();
        var mockLogger = new Mock<ILogger<UsersController>>();

        var enabledUser = new UserDto(1, "testuser", null, "user", true, false, false, DateTime.UtcNow, null);
        mockUserService.Setup(s => s.EnableUserAsync(It.IsAny<int>())).ReturnsAsync(enabledUser);

        var controller = new UsersController(mockUserService.Object, mockLogger.Object);

        // Act
        var result = await controller.EnableUser(1);

        // Assert
        var actionResult = Assert.IsType<ActionResult<ApiResponse<UserDto>>>(result);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<UserDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.True(response.Data?.IsEnabled);
    }
}
