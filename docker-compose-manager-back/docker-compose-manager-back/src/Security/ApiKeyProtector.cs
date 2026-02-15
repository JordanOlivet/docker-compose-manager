using System.Security.Cryptography;
using System.Text;

namespace docker_compose_manager_back.Security;

/// <summary>
/// Decrypts API keys that were encrypted at build time using OpenSSL AES-256-CBC with PBKDF2.
/// This prevents keys from appearing in plain text in Docker image layers or config files.
/// </summary>
public static class ApiKeyProtector
{
    private static readonly byte[] Passphrase = Encoding.UTF8.GetBytes("dcm-k3y-sh13ld-x7q9m2v4");
    private const int Iterations = 100000;
    private const int SaltLength = 8;
    private static readonly byte[] OpenSslMagic = "Salted__"u8.ToArray();

    /// <summary>
    /// Decrypts a base64-encoded string that was encrypted with:
    /// openssl enc -aes-256-cbc -pbkdf2 -iter 100000 -md sha256 -pass pass:PASSPHRASE -base64 -A
    /// </summary>
    public static string? Decrypt(string? encryptedBase64)
    {
        if (string.IsNullOrEmpty(encryptedBase64))
            return null;

        try
        {
            byte[] encrypted = Convert.FromBase64String(encryptedBase64);

            // OpenSSL format: "Salted__" (8 bytes) + salt (8 bytes) + ciphertext
            if (encrypted.Length < OpenSslMagic.Length + SaltLength)
                return null;

            // Verify OpenSSL magic header
            for (int i = 0; i < OpenSslMagic.Length; i++)
            {
                if (encrypted[i] != OpenSslMagic[i])
                    return null;
            }

            byte[] salt = encrypted[OpenSslMagic.Length..(OpenSslMagic.Length + SaltLength)];
            byte[] ciphertext = encrypted[(OpenSslMagic.Length + SaltLength)..];

            // Derive key (32 bytes) + IV (16 bytes) using PBKDF2-SHA256
            using var kdf = new Rfc2898DeriveBytes(Passphrase, salt, Iterations, HashAlgorithmName.SHA256);
            byte[] keyAndIv = kdf.GetBytes(32 + 16);

            using var aes = Aes.Create();
            aes.Key = keyAndIv[..32];
            aes.IV = keyAndIv[32..];
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            byte[] decrypted = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return null;
        }
    }
}
