namespace ChristinaTicketingSystem.Api.Models;

public class AuthSettings
{
    public const string SectionName = "DemoAuth";

    public List<SeedUserSettings> SeedUsers { get; set; } =
    [
        new()
        {
            Username = "admin",
            Password = "Password123!",
            DisplayName = "Christina Admin",
            Role = "Admin"
        },
        new()
        {
            Username = "itsupport",
            Password = "IT123456!",
            DisplayName = "I.T Support",
            Role = "I.T"
        }
    ];
}

public class SeedUserSettings
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "Customer";
}
