using ParkEasy.Application.Services;
using Xunit;

namespace ParkEasy.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ThenVerify_SamePassword_ReturnsTrue()
    {
        var (hash, salt) = PasswordHasher.Hash("MinhaSenha123");

        var result = PasswordHasher.Verify("MinhaSenha123", hash, salt);

        Assert.True(result);
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var (hash, salt) = PasswordHasher.Hash("MinhaSenha123");

        var result = PasswordHasher.Verify("SenhaErrada", hash, salt);

        Assert.False(result);
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashAndSalt()
    {
        var (hash1, salt1) = PasswordHasher.Hash("MinhaSenha123");
        var (hash2, salt2) = PasswordHasher.Hash("MinhaSenha123");

        Assert.NotEqual(salt1, salt2);
        Assert.NotEqual(hash1, hash2);

        Assert.True(PasswordHasher.Verify("MinhaSenha123", hash1, salt1));
        Assert.True(PasswordHasher.Verify("MinhaSenha123", hash2, salt2));
    }
}
