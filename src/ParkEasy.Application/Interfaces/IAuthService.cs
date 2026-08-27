using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;

namespace ParkEasy.Application.Interfaces;

public interface IAuthService
{
    Task<User?> AuthenticateAsync(string username, string password);

    /// <summary>
    /// Troca a senha de <paramref name="targetUserId"/>. Se for o próprio usuário
    /// (<paramref name="actingUserId"/> == <paramref name="targetUserId"/>), exige
    /// <paramref name="currentPassword"/> correta. Caso contrário, exige que quem está agindo
    /// tenha um cargo com autoridade estritamente maior que a do usuário alvo (não precisa da
    /// senha atual — é uma redefinição administrativa).
    /// </summary>
    Task ChangePasswordAsync(long actingUserId, long targetUserId, string? currentPassword, string newPassword);

    /// <summary>
    /// Cria um novo usuário. Só é permitido atribuir um cargo igual ou abaixo do cargo de
    /// quem está criando (ninguém cria alguém com mais autoridade que si próprio).
    /// </summary>
    Task<User> CreateUserAsync(long actingUserId, string username, string password, UserRole role);

    Task<List<User>> GetAllUsersAsync();
}
