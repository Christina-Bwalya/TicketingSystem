using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ChristinaTicketingSystem.Api.Models;

public enum TicketStatus
{
    Open = 0,
    InProgress = 1,
    WaitingForUser = 2,
    Resolved = 3,
    Closed = 4
}

public enum TicketPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

[Table("tickets")]
public class Ticket : BaseModel
{
    [PrimaryKey("id", false)]
    public int Id { get; set; }

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("category")]
    public string Category { get; set; } = string.Empty;

    [Column("created_by_username")]
    public string CreatedByUsername { get; set; } = string.Empty;

    [Column("created_by_display_name")]
    public string CreatedByDisplayName { get; set; } = string.Empty;

    [Column("created_by_role")]
    public string CreatedByRole { get; set; } = "Customer";

    [Column("status")]
    public int Status { get; set; } = (int)TicketStatus.Open;

    [Column("priority")]
    public int Priority { get; set; } = (int)TicketPriority.Medium;

    [Column("created_date")]
    public DateTime CreatedDate { get; set; }

    [Column("due_date")]
    public DateTime? DueDate { get; set; }

    [Column("assigned_to")]
    public string? AssignedTo { get; set; }

    [Column("overview")]
    public string? Overview { get; set; }

    [Column("review_notes")]
    public string? ReviewNotes { get; set; }

    [Column("attachment_file_name")]
    public string? AttachmentFileName { get; set; }

    [Column("attachment_stored_file_name")]
    public string? AttachmentStoredFileName { get; set; }

    [Column("attachment_content_type")]
    public string? AttachmentContentType { get; set; }

    [Column("attachment_relative_path")]
    public string? AttachmentRelativePath { get; set; }

    // Not mapped — loaded separately
    public List<TicketComment> Comments { get; set; } = [];

    public TicketStatus TicketStatus => (TicketStatus)Status;
    public TicketPriority TicketPriority => (TicketPriority)Priority;
}

[Table("ticket_comments")]
public class TicketComment : BaseModel
{
    [PrimaryKey("id", false)]
    public int Id { get; set; }

    [Column("ticket_id")]
    public int TicketId { get; set; }

    [Column("author_name")]
    public string AuthorName { get; set; } = string.Empty;

    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [Column("created_date")]
    public DateTime CreatedDate { get; set; }
}
