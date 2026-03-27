using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ChristinaTicketingSystem.Api.Models;
using ChristinaTicketingSystem.Api.Models.Dtos;
using Microsoft.Extensions.Options;

namespace ChristinaTicketingSystem.Api.Services;

public class ExternalHelpdeskClient
{
    private readonly HttpClient _http;
    private readonly HelpdeskOptions _options;
    private readonly ILogger<ExternalHelpdeskClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public ExternalHelpdeskClient(
        HttpClient http,
        IOptions<HelpdeskOptions> options,
        ILogger<ExternalHelpdeskClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Forward a ticket to the external system.
    /// Returns their external_id, or null on failure. Never throws.
    /// </summary>
    public async Task<string?> ForwardTicketAsync(Ticket ticket)
    {
        var payload = new OutboundTicketPayload
        {
            ExternalId = $"TKT-{ticket.Id}",
            Title = ticket.Title,
            Description = ticket.Description,
            Category = ticket.Category,
            Priority = MapPriorityOutbound(ticket.TicketPriority),
            CreatedBy = ticket.CreatedByUsername,
            CreatedDate = ticket.CreatedDate
        };

        try
        {
            var response = await SendAsync(HttpMethod.Post, "/api/integration/tickets/inbound", payload);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Forward ticket {TicketId} failed: {Status} | {Body}", ticket.Id, response.StatusCode, body);
                return null;
            }

            var result = JsonSerializer.Deserialize<JsonElement>(body);

            // Their response: { "external_id": "their-ref" }
            var externalId = result.TryGetProperty("external_id", out var prop)
                ? prop.GetString()
                : null;

            _logger.LogInformation("Ticket {TicketId} forwarded → their ref: {ExternalId}", ticket.Id, externalId);
            return externalId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Forward ticket {TicketId} threw an exception", ticket.Id);
            return null;
        }
    }

    /// <summary>Push a status update to the external system for a forwarded ticket.</summary>
    public async Task PushStatusUpdateAsync(Ticket ticket, string updatedBy)
    {
        if (string.IsNullOrWhiteSpace(ticket.ExternalTicketRef)) return;

        var payload = new OutboundStatusUpdatePayload
        {
            NewStatus = MapStatusOutbound(ticket.TicketStatus),
            UpdatedBy = updatedBy,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            var path = $"/api/integration/tickets/{ticket.ExternalTicketRef}/status";
            var response = await SendAsync(HttpMethod.Patch, path, payload);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Push status for ticket {TicketId} failed: {Status} | {Body}", ticket.Id, response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Push status for ticket {TicketId} threw an exception", ticket.Id);
        }
    }

    /// <summary>Push a comment to the external system for a forwarded ticket.</summary>
    public async Task PushCommentAsync(Ticket ticket, string message, string authorName)
    {
        if (string.IsNullOrWhiteSpace(ticket.ExternalTicketRef)) return;

        var payload = new OutboundCommentPayload
        {
            CommentAuthor = authorName,
            CommentMessage = message,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            var path = $"/api/integration/tickets/{ticket.ExternalTicketRef}/comments";
            var response = await SendAsync(HttpMethod.Post, path, payload);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Push comment for ticket {TicketId} failed: {Status} | {Body}", ticket.Id, response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Push comment for ticket {TicketId} threw an exception", ticket.Id);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object payload)
    {
        var url = _options.BaseUrl.TrimEnd('/') + path;
        var json = JsonSerializer.Serialize(payload, JsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        return await _http.SendAsync(request);
    }

    private static string MapPriorityOutbound(TicketPriority priority) => priority switch
    {
        TicketPriority.Low => "Low",
        TicketPriority.High => "High",
        TicketPriority.Critical => "Critical",
        _ => "Medium"
    };

    private static string MapStatusOutbound(TicketStatus status) => status switch
    {
        TicketStatus.Open => "open",
        TicketStatus.InProgress => "in_progress",
        TicketStatus.WaitingForUser => "in_progress",
        TicketStatus.Resolved => "resolved",
        TicketStatus.Closed => "closed",
        _ => "open"
    };
}
