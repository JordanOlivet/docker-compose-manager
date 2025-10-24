using FluentValidation;

namespace docker_compose_manager_back.Validators;

// Note: ContainerIdValidator removed - was too generic and caused conflicts
// Container ID validation should be done in specific validators or controllers

/// <summary>
/// Validator for bulk container action requests
/// </summary>
public class BulkContainerActionRequest
{
    public List<string> ContainerIds { get; set; } = new();
    public string Action { get; set; } = string.Empty;
}

public class BulkContainerActionRequestValidator : AbstractValidator<BulkContainerActionRequest>
{
    public BulkContainerActionRequestValidator()
    {
        RuleFor(x => x.ContainerIds)
            .NotEmpty().WithMessage("At least one container ID is required")
            .Must(ids => ids.Count <= 50).WithMessage("Cannot perform bulk action on more than 50 containers at once")
            .Must(ids => ids.All(id => !string.IsNullOrWhiteSpace(id)))
            .WithMessage("Container IDs cannot be empty");

        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("Action is required")
            .Must(action => new[] { "start", "stop", "restart", "remove" }.Contains(action.ToLower()))
            .WithMessage("Action must be one of: start, stop, restart, remove");
    }
}
