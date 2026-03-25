using ChristinaTicketingSystem.Api.Data;
using ChristinaTicketingSystem.Api.Models;
using Microsoft.Extensions.Options;

namespace ChristinaTicketingSystem.Api.Services;

public class DatabaseSeeder
{
    private readonly SupabaseService _supabase;
    private readonly PasswordService _passwordService;
    private readonly AuthSettings _authSettings;

    public DatabaseSeeder(
        SupabaseService supabase,
        PasswordService passwordService,
        IOptions<AuthSettings> authOptions)
    {
        _supabase = supabase;
        _passwordService = passwordService;
        _authSettings = authOptions.Value;
    }

    public async Task InitializeAsync()
    {
        foreach (var seededUser in _authSettings.SeedUsers
                     .GroupBy(u => u.Username.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Select(g => g.First()))
        {
            var username = seededUser.Username.Trim();

            var existing = await _supabase.Client
                .From<AuthUser>()
                .Filter("username", Supabase.Postgrest.Constants.Operator.Equals, username)
                .Single();

            if (existing is not null)
                continue;

            await _supabase.Client
                .From<AuthUser>()
                .Insert(new AuthUser
                {
                    Username = username,
                    DisplayName = seededUser.DisplayName.Trim(),
                    Role = seededUser.Role,
                    PasswordHash = _passwordService.HashPassword(seededUser.Password),
                    CreatedAtUtc = DateTime.UtcNow
                });
        }
    }
}
