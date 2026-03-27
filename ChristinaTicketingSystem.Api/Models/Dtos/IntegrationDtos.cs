using System.ComponentModel.DataAnnotations;

namespace ChristinaTicketingSystem.Api.Models.Dtos;

// ── Outbound: what we send to the external system when forwarding a ticket ──

public class OutboundTicketPayload
{
    public string ExternalTicketRef { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public SubmittedByDto SubmittedBy { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class SubmittedByDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// ── Outbound: status update we push to them ──

public class OutboundStatusUpdatePayload
{
    public string Status { get; set; } = string.Empty;
    public UpdatedByDto UpdatedBy { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class UpdatedByDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// ── Outbound: comment we push to them ──

public class OutboundCommentPayload
{
    public string Message { get; set; } = string.Empty;
    public bool IsInternal { get; set; } = false;
    public AuthorDto Author { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class AuthorDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// ── Inbound: ticket they forward to us ──

public class InboundTicketPayload
{
    [Required] public string ExternalTicketRef { get; set; } = string.Empty;
    [Required] public string Title { get; set; } = string.Empty;
    [Required] public string Description { get; set; } = string.Empty;
    [Required] public string Category { get; set; } = string.Empty;
    [Required] public string Priority { get; set; } = string.Empty;
    [Required] public InboundSubmittedByDto SubmittedBy { get; set; } = new();
    [Required] public DateTime CreatedAt { get; set; }
}

public class InboundSubmittedByDto
{
    [Required] public string FullName { get; set; } = string.Empty;
    [Required] public string Email { get; set; } = string.Empty;
}

// ── Inbound: status update they push to us ──

public class InboundStatusUpdatePayload
{
    [Required] public string Status { get; set; } = string.Empty;
    [Required] public InboundUpdatedByDto UpdatedBy { get; set; } = new();
    [Required] public DateTime Timestamp { get; set; }
}

public class InboundUpdatedByDto
{
    [Required] public string FullName { get; set; } = string.Empty;
    [Required] public string Email { get; set; } = string.Empty;
}

// ── Inbound: comment they push to us ──

public class InboundCommentPayload
{
    [Required] public string Message { get; set; } = string.Empty;
    public bool IsInternal { get; set; } = false;
    [Required] public InboundAuthorDto Author { get; set; } = new();
    [Required] public DateTime Timestamp { get; set; }
}

public class InboundAuthorDto
{
    [Required] public string FullName { get; set; } = string.Empty;
    [Required] public string Email { get; set; } = string.Empty;
}

// ── Response: what we return when they create a ticket with us ──

public class InboundTicketResponse
{
    public string TicketNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
}
