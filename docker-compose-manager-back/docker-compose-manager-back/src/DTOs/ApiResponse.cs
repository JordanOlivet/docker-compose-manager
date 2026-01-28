namespace docker_compose_manager_back.DTOs;

public record ApiResponse<T>(
    T? Data,
    bool Success,
    string? Message = null,
    Dictionary<string, string[]>? Errors = null,
    string? ErrorCode = null
);

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data, string? message = null)
        => new(data, true, message);

    public static ApiResponse<T> Fail<T>(string message, string? errorCode = null, Dictionary<string, string[]>? errors = null)
        => new(default, false, message, errors, errorCode);
}
