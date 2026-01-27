using FluentValidation;
using docker_compose_manager_back.Controllers;

namespace docker_compose_manager_back.Validators;

/// <summary>
/// Validator for create role requests
/// </summary>
public class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Role name is required")
            .Length(2, 50).WithMessage("Role name must be between 2 and 50 characters")
            .Matches(@"^[a-zA-Z0-9_-]+$").WithMessage("Role name can only contain letters, numbers, hyphens and underscores")
            .Must(name => name != "admin" && name != "user")
            .WithMessage("Cannot create built-in role names 'admin' or 'user'");

        RuleFor(x => x.Permissions)
            .NotNull().WithMessage("Permissions are required")
            .NotEmpty().WithMessage("At least one permission is required")
            .Must(permissions => permissions.All(p => !string.IsNullOrWhiteSpace(p)))
            .WithMessage("Permissions cannot be empty strings");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}

/// <summary>
/// Validator for update role requests
/// </summary>
public class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        When(x => x.Permissions != null, () =>
        {
            RuleFor(x => x.Permissions)
                .NotEmpty().WithMessage("At least one permission is required")
                .Must(permissions => permissions!.All(p => !string.IsNullOrWhiteSpace(p)))
                .WithMessage("Permissions cannot be empty strings");
        });

        When(x => !string.IsNullOrEmpty(x.Description), () =>
        {
            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Description cannot exceed 500 characters");
        });
    }
}
