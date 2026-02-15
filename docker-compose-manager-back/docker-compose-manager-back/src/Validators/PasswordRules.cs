using FluentValidation;

namespace docker_compose_manager_back.Validators;

/// <summary>
/// Centralized password validation rules applied consistently across the entire application.
/// All password validations (create user, change password, reset password, update user)
/// use these shared rules to ensure consistency.
/// </summary>
public static class PasswordRules
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    private static readonly string[] WeakPatterns = { "12345", "password", "qwerty", "abc123", "admin" };

    /// <summary>
    /// Applies the full set of password validation rules to a FluentValidation rule builder.
    /// Rules are only enforced when <see cref="ValidationConfig.ShouldEnforceStrictPasswordRules"/> is true (production).
    /// </summary>
    public static IRuleBuilderOptions<T, string> ApplyPasswordRules<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .MinimumLength(MinLength).WithMessage($"Password must be at least {MinLength} characters")
            .MaximumLength(MaxLength).WithMessage($"Password cannot exceed {MaxLength} characters")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character")
            .Must(password => !ContainsWeakPattern(password))
                .WithMessage("Password contains a common weak pattern");
    }

    /// <summary>
    /// Checks if the password contains any known weak patterns.
    /// </summary>
    public static bool ContainsWeakPattern(string password)
    {
        if (string.IsNullOrEmpty(password)) return false;
        string lowerPassword = password.ToLower();
        return WeakPatterns.Any(pattern => lowerPassword.Contains(pattern));
    }
}
