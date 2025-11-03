namespace docker_compose_manager_back.Validators;

/// <summary>
/// Configuration for validators to determine environment-specific behavior
/// </summary>
public static class ValidationConfig
{
    public static bool IsDevelopment { get; set; }

    /// <summary>
    /// Determines if strict password validation should be enforced
    /// In development mode, password restrictions are relaxed
    /// </summary>
    public static bool ShouldEnforceStrictPasswordRules => !IsDevelopment;
}
