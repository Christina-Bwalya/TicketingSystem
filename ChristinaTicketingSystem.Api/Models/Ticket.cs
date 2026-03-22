using System.ComponentModel.DataAnnotations;

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

public class Ticket
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CreatedByUsername { get; set; } = string.Empty;
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public string CreatedByRole { get; set; } = "Customer";
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public DateTime CreatedDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? AssignedTo { get; set; }
    public string? Overview { get; set; }
    public string? ReviewNotes { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentStoredFileName { get; set; }
    public string? AttachmentContentType { get; set; }
    public string? AttachmentRelativePath { get; set; }
    public List<TicketComment> Comments { get; set; } = [];
}

public class TicketComment
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    [MaxLength(100)]
    public string AuthorName { get; set; } = string.Empty;
    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
