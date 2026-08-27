using ParkEasy.Domain.Enums;

namespace ParkEasy.Application.Interfaces;

/// <summary>
/// Guarda o usuário autenticado na sessão atual do app (registrado como singleton).
/// </summary>
public interface ICurrentUserContext
{
    long? UserId { get; }
    string? Username { get; }
    UserRole? Role { get; }
    void SignIn(long userId, string username, UserRole role);
    void SignOut();
}
