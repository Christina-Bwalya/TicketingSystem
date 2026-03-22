using System.ComponentModel.DataAnnotations;

namespace ChristinaTicketingSystem.Api.Models;

public class AuthUser
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Role { get; set; } = "Customer";
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
