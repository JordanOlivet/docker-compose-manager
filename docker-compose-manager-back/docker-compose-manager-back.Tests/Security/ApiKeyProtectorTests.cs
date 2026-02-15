using docker_compose_manager_back.Security;

namespace docker_compose_manager_back.Tests.Security;

public class ApiKeyProtectorTests
{
    [Fact]
    public void Decrypt_WithValidEncryptedValue_ReturnsOriginalKey()
    {
        // This value was encrypted with:
        // echo -n "re_test_fake_api_key_123" | openssl enc -aes-256-cbc -pbkdf2 -iter 100000 -md sha256 -pass pass:dcm-k3y-sh13ld-x7q9m2v4 -base64 -A
        string encrypted = "U2FsdGVkX19WA9sXtjFkN7aVwyp8azmASpMEYkt4cpZvjA+C2CYRkhDgK5SF/x9A";

        string? result = ApiKeyProtector.Decrypt(encrypted);

        Assert.Equal("re_test_fake_api_key_123", result);
    }

    [Fact]
    public void Decrypt_WithNull_ReturnsNull()
    {
        Assert.Null(ApiKeyProtector.Decrypt(null));
    }

    [Fact]
    public void Decrypt_WithEmpty_ReturnsNull()
    {
        Assert.Null(ApiKeyProtector.Decrypt(""));
    }

    [Fact]
    public void Decrypt_WithInvalidBase64_ReturnsNull()
    {
        Assert.Null(ApiKeyProtector.Decrypt("not-valid-base64!!!"));
    }

    [Fact]
    public void Decrypt_WithWrongEncryptedData_ReturnsNull()
    {
        // Valid base64 but not encrypted with our passphrase
        Assert.Null(ApiKeyProtector.Decrypt("SGVsbG8gV29ybGQ="));
    }
}
