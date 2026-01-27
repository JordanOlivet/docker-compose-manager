using FluentValidation;
using docker_compose_manager_back.DTOs;

namespace docker_compose_manager_back.Validators;

/// <summary>
/// Validator for add compose path requests
/// </summary>
public class AddComposePathRequestValidator : AbstractValidator<AddComposePathRequest>
{
    public AddComposePathRequestValidator()
    {
        RuleFor(x => x.Path)
            .NotEmpty().WithMessage("Path is required")
            .Must(BeValidPath).WithMessage("Path must be a valid directory path")
            .MaximumLength(500).WithMessage("Path cannot exceed 500 characters");
    }

    private static bool BeValidPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            // Check if path contains invalid characters
            var invalidChars = Path.GetInvalidPathChars();
            if (path.Any(c => invalidChars.Contains(c)))
                return false;

            // Try to get full path
            _ = Path.GetFullPath(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Validator for duplicate file requests
/// </summary>
public class DuplicateFileRequestValidator : AbstractValidator<DuplicateFileRequest>
{
    public DuplicateFileRequestValidator()
    {
        When(x => !string.IsNullOrEmpty(x.NewFileName), () =>
        {
            RuleFor(x => x.NewFileName)
                .Must(BeValidFileName).WithMessage("Invalid file name")
                .MaximumLength(255).WithMessage("File name cannot exceed 255 characters")
                .Must(name => name!.EndsWith(".yml") || name.EndsWith(".yaml") || !name.Contains("."))
                .WithMessage("File must have .yml or .yaml extension, or no extension");
        });
    }

    private static bool BeValidFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return true;

        try
        {
            // Check for invalid characters
            var invalidChars = Path.GetInvalidFileNameChars();
            return !fileName.Any(c => invalidChars.Contains(c));
        }
        catch
        {
            return false;
        }
    }
}
