using ChristinaTicketingSystem.Api.Data;
using ChristinaTicketingSystem.Api.Models;

namespace ChristinaTicketingSystem.Api.Services;

public class AuthUserStore
{
    private readonly SupabaseService _supabase;
    private readonly PasswordService _passwordService;

    public AuthUserStore(SupabaseService supabase, PasswordService passwordService)
    {
        _supabase = supabase;
        _passwordService = passwordService;
    }

    public async Task<AuthUser?> TryGetUserAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        var result = await _supabase.Client
            .From<AuthUser>()
            .Filter("username", Supabase.Postgrest.Constants.Operator.Equals, username.Trim())
            .Single();

        return result;
    }

    public async Task<bool> RegisterUserAsync(string username, string displayName, string password, string role = "Customer")
    {
        var normalizedUsername = username.Trim();

        var existing = await _supabase.Client
            .From<AuthUser>()
            .Filter("username", Supabase.Postgrest.Constants.Operator.Equals, normalizedUsername)
            .Single();

        if (existing is not null)
            return false;

        await _supabase.Client
            .From<AuthUser>()
            .Insert(new AuthUser
            {
                Username = normalizedUsername,
                DisplayName = displayName.Trim(),
                Role = role,
                PasswordHash = _passwordService.HashPassword(password),
                CreatedAtUtc = DateTime.UtcNow
            });

        return true;
    }
}
