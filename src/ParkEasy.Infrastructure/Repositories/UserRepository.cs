using Microsoft.EntityFrameworkCore;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Interfaces;
using ParkEasy.Infrastructure.Data;

namespace ParkEasy.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ParkingDbContext _context;

    public UserRepository(ParkingDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(long id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        var normalized = username.ToUpperInvariant();

        return await _context.Users
            .FirstOrDefaultAsync(u => u.Username.ToUpper() == normalized);
    }

    public async Task<List<User>> GetAllAsync()
    {
        return await _context.Users
            .OrderBy(u => u.Username)
            .ToListAsync();
    }

    public async Task AddAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> AnyUsersExistAsync()
    {
        return await _context.Users.AnyAsync();
    }
}
