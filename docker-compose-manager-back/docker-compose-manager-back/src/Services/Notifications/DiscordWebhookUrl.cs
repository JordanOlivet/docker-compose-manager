namespace docker_compose_manager_back.Services.Notifications;

/// <summary>
/// Validation and display-masking helpers for Discord webhook URLs.
/// A webhook URL looks like:
/// https://discord.com/api/webhooks/{id}/{token}
/// </summary>
public static class DiscordWebhookUrl
{
    private static readonly string[] AllowedHosts = { "discord.com", "discordapp.com", "ptb.discord.com", "canary.discord.com" };

    /// <summary>
    /// Returns true if the value is a valid https Discord webhook URL.
    /// Empty/whitespace is considered invalid (callers allow empty separately to clear the setting).
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (!AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        // Path must be /api/webhooks/{id}/{token}
        string[] segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 4
            && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && segments[1].Equals("webhooks", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Masks the secret token portion of a webhook URL for display, keeping the
    /// scheme/host/id prefix so the user can recognize it. Non-webhook or empty
    /// values are returned unchanged.
    /// </summary>
    public static string? Mask(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !IsValid(value))
        {
            return value;
        }

        // Split off the token (last path segment) and replace it with a placeholder.
        int lastSlash = value.LastIndexOf('/');
        if (lastSlash < 0 || lastSlash == value.Length - 1)
        {
            return value;
        }

        return value[..(lastSlash + 1)] + "••••••••";
    }
}
