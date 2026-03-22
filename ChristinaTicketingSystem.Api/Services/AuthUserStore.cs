using ChristinaTicketingSystem.Api.Data;
using ChristinaTicketingSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChristinaTicketingSystem.Api.Services;

public class AuthUserStore
{
    private readonly AppDbContext _dbContext;
    private readonly PasswordService _passwordService;

    public AuthUserStore(AppDbContext dbContext, PasswordService passwordService)
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
    }

    public async Task<AuthUser?> TryGetUserAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        return await _dbContext.Users
            .SingleOrDefaultAsync(user => user.Username == username.Trim());
    }

    public async Task<bool> RegisterUserAsync(string username, string displayName, string password, string role = "Customer")
    {
        var normalizedUsername = username.Trim();
        var normalizedDisplayName = displayName.Trim();

        var exists = await _dbContext.Users
            .AnyAsync(user => user.Username == normalizedUsername);

        if (exists)
        {
            return false;
        }

        _dbContext.Users.Add(new AuthUser
        {
            Username = normalizedUsername,
            DisplayName = normalizedDisplayName,
            Role = role,
            PasswordHash = _passwordService.HashPassword(password),
            CreatedAtUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();
        return true;
    }
}
