using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ChristinaTicketingSystem.Api.Models;

[Table("users")]
public class AuthUser : BaseModel
{
    [PrimaryKey("id", false)]
    public int Id { get; set; }

    [Column("username")]
    public string Username { get; set; } = string.Empty;

    [Column("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [Column("role")]
    public string Role { get; set; } = "Customer";

    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }
}
