using FluentValidation;
using docker_compose_manager_back.DTOs;

namespace docker_compose_manager_back.Validators;

public class CreateComposeFileRequestValidator : AbstractValidator<CreateComposeFileRequest>
{
    public CreateComposeFileRequestValidator()
    {
        RuleFor(x => x.FilePath)
            .NotEmpty().WithMessage("File path is required")
            .Must(path => path.EndsWith(".yml") || path.EndsWith(".yaml"))
            .WithMessage("File must be a YAML file (.yml or .yaml)");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("File content is required")
            .MinimumLength(10).WithMessage("File content must be at least 10 characters");
    }
}

public class UpdateComposeFileRequestValidator : AbstractValidator<UpdateComposeFileRequest>
{
    public UpdateComposeFileRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("File content is required")
            .MinimumLength(10).WithMessage("File content must be at least 10 characters");

        RuleFor(x => x.ETag)
            .NotEmpty().WithMessage("ETag is required for optimistic locking");
    }
}

public class ComposeUpRequestValidator : AbstractValidator<ComposeUpRequest>
{
    public ComposeUpRequestValidator()
    {
        // All fields are optional with defaults, but we can add custom validation if needed
    }
}

public class ComposeDownRequestValidator : AbstractValidator<ComposeDownRequest>
{
    public ComposeDownRequestValidator()
    {
        RuleFor(x => x.RemoveImages)
            .Must(x => x == null || x == "all" || x == "local")
            .WithMessage("RemoveImages must be 'all', 'local', or null");
    }
}
