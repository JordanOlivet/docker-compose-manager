using Microsoft.Extensions.Options;

namespace DockerComposeManager.Services.Security;

/// <summary>
/// BCrypt implementation of password hashing.
/// Uses BCrypt.Net-Next library with configurable work factor.
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    private readonly PasswordHashingOptions _options;
    private readonly ILogger<BCryptPasswordHasher> _logger;

    public BCryptPasswordHasher(
        IOptions<PasswordHashingOptions> options,
        ILogger<BCryptPasswordHasher> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate work factor on initialization
        if (_options.WorkFactor < 4 || _options.WorkFactor > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(_options.WorkFactor),
                $"BCrypt work factor must be between 4 and 31. Current value: {_options.WorkFactor}");
        }

        _logger.LogInformation(
            "BCryptPasswordHasher initialized with work factor {WorkFactor}",
            _options.WorkFactor);
    }

    /// <inheritdoc />
    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentNullException(nameof(password), "Password cannot be null or empty");
        }

        try
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(password, _options.WorkFactor);
            _logger.LogDebug("Password hashed successfully with work factor {WorkFactor}", _options.WorkFactor);
            return hash;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hashing password");
            throw;
        }
    }

    /// <inheritdoc />
    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentNullException(nameof(password), "Password cannot be null or empty");
        }

        if (string.IsNullOrEmpty(hashedPassword))
        {
            throw new ArgumentNullException(nameof(hashedPassword), "Hashed password cannot be null or empty");
        }

        try
        {
            var isValid = BCrypt.Net.BCrypt.Verify(password, hashedPassword);
            _logger.LogDebug("Password verification result: {IsValid}", isValid);
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error verifying password. This may indicate a corrupted hash.");
            return false;
        }
    }

    /// <inheritdoc />
    public bool NeedsRehash(string hashedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword))
        {
            return true;
        }

        try
        {
            // BCrypt hash format: $2a$[workFactor]$[salt + hash]
            // Extract work factor from hash and compare with current setting
            var parts = hashedPassword.Split('$');
            if (parts.Length < 4)
            {
                _logger.LogWarning("Invalid hash format, rehash needed");
                return true;
            }

            if (int.TryParse(parts[2], out int hashWorkFactor))
            {
                var needsRehash = hashWorkFactor < _options.WorkFactor;
                if (needsRehash)
                {
                    _logger.LogInformation(
                        "Hash with work factor {OldWorkFactor} needs rehashing to {NewWorkFactor}",
                        hashWorkFactor,
                        _options.WorkFactor);
                }
                return needsRehash;
            }

            _logger.LogWarning("Could not parse work factor from hash, rehash needed");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if rehash is needed");
            return true;
        }
    }
}

/// <summary>
/// Configuration options for password hashing.
/// </summary>
public class PasswordHashingOptions
{
    public const string SectionName = "Security:PasswordHashing";

    /// <summary>
    /// BCrypt work factor (cost). Higher values increase security but take longer.
    /// Recommended range: 10-14 for production.
    /// Default: 12 (provides good balance of security and performance)
    /// </summary>
    public int WorkFactor { get; set; } = 12;
}
