using System.Security.Cryptography;
using System.Text;

namespace Lighthouse.Security;

/// <summary>
/// Decrypts API keys that were encrypted at build time using OpenSSL AES-256-CBC with PBKDF2.
/// This keeps keys out of plain text in Docker image layers or config files.
/// </summary>
/// <remarks>
/// SECURITY NOTE: the passphrase below is compiled into the binary, so this is
/// <b>obfuscation, not confidentiality</b>. Anyone with the image/binary can recover the
/// key. It only raises the bar against casual inspection of image layers; it is not a
/// substitute for injecting real secrets at runtime (env var / secret store). Do not rely
/// on it to protect high-value credentials.
/// </remarks>
public static class ApiKeyProtector
{
    // Obfuscation passphrase only — see the security note on the class. Not a secret.
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
            byte[] keyAndIv = Rfc2898DeriveBytes.Pbkdf2(Passphrase, salt, Iterations, HashAlgorithmName.SHA256, 32 + 16);

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
