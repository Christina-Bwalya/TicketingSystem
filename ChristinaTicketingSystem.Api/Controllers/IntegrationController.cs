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
        if (!ValidateApiKey()) return Unauthorized(new { error = "Invalid or missing API key." });
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var category = dto.Category.Trim();
        if (!_options.InboundCategories.Contains(category))
            return BadRequest(new { error = $"Category '{dto.Category}' is not handled by this system. Valid: {string.Join(", ", _options.InboundCategories)}" });

        var ticket = new Ticket
        {
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            Category = category,
            CreatedByUsername = dto.CreatedBy,
            CreatedByDisplayName = dto.CreatedBy,
            CreatedByRole = "Customer",
            Status = (int)TicketStatus.Open,
            Priority = (int)MapPriorityInbound(dto.Priority),
            CreatedDate = dto.CreatedDate == default ? DateTime.UtcNow : dto.CreatedDate,
            ExternalTicketRef = dto.ExternalId,
            ExternalSource = "inbound"
        };

        var result = await _supabase.Client.From<Ticket>().Insert(ticket);
        var created = result.Models.FirstOrDefault() ?? ticket;

        _logger.LogInformation("Inbound ticket TKT-{Id} created from external ref {Ref}", created.Id, dto.ExternalId);

        return Created(string.Empty, new InboundTicketResponse
        {
            ExternalId = $"TKT-{created.Id}"
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
        if (!ValidateApiKey()) return Unauthorized(new { error = "Invalid or missing API key." });
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var ticket = await FindByTicketNumberAsync(ticketNumber);
        if (ticket is null) return NotFound(new { error = $"Ticket {ticketNumber} not found." });

        var mapped = MapStatusInbound(dto.NewStatus);
        if (mapped is null)
        {
            _logger.LogWarning("Unknown inbound status '{Status}' for ticket {TicketNumber}", dto.NewStatus, ticketNumber);
            return BadRequest(new { error = $"Unknown status value '{dto.NewStatus}'. Valid: open, in_progress, resolved, closed" });
        }

        ticket.Status = (int)mapped.Value;
        await _supabase.Client.From<Ticket>().Update(ticket);

        _logger.LogInformation("Ticket {TicketNumber} status updated to {Status} by external system", ticketNumber, dto.NewStatus);

        return Ok(new { message = "Update applied." });
    }

    // ── 3. External system pushes a comment ──────────────────────────────

    [HttpPost("tickets/{ticketNumber}/comments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddComment(
        string ticketNumber,
        [FromBody] InboundCommentPayload dto)
    {
        if (!ValidateApiKey()) return Unauthorized(new { error = "Invalid or missing API key." });
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var ticket = await FindByTicketNumberAsync(ticketNumber);
        if (ticket is null) return NotFound(new { error = $"Ticket {ticketNumber} not found." });

        var comment = new TicketComment
        {
            TicketId = ticket.Id,
            AuthorName = $"{dto.CommentAuthor} (External)",
            Message = dto.CommentMessage.Trim(),
            CreatedDate = dto.Timestamp ?? DateTime.UtcNow
        };

        await _supabase.Client.From<TicketComment>().Insert(comment);

        _logger.LogInformation("Comment added to ticket {TicketNumber} by external agent {Author}", ticketNumber, dto.CommentAuthor);

        return Ok(new { message = "Update applied." });
    }

    // ── 4. External system fetches current ticket state ───────────────────

    [HttpGet("tickets/{ticketNumber}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTicket(string ticketNumber)
    {
        if (!ValidateApiKey()) return Unauthorized(new { error = "Invalid or missing API key." });

        var ticket = await FindByTicketNumberAsync(ticketNumber);
        if (ticket is null) return NotFound(new { error = $"Ticket {ticketNumber} not found." });

        return Ok(new
        {
            ticketNumber,
            title = ticket.Title,
            description = ticket.Description,
            category = ticket.Category,
            priority = MapPriorityOutbound(ticket.TicketPriority),
            status = MapStatusOutbound(ticket.TicketStatus),
            createdAt = ticket.CreatedDate,
            updatedAt = ticket.CreatedDate
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<Ticket?> FindByTicketNumberAsync(string ticketNumber)
    {
        if (!ticketNumber.StartsWith("TKT-", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(ticketNumber[4..], out var id))
            return null;

        return await _supabase.Client.From<Ticket>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, id)
            .Single();
    }

    private bool ValidateApiKey()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
        var key = header["Bearer ".Length..].Trim();
        return string.Equals(key, _options.InboundSecret, StringComparison.Ordinal);
    }

    private static TicketPriority MapPriorityInbound(string priority) => priority switch
    {
        "Low" => TicketPriority.Low,
        "High" => TicketPriority.High,
        "Critical" => TicketPriority.Critical,
        _ => TicketPriority.Medium
    };

    private static string MapPriorityOutbound(TicketPriority priority) => priority switch
    {
        TicketPriority.Low => "Low",
        TicketPriority.High => "High",
        TicketPriority.Critical => "Critical",
        _ => "Medium"
    };

    private static TicketStatus? MapStatusInbound(string status) => status.ToLowerInvariant() switch
    {
        "open" => TicketStatus.Open,
        "in_progress" => TicketStatus.InProgress,
        "resolved" => TicketStatus.Resolved,
        "closed" => TicketStatus.Closed,
        _ => null
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
