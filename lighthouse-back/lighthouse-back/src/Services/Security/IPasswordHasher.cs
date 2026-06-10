namespace DockerComposeManager.Services.Security;

/// <summary>
/// Interface for password hashing and verification operations.
/// Abstracts the underlying hashing algorithm to allow for easy replacement.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a plaintext password using the configured algorithm and work factor.
    /// </summary>
    /// <param name="password">The plaintext password to hash</param>
    /// <returns>The hashed password</returns>
    /// <exception cref="ArgumentNullException">Thrown when password is null or empty</exception>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a plaintext password against a hashed password.
    /// </summary>
    /// <param name="password">The plaintext password to verify</param>
    /// <param name="hashedPassword">The hashed password to verify against</param>
    /// <returns>True if the password matches the hash, false otherwise</returns>
    /// <exception cref="ArgumentNullException">Thrown when password or hashedPassword is null or empty</exception>
    bool VerifyPassword(string password, string hashedPassword);

    /// <summary>
    /// Determines if a password hash needs to be rehashed due to outdated algorithm or work factor.
    /// This is useful for upgrading password security over time.
    /// </summary>
    /// <param name="hashedPassword">The hashed password to check</param>
    /// <returns>True if the hash should be updated, false otherwise</returns>
    bool NeedsRehash(string hashedPassword);
}
