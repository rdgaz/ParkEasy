using Moq;
using ParkEasy.Application.Services;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;
using ParkEasy.Domain.Interfaces;
using Xunit;

namespace ParkEasy.Tests;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _repoMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _repoMock = new Mock<IUserRepository>();
        _authService = new AuthService(_repoMock.Object);
    }

    private static User MakeUser(long id, string username, string password, UserRole role)
    {
        var (hash, salt) = PasswordHasher.Hash(password);
        return new User { Id = id, Username = username, PasswordHash = hash, PasswordSalt = salt, Role = role };
    }

    [Fact]
    public async Task AuthenticateAsync_ValidCredentials_ReturnsUser()
    {
        var user = MakeUser(1, "admin", "senha123", UserRole.Desenvolvedor);
        _repoMock.Setup(r => r.GetByUsernameAsync("admin")).ReturnsAsync(user);

        var result = await _authService.AuthenticateAsync("admin", "senha123");

        Assert.NotNull(result);
        Assert.Equal(1, result!.Id);
    }

    [Fact]
    public async Task AuthenticateAsync_WrongPassword_ReturnsNull()
    {
        var user = MakeUser(1, "admin", "senha123", UserRole.Desenvolvedor);
        _repoMock.Setup(r => r.GetByUsernameAsync("admin")).ReturnsAsync(user);

        var result = await _authService.AuthenticateAsync("admin", "senhaErrada");

        Assert.Null(result);
    }

    [Fact]
    public async Task AuthenticateAsync_UnknownUsername_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetByUsernameAsync("naoexiste")).ReturnsAsync((User?)null);

        var result = await _authService.AuthenticateAsync("naoexiste", "qualquercoisa");

        Assert.Null(result);
    }

    [Fact]
    public async Task ChangePasswordAsync_Self_CorrectCurrentPassword_UpdatesHash()
    {
        var user = MakeUser(1, "admin", "senhaAntiga", UserRole.Colaborador);
        _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

        await _authService.ChangePasswordAsync(1, 1, "senhaAntiga", "senhaNovaSegura");

        Assert.True(PasswordHasher.Verify("senhaNovaSegura", user.PasswordHash, user.PasswordSalt));
        _repoMock.Verify(r => r.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_Self_WrongCurrentPassword_ThrowsInvalidOperationException()
    {
        var user = MakeUser(1, "admin", "senhaAntiga", UserRole.Colaborador);
        _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _authService.ChangePasswordAsync(1, 1, "senhaErrada", "senhaNovaSegura"));
    }

    [Fact]
    public async Task ChangePasswordAsync_HigherRoleResetsLowerRole_SucceedsWithoutCurrentPassword()
    {
        var manager = MakeUser(1, "gerente", "x", UserRole.Gerente);
        var collaborator = MakeUser(2, "colaborador", "senhaAntiga", UserRole.Colaborador);

        _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(manager);
        _repoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(collaborator);

        await _authService.ChangePasswordAsync(1, 2, currentPassword: null, "senhaNovaSegura");

        Assert.True(PasswordHasher.Verify("senhaNovaSegura", collaborator.PasswordHash, collaborator.PasswordSalt));
    }

    [Fact]
    public async Task ChangePasswordAsync_SameRoleCannotResetPeer_ThrowsInvalidOperationException()
    {
        var manager1 = MakeUser(1, "gerente1", "x", UserRole.Gerente);
        var manager2 = MakeUser(2, "gerente2", "y", UserRole.Gerente);

        _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(manager1);
        _repoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(manager2);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _authService.ChangePasswordAsync(1, 2, currentPassword: null, "senhaNovaSegura"));
    }

    [Fact]
    public async Task ChangePasswordAsync_LowerRoleCannotResetHigherRole_ThrowsInvalidOperationException()
    {
        var collaborator = MakeUser(1, "colaborador", "x", UserRole.Colaborador);
        var manager = MakeUser(2, "gerente", "y", UserRole.Gerente);

        _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(collaborator);
        _repoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(manager);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _authService.ChangePasswordAsync(1, 2, currentPassword: null, "senhaNovaSegura"));
    }

    [Fact]
    public async Task ChangePasswordAsync_NewPasswordTooShort_ThrowsArgumentException()
    {
        var user = MakeUser(1, "admin", "senhaAntiga", UserRole.Colaborador);
        _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _authService.ChangePasswordAsync(1, 1, "senhaAntiga", "abc"));
    }

    [Fact]
    public async Task CreateUserAsync_RoleAtOrBelowOwnLevel_Succeeds()
    {
        var manager = MakeUser(1, "gerente", "x", UserRole.Gerente);
        _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(manager);
        _repoMock.Setup(r => r.GetByUsernameAsync("novo")).ReturnsAsync((User?)null);

        var result = await _authService.CreateUserAsync(1, "novo", "senha123", UserRole.Colaborador);

        Assert.Equal("novo", result.Username);
        Assert.Equal(UserRole.Colaborador, result.Role);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_RoleAboveOwnLevel_ThrowsInvalidOperationException()
    {
        var manager = MakeUser(1, "gerente", "x", UserRole.Gerente);
        _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(manager);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _authService.CreateUserAsync(1, "novo", "senha123", UserRole.Administrador));
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateUsername_ThrowsInvalidOperationException()
    {
        var admin = MakeUser(1, "admin", "x", UserRole.Desenvolvedor);
        var existing = MakeUser(2, "joao", "y", UserRole.Colaborador);

        _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(admin);
        _repoMock.Setup(r => r.GetByUsernameAsync("joao")).ReturnsAsync(existing);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _authService.CreateUserAsync(1, "joao", "senha123", UserRole.Colaborador));
    }
}
