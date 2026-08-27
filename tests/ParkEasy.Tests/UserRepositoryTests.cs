using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ParkEasy.Domain.Entities;
using ParkEasy.Domain.Enums;
using ParkEasy.Infrastructure.Data;
using ParkEasy.Infrastructure.Repositories;
using Xunit;

namespace ParkEasy.Tests;

public class UserRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ParkingDbContext _context;
    private readonly UserRepository _repository;

    public UserRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ParkingDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ParkingDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new UserRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static User MakeUser(string username, UserRole role = UserRole.Colaborador) => new()
    {
        Username = username,
        PasswordHash = "hash",
        PasswordSalt = "salt",
        Role = role,
        CreatedAt = DateTime.Now
    };

    [Fact]
    public async Task GetByUsernameAsync_ExistingUsername_ReturnsUser()
    {
        await _repository.AddAsync(MakeUser("admin"));

        var result = await _repository.GetByUsernameAsync("admin");

        Assert.NotNull(result);
        Assert.Equal("admin", result!.Username);
    }

    [Fact]
    public async Task GetByUsernameAsync_IsCaseInsensitive()
    {
        await _repository.AddAsync(MakeUser("Admin"));

        var result = await _repository.GetByUsernameAsync("ADMIN");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetByUsernameAsync_UnknownUsername_ReturnsNull()
    {
        var result = await _repository.GetByUsernameAsync("naoexiste");

        Assert.Null(result);
    }

    [Fact]
    public async Task AnyUsersExistAsync_EmptyTable_ReturnsFalse()
    {
        Assert.False(await _repository.AnyUsersExistAsync());
    }

    [Fact]
    public async Task AnyUsersExistAsync_AfterAddingUser_ReturnsTrue()
    {
        await _repository.AddAsync(MakeUser("admin"));

        Assert.True(await _repository.AnyUsersExistAsync());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsUsersOrderedByUsername()
    {
        await _repository.AddAsync(MakeUser("zeca", UserRole.Colaborador));
        await _repository.AddAsync(MakeUser("admin", UserRole.Desenvolvedor));
        await _repository.AddAsync(MakeUser("maria", UserRole.Gerente));

        var result = await _repository.GetAllAsync();

        Assert.Equal(["admin", "maria", "zeca"], result.Select(u => u.Username));
    }

    [Fact]
    public async Task AddAsync_DuplicateUsername_ThrowsDbUpdateException()
    {
        await _repository.AddAsync(MakeUser("admin"));

        await Assert.ThrowsAsync<DbUpdateException>(() => _repository.AddAsync(MakeUser("admin")));
    }
}
