using System.ComponentModel.DataAnnotations;
using ChristinaTicketingSystem.Api.Models;
using Microsoft.AspNetCore.Http;

namespace ChristinaTicketingSystem.Api.Models.Dtos;

public class TicketCreateDto
{
    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? AssignedTo { get; set; }

    public DateTime? DueDate { get; set; }

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    [MaxLength(1000)]
    public string? Overview { get; set; }

    [MaxLength(1000)]
    public string? ReviewNotes { get; set; }

    public IFormFile? Attachment { get; set; }
}

public class TicketUpdateDto
{
    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? AssignedTo { get; set; }

    [Required]
    public TicketStatus Status { get; set; }

    public DateTime? DueDate { get; set; }

    [Required]
    public TicketPriority Priority { get; set; }

    [MaxLength(1000)]
    public string? Overview { get; set; }

    [MaxLength(1000)]
    public string? ReviewNotes { get; set; }
}

public class TicketReadDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CreatedByUsername { get; set; } = string.Empty;
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public string CreatedByRole { get; set; } = string.Empty;
    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? AssignedTo { get; set; }
    public string? Overview { get; set; }
    public string? ReviewNotes { get; set; }
    public bool HasAttachment { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentContentType { get; set; }
    public string? AttachmentUrl { get; set; }
    public List<TicketCommentReadDto> Comments { get; set; } = [];
}

public class TicketStatusUpdateDto
{
    [Required]
    public TicketStatus Status { get; set; }
}

public class TicketCommentCreateDto
{
    [Required]
    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;
}

public class TicketCommentReadDto
{
    public string AuthorName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

