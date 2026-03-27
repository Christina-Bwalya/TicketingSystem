using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ChristinaTicketingSystem.Api.Models.Dtos;

// ── Outbound: what we send to the external system when forwarding a ticket ──

public class OutboundTicketPayload
{
    [JsonPropertyName("external_id")]
    public string ExternalId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty;

    [JsonPropertyName("created_by")]
    public string CreatedBy { get; set; } = string.Empty;

    [JsonPropertyName("created_date")]
    public DateTime CreatedDate { get; set; }
}

// ── Outbound: status update we push to them ──

public class OutboundStatusUpdatePayload
{
    [JsonPropertyName("new_status")]
    public string NewStatus { get; set; } = string.Empty;

    [JsonPropertyName("updated_by")]
    public string UpdatedBy { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}

// ── Outbound: comment we push to them ──

public class OutboundCommentPayload
{
    [JsonPropertyName("comment_author")]
    public string CommentAuthor { get; set; } = string.Empty;

    [JsonPropertyName("comment_message")]
    public string CommentMessage { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}

// ── Inbound: ticket they forward to us ──

public class InboundTicketPayload
{
    [Required]
    [JsonPropertyName("external_id")]
    public string ExternalId { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty;

    [JsonPropertyName("created_by")]
    public string CreatedBy { get; set; } = string.Empty;

    [JsonPropertyName("created_date")]
    public DateTime CreatedDate { get; set; }
}

// ── Inbound: status update they push to us ──

public class InboundStatusUpdatePayload
{
    [Required]
    [JsonPropertyName("new_status")]
    public string NewStatus { get; set; } = string.Empty;

    [JsonPropertyName("updated_by")]
    public string? UpdatedBy { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }
}

// ── Inbound: comment they push to us ──

public class InboundCommentPayload
{
    [Required]
    [JsonPropertyName("comment_author")]
    public string CommentAuthor { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("comment_message")]
    public string CommentMessage { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }
}

// ── Response: what we return when they create a ticket with us ──

public class InboundTicketResponse
{
    [JsonPropertyName("external_id")]
    public string ExternalId { get; set; } = string.Empty;
}
