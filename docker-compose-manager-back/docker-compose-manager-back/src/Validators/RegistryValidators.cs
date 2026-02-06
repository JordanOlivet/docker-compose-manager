using docker_compose_manager_back.DTOs;
using FluentValidation;

namespace docker_compose_manager_back.Validators;

/// <summary>
/// Validator for registry login requests
/// </summary>
public class RegistryLoginRequestValidator : AbstractValidator<RegistryLoginRequest>
{
    private static readonly string[] ValidAuthTypes = { "password", "token" };

    public RegistryLoginRequestValidator()
    {
        RuleFor(x => x.RegistryUrl)
            .NotEmpty().WithMessage("Registry URL is required")
            .MaximumLength(500).WithMessage("Registry URL must not exceed 500 characters");

        RuleFor(x => x.AuthType)
            .NotEmpty().WithMessage("Authentication type is required")
            .Must(authType => ValidAuthTypes.Contains(authType.ToLower()))
            .WithMessage("Authentication type must be 'password' or 'token'");

        // Username is required when using password authentication
        When(x => x.AuthType?.ToLower() == "password", () =>
        {
            RuleFor(x => x.Username)
                .NotEmpty().WithMessage("Username is required for password authentication")
                .MaximumLength(200).WithMessage("Username must not exceed 200 characters");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required for password authentication");
        });

        // Token is required when using token authentication
        When(x => x.AuthType?.ToLower() == "token", () =>
        {
            RuleFor(x => x.Token)
                .NotEmpty().WithMessage("Token is required for token authentication");
        });
    }
}

/// <summary>
/// Validator for registry logout requests
/// </summary>
public class RegistryLogoutRequestValidator : AbstractValidator<RegistryLogoutRequest>
{
    public RegistryLogoutRequestValidator()
    {
        RuleFor(x => x.RegistryUrl)
            .NotEmpty().WithMessage("Registry URL is required")
            .MaximumLength(500).WithMessage("Registry URL must not exceed 500 characters");
    }
}
