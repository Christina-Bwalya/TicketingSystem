using ChristinaTicketingSystem.Api.Data;
using ChristinaTicketingSystem.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChristinaTicketingSystem.Api.Services;

public class DatabaseSeeder
{
    private readonly AppDbContext _dbContext;
    private readonly PasswordService _passwordService;
    private readonly AuthSettings _authSettings;

    public DatabaseSeeder(
        AppDbContext dbContext,
        PasswordService passwordService,
        IOptions<AuthSettings> authOptions)
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
        _authSettings = authOptions.Value;
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.EnsureCreatedAsync();
        await EnsureTicketAttachmentColumnsAsync();

        foreach (var seededUser in _authSettings.SeedUsers
                     .GroupBy(user => user.Username.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First()))
        {
            var username = seededUser.Username.Trim();
            var exists = await _dbContext.Users.AnyAsync(user => user.Username == username);
            if (exists)
            {
                continue;
            }

            _dbContext.Users.Add(new AuthUser
            {
                Username = username,
                DisplayName = seededUser.DisplayName.Trim(),
                Role = seededUser.Role,
                PasswordHash = _passwordService.HashPassword(seededUser.Password),
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync();
    }

    private async Task EnsureTicketAttachmentColumnsAsync()
    {
        var connection = _dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var pragmaCommand = connection.CreateCommand();
            pragmaCommand.CommandText = "PRAGMA table_info('Tickets');";

            await using var reader = await pragmaCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                existingColumns.Add(reader.GetString(1));
            }

            var alterStatements = new List<string>();

            if (!existingColumns.Contains("AttachmentFileName"))
            {
                alterStatements.Add("ALTER TABLE Tickets ADD COLUMN AttachmentFileName TEXT NULL;");
            }

            if (!existingColumns.Contains("AttachmentStoredFileName"))
            {
                alterStatements.Add("ALTER TABLE Tickets ADD COLUMN AttachmentStoredFileName TEXT NULL;");
            }

            if (!existingColumns.Contains("AttachmentContentType"))
            {
                alterStatements.Add("ALTER TABLE Tickets ADD COLUMN AttachmentContentType TEXT NULL;");
            }

            if (!existingColumns.Contains("AttachmentRelativePath"))
            {
                alterStatements.Add("ALTER TABLE Tickets ADD COLUMN AttachmentRelativePath TEXT NULL;");
            }

            foreach (var statement in alterStatements)
            {
                await _dbContext.Database.ExecuteSqlRawAsync(statement);
            }
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
