using ParkEasy.Application.Interfaces;
using ParkEasy.Domain.Enums;

namespace ParkEasy.Application.Services;

public class CurrentUserContext : ICurrentUserContext
{
    public long? UserId { get; private set; }
    public string? Username { get; private set; }
    public UserRole? Role { get; private set; }

    public void SignIn(long userId, string username, UserRole role)
    {
        UserId = userId;
        Username = username;
        Role = role;
    }

    public void SignOut()
    {
        UserId = null;
        Username = null;
        Role = null;
    }
}
