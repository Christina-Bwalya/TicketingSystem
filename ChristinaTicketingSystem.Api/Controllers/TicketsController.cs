using ChristinaTicketingSystem.Api.Data;
using ChristinaTicketingSystem.Api.Models;
using ChristinaTicketingSystem.Api.Models.Dtos;
using ChristinaTicketingSystem.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace ChristinaTicketingSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;
    private const string BearerScheme = "Bearer ";
    private static readonly string UploadsFolder = Path.Combine("Uploads", "tickets");

    private static readonly HashSet<string> AllowedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp",
        ".pdf", ".txt", ".csv", ".doc", ".docx", ".xls", ".xlsx"
    };

    private readonly AuthSessionStore _sessionStore;
    private readonly SupabaseService _supabase;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<TicketsController> _logger;
    private readonly ExternalHelpdeskClient _helpdeskClient;
    private readonly HelpdeskOptions _helpdeskOptions;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public TicketsController(
        AuthSessionStore sessionStore,
        SupabaseService supabase,
        IWebHostEnvironment environment,
        ILogger<TicketsController> logger,
        ExternalHelpdeskClient helpdeskClient,
        IOptions<HelpdeskOptions> helpdeskOptions)
    {
        _sessionStore = sessionStore;
        _supabase = supabase;
        _environment = environment;
        _logger = logger;
        _helpdeskClient = helpdeskClient;
        _helpdeskOptions = helpdeskOptions.Value;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<TicketReadDto>>> GetAll([FromQuery] TicketStatus? status)
    {
        var unauthorized = EnsureAuthenticated(out var session);
        if (unauthorized is not null) return unauthorized;

        Supabase.Postgrest.Interfaces.IPostgrestTable<Ticket> ticketQuery = _supabase.Client.From<Ticket>();

        if (status.HasValue)
            ticketQuery = ticketQuery.Filter("status", Supabase.Postgrest.Constants.Operator.Equals, (int)status.Value);

        var ticketsResult = await ticketQuery.Get();
        var tickets = ticketsResult.Models;

        if (tickets.Count == 0)
            return Ok(Array.Empty<TicketReadDto>());

        // Load comments filtered by ticket IDs using in operator
        var ticketIds = tickets.Select(t => t.Id.ToString()).ToArray();
        var commentsResult = await _supabase.Client
            .From<TicketComment>()
            .Filter("ticket_id", Supabase.Postgrest.Constants.Operator.In, ticketIds)
            .Get();

        var commentsByTicket = commentsResult.Models
            .GroupBy(c => c.TicketId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var ticket in tickets)
            ticket.Comments = commentsByTicket.TryGetValue(ticket.Id, out var comments) ? comments : [];

        var visible = tickets
            .Where(t => CanAccessTicket(session!, t))
            .OrderByDescending(t => t.CreatedDate)
            .Select(ToReadDto)
            .ToList();

        return Ok(visible);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketReadDto>> GetById(int id)
    {
        var unauthorized = EnsureAuthenticated(out var session);
        if (unauthorized is not null) return unauthorized;

        var ticket = await GetTicketWithCommentsAsync(id);
        if (ticket is null) return NotFound();
        if (!CanAccessTicket(session!, ticket)) return Forbid();

        return Ok(ToReadDto(ticket));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Consumes("application/json")]
    public async Task<ActionResult<TicketReadDto>> CreateJson([FromBody] TicketCreateDto dto)
    {
        return await CreateTicketAsync(dto, null);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxAttachmentBytes + (1024 * 1024))]
    public async Task<ActionResult<TicketReadDto>> CreateWithAttachment([FromForm] TicketCreateDto dto)
    {
        return await CreateTicketAsync(dto, dto.Attachment);
    }

    private async Task<ActionResult<TicketReadDto>> CreateTicketAsync(TicketCreateDto dto, IFormFile? attachment)
    {
        var unauthorized = EnsureAuthenticated(out var session);
        if (unauthorized is not null) return unauthorized;

        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var ticket = new Ticket
        {
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            Category = dto.Category.Trim(),
            CreatedByUsername = session!.Username,
            CreatedByDisplayName = session.DisplayName,
            CreatedByRole = session.Role,
            AssignedTo = dto.AssignedTo?.Trim(),
            Status = (int)TicketStatus.Open,
            Priority = (int)dto.Priority,
            CreatedDate = DateTime.UtcNow,
            DueDate = CanManagePlanningFields(session) ? dto.DueDate : null,
            Overview = IsAdmin(session) ? dto.Overview?.Trim() : null,
            ReviewNotes = IsAdmin(session) ? dto.ReviewNotes?.Trim() : null
        };

        if (attachment is not null)
        {
            var validationMessage = ValidateAttachment(attachment);
            if (validationMessage is not null)
            {
                ModelState.AddModelError(nameof(dto.Attachment), validationMessage);
                return ValidationProblem(ModelState);
            }
            await SaveAttachmentAsync(ticket, attachment);
        }

        var insertResult = await _supabase.Client.From<Ticket>().Insert(ticket);
        var created = insertResult.Models.FirstOrDefault() ?? ticket;
        created.Comments = [];

        _logger.LogInformation("Ticket {TicketId} created by {Username}", created.Id, session.Username);

        // Fire-and-forget forward to external system if category matches
        if (_helpdeskOptions.ForwardCategories.Contains(created.Category))
        {
            _ = Task.Run(async () =>
            {
                var externalId = await _helpdeskClient.ForwardTicketAsync(created);
                if (externalId is not null)
                {
                    created.ExternalTicketRef = externalId;
                    created.ExternalCallbackUrl = $"{_helpdeskOptions.BaseUrl.TrimEnd('/')}/api/integration/tickets/{externalId}";
                    created.ExternalSource = "outbound";
                    await _supabase.Client.From<Ticket>().Update(created);
                }
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ToReadDto(created));
    }

    [HttpGet("{id:int}/attachment")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(int id)
    {
        var unauthorized = EnsureAuthenticated(out var session);
        if (unauthorized is not null) return unauthorized;

        var ticket = await _supabase.Client.From<Ticket>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, id)
            .Single();

        if (ticket is null) return NotFound();
        if (!CanAccessTicket(session!, ticket)) return Forbid();

        if (string.IsNullOrWhiteSpace(ticket.AttachmentRelativePath) ||
            string.IsNullOrWhiteSpace(ticket.AttachmentFileName))
            return NotFound();

        var absolutePath = Path.GetFullPath(
            Path.Combine(_environment.ContentRootPath, ticket.AttachmentRelativePath));

        // Path traversal protection — ensure resolved path is within uploads directory
        var allowedRoot = Path.GetFullPath(
            Path.Combine(_environment.ContentRootPath, UploadsFolder));

        if (!absolutePath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Path traversal attempt detected for ticket {TicketId} by {Username}", id, session!.Username);
            return NotFound();
        }

        if (!System.IO.File.Exists(absolutePath)) return NotFound();

        var contentType = string.IsNullOrWhiteSpace(ticket.AttachmentContentType)
            ? "application/octet-stream"
            : ticket.AttachmentContentType;

        return PhysicalFile(absolutePath, contentType, ticket.AttachmentFileName);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] TicketUpdateDto dto)
    {
        var unauthorized = EnsureAuthenticated(out var session);
        if (unauthorized is not null) return unauthorized;
        if (!CanWorkTickets(session)) return Forbid();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var ticket = await GetTicketWithCommentsAsync(id);
        if (ticket is null) return NotFound();
        if (!CanAccessTicket(session!, ticket)) return Forbid();

        ticket.Title = dto.Title.Trim();
        ticket.Description = dto.Description.Trim();
        ticket.Category = dto.Category.Trim();
        ticket.AssignedTo = dto.AssignedTo?.Trim();
        ticket.Status = (int)dto.Status;
        ticket.Priority = (int)dto.Priority;
        ticket.DueDate = dto.DueDate;
        ticket.Overview = IsAdmin(session) ? dto.Overview?.Trim() : ticket.Overview;
        ticket.ReviewNotes = IsAdmin(session) ? dto.ReviewNotes?.Trim() : ticket.ReviewNotes;

        await _supabase.Client.From<Ticket>().Update(ticket);

        _logger.LogInformation("Ticket {TicketId} updated by {Username}", id, session!.Username);

        // Push status update to external system if this ticket was forwarded
        if (ticket.ExternalSource == "outbound")
            _ = Task.Run(() => _helpdeskClient.PushStatusUpdateAsync(ticket, session.DisplayName));

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var unauthorized = EnsureAuthenticated(out var session);
        if (unauthorized is not null) return unauthorized;

        var ticket = await _supabase.Client.From<Ticket>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, id)
            .Single();

        if (ticket is null) return NotFound();
        if (!CanAccessTicket(session!, ticket) || !IsAdmin(session)) return Forbid();

        // DB cascade delete handles ticket_comments automatically
        await _supabase.Client.From<Ticket>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, id)
            .Delete();

        _logger.LogInformation("Ticket {TicketId} deleted by {Username}", id, session!.Username);

        return NoContent();
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] TicketStatusUpdateDto dto)
    {
        var unauthorized = EnsureAuthenticated(out var session);
        if (unauthorized is not null) return unauthorized;
        if (!CanWorkTickets(session)) return Forbid();
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var ticket = await _supabase.Client.From<Ticket>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, id)
            .Single();

        if (ticket is null) return NotFound();
        if (!CanAccessTicket(session!, ticket)) return Forbid();

        ticket.Status = (int)dto.Status;
        await _supabase.Client.From<Ticket>().Update(ticket);

        // Push status update to external system if this ticket was forwarded
        if (ticket.ExternalSource == "outbound")
            _ = Task.Run(() => _helpdeskClient.PushStatusUpdateAsync(ticket, session!.DisplayName));

        return NoContent();
    }

    [HttpPost("{id:int}/comments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketReadDto>> AddComment(int id, [FromBody] TicketCommentCreateDto dto)
    {
        var unauthorized = EnsureAuthenticated(out var session);
        if (unauthorized is not null) return unauthorized;
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var trimmedMessage = dto.Message.Trim();
        if (string.IsNullOrEmpty(trimmedMessage))
        {
            ModelState.AddModelError(nameof(dto.Message), "Comment cannot be empty.");
            return ValidationProblem(ModelState);
        }

        var ticket = await GetTicketWithCommentsAsync(id);
        if (ticket is null) return NotFound();
        if (!CanAccessTicket(session!, ticket)) return Forbid();

        await _supabase.Client.From<TicketComment>().Insert(new TicketComment
        {
            TicketId = id,
            AuthorName = session!.DisplayName,
            Message = trimmedMessage,
            CreatedDate = DateTime.UtcNow
        });

        var updated = await GetTicketWithCommentsAsync(id);

        // Push comment to external system if this ticket was forwarded
        if (ticket.ExternalSource == "outbound")
            _ = Task.Run(() => _helpdeskClient.PushCommentAsync(ticket, trimmedMessage, session!.DisplayName));

        return Ok(ToReadDto(updated!));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<Ticket?> GetTicketWithCommentsAsync(int id)
    {
        var ticket = await _supabase.Client.From<Ticket>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, id)
            .Single();

        if (ticket is null) return null;

        var commentsResult = await _supabase.Client
            .From<TicketComment>()
            .Filter("ticket_id", Supabase.Postgrest.Constants.Operator.Equals, id)
            .Get();

        ticket.Comments = commentsResult.Models;
        return ticket;
    }

    private static TicketReadDto ToReadDto(Ticket ticket) => new()
    {
        Id = ticket.Id,
        Title = ticket.Title,
        Description = ticket.Description,
        Category = ticket.Category,
        CreatedByUsername = ticket.CreatedByUsername,
        CreatedByDisplayName = ticket.CreatedByDisplayName,
        CreatedByRole = ticket.CreatedByRole,
        Status = ticket.TicketStatus,
        Priority = ticket.TicketPriority,
        CreatedDate = ticket.CreatedDate,
        DueDate = ticket.DueDate,
        AssignedTo = ticket.AssignedTo,
        Overview = ticket.Overview,
        ReviewNotes = ticket.ReviewNotes,
        HasAttachment = !string.IsNullOrWhiteSpace(ticket.AttachmentRelativePath),
        AttachmentFileName = ticket.AttachmentFileName,
        AttachmentContentType = ticket.AttachmentContentType,
        AttachmentUrl = string.IsNullOrWhiteSpace(ticket.AttachmentRelativePath)
            ? null
            : $"/api/tickets/{ticket.Id}/attachment",
        Comments = ticket.Comments
            .OrderBy(c => c.CreatedDate)
            .Select(c => new TicketCommentReadDto
            {
                AuthorName = c.AuthorName,
                Message = c.Message,
                CreatedDate = c.CreatedDate
            })
            .ToList()
    };

    private UnauthorizedObjectResult? EnsureAuthenticated(out AuthSession? session)
    {
        session = null;
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith(BearerScheme, StringComparison.OrdinalIgnoreCase))
        {
            var token = header[BearerScheme.Length..].Trim();
            if (_sessionStore.TryGetValidSession(token, out session))
                return null;
        }
        return Unauthorized(new { message = "Please log in to access tickets." });
    }

    private static bool IsAdmin(AuthSession? s) =>
        string.Equals(s?.Role, "Admin", StringComparison.OrdinalIgnoreCase);

    private static bool IsIt(AuthSession? s) =>
        string.Equals(s?.Role, "I.T", StringComparison.OrdinalIgnoreCase);

    private static bool CanWorkTickets(AuthSession? s) => IsAdmin(s) || IsIt(s);
    private static bool CanManagePlanningFields(AuthSession? s) => IsAdmin(s) || IsIt(s);

    private static bool CanAccessTicket(AuthSession session, Ticket ticket) =>
        IsAdmin(session) ||
        (IsIt(session) && (IsCustomerCreatedTicket(ticket) || IsAssignedToSession(session, ticket))) ||
        string.Equals(ticket.CreatedByUsername, session.Username, StringComparison.OrdinalIgnoreCase);

    private static bool IsCustomerCreatedTicket(Ticket ticket) =>
        string.IsNullOrWhiteSpace(ticket.CreatedByRole) ||
        (!string.Equals(ticket.CreatedByRole, "Admin", StringComparison.OrdinalIgnoreCase) &&
         !string.Equals(ticket.CreatedByRole, "I.T", StringComparison.OrdinalIgnoreCase));

    private static bool IsAssignedToSession(AuthSession session, Ticket ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket.AssignedTo)) return false;
        var assignedTo = ticket.AssignedTo.Trim();
        return string.Equals(assignedTo, session.Username, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(assignedTo, session.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ValidateAttachment(IFormFile attachment)
    {
        if (attachment.Length <= 0) return "Attachment is empty.";
        if (attachment.Length > MaxAttachmentBytes) return "Attachment must be 10 MB or smaller.";
        var ext = Path.GetExtension(attachment.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedAttachmentExtensions.Contains(ext))
            return "Attachment type is not supported.";
        return null;
    }

    private async Task SaveAttachmentAsync(Ticket ticket, IFormFile attachment)
    {
        var uploadsDir = Path.Combine(_environment.ContentRootPath, UploadsFolder);
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(attachment.FileName);
        var storedFileName = $"{Guid.NewGuid():N}{ext}";
        var absolutePath = Path.Combine(uploadsDir, storedFileName);

        await using var stream = System.IO.File.Create(absolutePath);
        await attachment.CopyToAsync(stream);

        ticket.AttachmentFileName = Path.GetFileName(attachment.FileName);
        ticket.AttachmentStoredFileName = storedFileName;
        ticket.AttachmentContentType = ResolveContentType(attachment, ext);
        ticket.AttachmentRelativePath = Path.Combine(UploadsFolder, storedFileName);
    }

    private string ResolveContentType(IFormFile attachment, string extension)
    {
        if (!string.IsNullOrWhiteSpace(attachment.ContentType) &&
            !string.Equals(attachment.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return attachment.ContentType;

        if (_contentTypeProvider.TryGetContentType($"file{extension}", out var resolved))
            return resolved;

        return "application/octet-stream";
    }
}
