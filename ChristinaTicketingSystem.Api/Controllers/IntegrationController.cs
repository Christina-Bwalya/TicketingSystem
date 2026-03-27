using System.Security.Cryptography;
using System.Text;
using ChristinaTicketingSystem.Api.Data;
using ChristinaTicketingSystem.Api.Models;
using ChristinaTicketingSystem.Api.Models.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ChristinaTicketingSystem.Api.Controllers;

[ApiController]
[Route("api/integration")]
public class IntegrationController : ControllerBase
{
    private readonly SupabaseService _supabase;
    private readonly HelpdeskOptions _options;
    private readonly ILogger<IntegrationController> _logger;

    public IntegrationController(
        SupabaseService supabase,
        IOptions<HelpdeskOptions> options,
        ILogger<IntegrationController> logger)
    {
        _supabase = supabase;
        _options = options.Value;
        _logger = logger;
    }

    // ── 1. External system forwards a ticket to us ────────────────────────

    [HttpPost("tickets/inbound")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<InboundTicketResponse>> ReceiveInboundTicket(
        [FromBody] InboundTicketPayload dto)
    {
        if (!await VerifySignatureAsync()) return Unauthorized();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var category = dto.Category.Trim().ToUpperInvariant();
        if (!_options.InboundCategories.Contains(category))
            return BadRequest(new { error = $"Category '{dto.Category}' is not handled by this system. Valid: {string.Join(", ", _options.InboundCategories)}" });

        var ticket = new Ticket
        {
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            Category = category,
            CreatedByUsername = dto.SubmittedBy.Email,
            CreatedByDisplayName = dto.SubmittedBy.FullName,
            CreatedByRole = "Customer",
            Status = (int)TicketStatus.Open,
            Priority = (int)MapPriorityInbound(dto.Priority),
            CreatedDate = dto.CreatedAt,
            ExternalTicketRef = dto.ExternalTicketRef,
            ExternalSource = "inbound"
        };

        var result = await _supabase.Client.From<Ticket>().Insert(ticket);
        var created = result.Models.FirstOrDefault() ?? ticket;

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var callbackUrl = $"{baseUrl}/api/integration/tickets/TKT-{created.Id}";

        _logger.LogInformation("Inbound ticket TKT-{Id} created from external ref {Ref}", created.Id, dto.ExternalTicketRef);

        return Created(callbackUrl, new InboundTicketResponse
        {
            TicketNumber = $"TKT-{created.Id}",
            Status = "OPEN",
            CallbackUrl = callbackUrl
        });
    }

    // ── 2. External system pushes a status update ─────────────────────────

    [HttpPatch("tickets/{ticketNumber}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        string ticketNumber,
        [FromBody] InboundStatusUpdatePayload dto)
    {
        if (!await VerifySignatureAsync()) return Unauthorized();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var ticket = await FindByTicketNumberAsync(ticketNumber);
        if (ticket is null) return NotFound(new { error = $"Ticket {ticketNumber} not found." });

        var mapped = MapStatusInbound(dto.Status);
        if (mapped is null)
        {
            _logger.LogWarning("Unknown inbound status '{Status}' for ticket {TicketNumber}", dto.Status, ticketNumber);
            return BadRequest(new { error = $"Unknown status value '{dto.Status}'." });
        }

        ticket.Status = (int)mapped.Value;
        await _supabase.Client.From<Ticket>().Update(ticket);

        _logger.LogInformation("Ticket {TicketNumber} status updated to {Status} by external system", ticketNumber, dto.Status);

        return Ok(new { ticketNumber, status = dto.Status.ToUpperInvariant() });
    }

    // ── 3. External system pushes a comment ──────────────────────────────

    [HttpPost("tickets/{ticketNumber}/comments")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddComment(
        string ticketNumber,
        [FromBody] InboundCommentPayload dto)
    {
        if (!await VerifySignatureAsync()) return Unauthorized();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var ticket = await FindByTicketNumberAsync(ticketNumber);
        if (ticket is null) return NotFound(new { error = $"Ticket {ticketNumber} not found." });

        var comment = new TicketComment
        {
            TicketId = ticket.Id,
            AuthorName = $"{dto.Author.FullName} (External)",
            Message = dto.Message.Trim(),
            CreatedDate = dto.Timestamp
        };

        var result = await _supabase.Client.From<TicketComment>().Insert(comment);
        var created = result.Models.FirstOrDefault() ?? comment;

        _logger.LogInformation("Comment added to ticket {TicketNumber} by external agent {Author}", ticketNumber, dto.Author.FullName);

        return Created(string.Empty, new
        {
            commentId = created.Id.ToString(),
            ticketNumber,
            createdAt = created.CreatedDate
        });
    }

    // ── 4. External system fetches current ticket state ───────────────────

    [HttpGet("tickets/{ticketNumber}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicket(string ticketNumber)
    {
        if (!ValidateApiKey()) return Unauthorized();

        var ticket = await FindByTicketNumberAsync(ticketNumber);
        if (ticket is null) return NotFound(new { error = $"Ticket {ticketNumber} not found." });

        return Ok(new
        {
            ticketNumber,
            title = ticket.Title,
            description = ticket.Description,
            category = ticket.Category,
            priority = ticket.TicketPriority.ToString().ToUpperInvariant(),
            status = MapStatusOutbound(ticket.TicketStatus),
            createdAt = ticket.CreatedDate,
            updatedAt = ticket.CreatedDate
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<Ticket?> FindByTicketNumberAsync(string ticketNumber)
    {
        // ticketNumber format: TKT-{id}
        if (!ticketNumber.StartsWith("TKT-", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(ticketNumber[4..], out var id))
            return null;

        return await _supabase.Client.From<Ticket>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, id)
            .Single();
    }

    private async Task<bool> VerifySignatureAsync()
    {
        // Also accept plain Bearer API key for GET requests
        if (ValidateApiKey()) return true;

        if (!Request.Headers.TryGetValue("X-Webhook-Signature", out var sigHeader))
            return false;

        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        var key = Encoding.UTF8.GetBytes(_options.InboundSecret);
        var data = Encoding.UTF8.GetBytes(body);
        var expected = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(key, data)).ToLowerInvariant();

        return string.Equals(sigHeader.ToString(), expected, StringComparison.OrdinalIgnoreCase);
    }

    private bool ValidateApiKey()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
        var key = header["Bearer ".Length..].Trim();
        return string.Equals(key, _options.ApiKey, StringComparison.Ordinal);
    }

    private static TicketPriority MapPriorityInbound(string priority) => priority.ToUpperInvariant() switch
    {
        "LOW" => TicketPriority.Low,
        "HIGH" => TicketPriority.High,
        "CRITICAL" => TicketPriority.Critical,
        _ => TicketPriority.Medium
    };

    private static TicketStatus? MapStatusInbound(string status) => status.ToUpperInvariant() switch
    {
        "OPEN" => TicketStatus.Open,
        "ASSIGNED" => TicketStatus.InProgress,
        "IN_PROGRESS" => TicketStatus.InProgress,
        "RESOLVED" => TicketStatus.Resolved,
        "CLOSED" => TicketStatus.Closed,
        _ => null
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
