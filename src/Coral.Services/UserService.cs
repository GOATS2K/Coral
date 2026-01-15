using Coral.Database;
using Coral.Database.Models;
using Coral.Services.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Coral.Services;

public interface IUserService
{
    Task<User> CreateUserAsync(string username, string password);
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<User?> GetUserByUsernameAsync(string username);
    Task<bool> IsFirstUserAsync();
    PasswordVerificationResult ValidatePassword(User user, string password);
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
}

public class UserService : IUserService
{
    private readonly CoralDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;

    public UserService(CoralDbContext context, IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<User> CreateUserAsync(string username, string password)
    {
        if (!RegexPatterns.Username().IsMatch(username))
        {
            throw new InvalidUsernameException();
        }

        var isFirstUser = !await _context.Users.AnyAsync();

        var user = new User
        {
            Username = username,
            PasswordHash = _passwordHasher.HashPassword(null!, password),
            Role = isFirstUser ? UserRole.Admin : UserRole.User
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users.FindAsync(userId);
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<bool> IsFirstUserAsync()
    {
        return !await _context.Users.AnyAsync();
    }

    public PasswordVerificationResult ValidatePassword(User user, string password)
    {
        return _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return false;
        }

        var result = ValidatePassword(user, currentPassword);
        if (result == PasswordVerificationResult.Failed)
        {
            return false;
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        await _context.SaveChangesAsync();

        return true;
    }
}
