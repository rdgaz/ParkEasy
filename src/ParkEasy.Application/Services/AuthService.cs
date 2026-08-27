using ParkEasy.Application.Interfaces;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;
using ParkEasy.Domain.Interfaces;

namespace ParkEasy.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        var user = await _userRepository.GetByUsernameAsync(username.Trim());

        if (user is null)
            return null;

        return PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt) ? user : null;
    }

    public async Task ChangePasswordAsync(long actingUserId, long targetUserId, string? currentPassword, string newPassword)
    {
        var targetUser = await _userRepository.GetByIdAsync(targetUserId);

        if (targetUser is null)
            throw new InvalidOperationException("Usuário não encontrado.");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            throw new ArgumentException("A nova senha deve ter pelo menos 6 caracteres.");

        if (actingUserId == targetUserId)
        {
            if (string.IsNullOrWhiteSpace(currentPassword) ||
                !PasswordHasher.Verify(currentPassword, targetUser.PasswordHash, targetUser.PasswordSalt))
                throw new InvalidOperationException("Senha atual incorreta.");
        }
        else
        {
            var actingUser = await _userRepository.GetByIdAsync(actingUserId);

            if (actingUser is null)
                throw new InvalidOperationException("Usuário não encontrado.");

            if (actingUser.Role <= targetUser.Role)
                throw new InvalidOperationException("Você não tem autoridade para alterar a senha deste usuário.");
        }

        var (hash, salt) = PasswordHasher.Hash(newPassword);
        targetUser.PasswordHash = hash;
        targetUser.PasswordSalt = salt;

        await _userRepository.UpdateAsync(targetUser);
    }

    public async Task<User> CreateUserAsync(long actingUserId, string username, string password, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Informe o nome de usuário.");

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            throw new ArgumentException("A senha deve ter pelo menos 6 caracteres.");

        var actingUser = await _userRepository.GetByIdAsync(actingUserId);

        if (actingUser is null)
            throw new InvalidOperationException("Usuário não encontrado.");

        if (role > actingUser.Role)
            throw new InvalidOperationException("Você não pode criar um usuário com cargo acima do seu.");

        var normalizedUsername = username.Trim();
        var existing = await _userRepository.GetByUsernameAsync(normalizedUsername);

        if (existing is not null)
            throw new InvalidOperationException("Já existe um usuário com esse nome.");

        var (hash, salt) = PasswordHasher.Hash(password);

        var newUser = new User
        {
            Username = normalizedUsername,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = role,
            CreatedAt = DateTime.Now
        };

        await _userRepository.AddAsync(newUser);

        return newUser;
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _userRepository.GetAllAsync();
    }
}
