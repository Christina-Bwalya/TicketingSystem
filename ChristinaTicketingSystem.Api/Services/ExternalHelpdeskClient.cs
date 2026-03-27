using System.Net.Http.Headers;
using System.Security.Cryptography;
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
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
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
    /// Forward a ticket to the external system. Returns their ticket ref and callback URL, or null on failure.
    /// Never throws — failures are logged and swallowed.
    /// </summary>
    public async Task<(string ExternalRef, string CallbackUrl)?> ForwardTicketAsync(Ticket ticket)
    {
        var payload = new OutboundTicketPayload
        {
            ExternalTicketRef = $"TKT-{ticket.Id}",
            Title = ticket.Title,
            Description = ticket.Description,
            Category = ticket.Category.ToUpperInvariant(),
            Priority = MapPriorityOutbound(ticket.TicketPriority),
            SubmittedBy = new SubmittedByDto
            {
                FullName = ticket.CreatedByDisplayName,
                Email = $"{ticket.CreatedByUsername}@internal"
            },
            CreatedAt = ticket.CreatedDate
        };

        try
        {
            var response = await SendAsync(
                HttpMethod.Post,
                "/api/integration/tickets/inbound",
                payload);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Forward ticket {TicketId} failed: {Status} | Response: {Body}", ticket.Id, response.StatusCode, errorBody);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(body);

            // Their response shape: { "externalTicketRef": "...", "status": "OPEN", "callbackUrl": "..." }
            string externalRef;
            string callbackUrl;

            if (result.TryGetProperty("externalTicketRef", out var refProp))
                externalRef = refProp.GetString() ?? string.Empty;
            else if (result.TryGetProperty("ticketNumber", out var numProp))
                externalRef = numProp.GetString() ?? string.Empty;
            else
                externalRef = $"EXT-{ticket.Id}";

            if (result.TryGetProperty("callbackUrl", out var cbProp))
                callbackUrl = cbProp.GetString() ?? string.Empty;
            else
                callbackUrl = string.Empty;

            _logger.LogInformation("Ticket {TicketId} forwarded → external ref {ExternalRef} | body: {Body}", ticket.Id, externalRef, body);
            return (externalRef, callbackUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Forward ticket {TicketId} threw an exception", ticket.Id);
            return null;
        }
    }

    /// <summary>Push a status update to the external system for a forwarded ticket.</summary>
    public async Task PushStatusUpdateAsync(Ticket ticket, string updatedByName)
    {
        if (string.IsNullOrWhiteSpace(ticket.ExternalCallbackUrl)) return;

        var payload = new OutboundStatusUpdatePayload
        {
            Status = MapStatusOutbound(ticket.TicketStatus),
            UpdatedBy = new UpdatedByDto { FullName = updatedByName, Email = $"{updatedByName}@internal" },
            Timestamp = DateTime.UtcNow
        };

        try
        {
            // callbackUrl is their full URL e.g. https://their-system/api/integration/tickets/EXT-123
            var url = ticket.ExternalCallbackUrl.TrimEnd('/') + "/status";
            var response = await SendToAbsoluteUrlAsync(HttpMethod.Patch, url, payload);

            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Push status for ticket {TicketId} failed: {Status}", ticket.Id, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Push status for ticket {TicketId} threw an exception", ticket.Id);
        }
    }

    /// <summary>Push a comment to the external system for a forwarded ticket.</summary>
    public async Task PushCommentAsync(Ticket ticket, string message, string authorName)
    {
        if (string.IsNullOrWhiteSpace(ticket.ExternalCallbackUrl)) return;

        var payload = new OutboundCommentPayload
        {
            Message = message,
            IsInternal = false,
            Author = new AuthorDto { FullName = authorName, Email = $"{authorName}@internal" },
            Timestamp = DateTime.UtcNow
        };

        try
        {
            var url = ticket.ExternalCallbackUrl.TrimEnd('/') + "/comments";
            var response = await SendToAbsoluteUrlAsync(HttpMethod.Post, url, payload);

            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Push comment for ticket {TicketId} failed: {Status}", ticket.Id, response.StatusCode);
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
        return await SendToAbsoluteUrlAsync(method, url, payload);
    }

    private async Task<HttpResponseMessage> SendToAbsoluteUrlAsync(HttpMethod method, string url, object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.Add("X-Webhook-Signature", ComputeSignature(json, _options.WebhookSecret));

        return await _http.SendAsync(request);
    }

    private static string ComputeSignature(string body, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(body);
        var hash = HMACSHA256.HashData(key, data);
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string MapPriorityOutbound(TicketPriority priority) => priority switch
    {
        TicketPriority.Low => "LOW",
        TicketPriority.Medium => "MEDIUM",
        TicketPriority.High => "HIGH",
        TicketPriority.Critical => "CRITICAL",
        _ => "MEDIUM"
    };

    private static string MapStatusOutbound(TicketStatus status) => status switch
    {
        TicketStatus.Open => "OPEN",
        TicketStatus.InProgress => "IN_PROGRESS",
        TicketStatus.WaitingForUser => "IN_PROGRESS",
        TicketStatus.Resolved => "RESOLVED",
        TicketStatus.Closed => "CLOSED",
        _ => "OPEN"
    };
}
