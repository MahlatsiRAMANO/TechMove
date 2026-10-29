using Microsoft.EntityFrameworkCore;
using TechMove.Api.Auth;
using TechMove.Api.Data;
using TechMove.Api.Data.Repositories;
using TechMove.Api.Models;
using TechMove.Api.Services;

namespace TechMove.Tests;

/// <summary>Account rules: sign-up, passwords, role changes and the "always one Admin" safeguard.</summary>
public class UserServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("users-" + Guid.NewGuid()).Options);
        _db.Database.EnsureCreated(); // seeds admin (1), staff (2), acme customer (3)
        _sut = new UserService(new UserRepository(_db), new ClientRepository(_db), new FixedClock(new DateTime(2026, 10, 29)));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void PasswordHasher_HashesAndVerifies_AndUsesRandomSalt()
    {
        var a = PasswordHasher.Hash("Secret123");
        var b = PasswordHasher.Hash("Secret123");

        Assert.NotEqual(a, b);                       // different salt each time
        Assert.DoesNotContain("Secret123", a);       // never stored in plain text
        Assert.True(PasswordHasher.Verify("Secret123", a));
        Assert.False(PasswordHasher.Verify("secret123", a));
        Assert.False(PasswordHasher.Verify("Secret123", "not-a-hash"));
    }

    [Fact]
    public async Task Register_CreatesUnlinkedCustomer_WithHashedPassword()
    {
        var user = await _sut.RegisterAsync("  new.customer  ", "Secret123");

        Assert.Equal("new.customer", user.Username);
        Assert.Equal(UserRole.Customer, user.Role);
        Assert.Null(user.ClientId);
        Assert.NotEqual("Secret123", user.PasswordHash);
        Assert.NotNull(await _sut.ValidateCredentialsAsync("NEW.CUSTOMER", "Secret123")); // usernames are case-insensitive
    }

    [Fact]
    public async Task Register_DuplicateUsername_ThrowsConflict()
    {
        await Assert.ThrowsAsync<ConflictException>(() => _sut.RegisterAsync("Admin", "Secret123"));
    }

    [Theory]
    [InlineData("Sh0rt")]
    [InlineData("nouppercase1")]
    [InlineData("NOLOWERCASE1")]
    [InlineData("NoDigitsHere")]
    [InlineData("")]
    public async Task Register_WeakPassword_Throws(string password)
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.RegisterAsync("someone", password));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    public async Task Register_InvalidUsername_Throws(string username)
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.RegisterAsync(username, "Secret123"));
    }

    [Fact]
    public async Task ValidateCredentials_WrongPassword_ReturnsNull()
    {
        Assert.Null(await _sut.ValidateCredentialsAsync("admin", "wrong"));
        Assert.NotNull(await _sut.ValidateCredentialsAsync("admin", "Admin@123"));
    }

    [Fact]
    public async Task UpdateAccess_LinkCustomerToClient()
    {
        var user = await _sut.RegisterAsync("customer2", "Secret123");

        var updated = await _sut.UpdateAccessAsync(user.Id, UserRole.Customer, 2, actingUserId: 1);

        Assert.Equal(2, updated.ClientId);
    }

    [Fact]
    public async Task UpdateAccess_PromotingToStaff_RemovesClientLink()
    {
        var updated = await _sut.UpdateAccessAsync(3, UserRole.Staff, 1, actingUserId: 1);

        Assert.Equal(UserRole.Staff, updated.Role);
        Assert.Null(updated.ClientId);
    }

    [Fact]
    public async Task UpdateAccess_UnknownClient_Throws()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.UpdateAccessAsync(3, UserRole.Customer, 999, actingUserId: 1));
    }

    [Fact]
    public async Task UpdateAccess_CannotChangeOwnRole()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.UpdateAccessAsync(1, UserRole.Staff, null, actingUserId: 1));
    }

    [Fact]
    public async Task UpdateAccess_CannotDemoteTheLastAdmin()
    {
        // Staff user 2 tries (via an admin session that isn't admin 1) - only one Admin exists.
        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.UpdateAccessAsync(1, UserRole.Staff, null, actingUserId: 99));
    }

    [Fact]
    public async Task Delete_CannotDeleteSelf_OrLastAdmin()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.DeleteAsync(1, actingUserId: 1));
        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.DeleteAsync(1, actingUserId: 99));
    }

    [Fact]
    public async Task Delete_RemovesUser()
    {
        await _sut.DeleteAsync(2, actingUserId: 1);

        Assert.Null(await _sut.ValidateCredentialsAsync("staff", "Staff@123"));
    }
}
