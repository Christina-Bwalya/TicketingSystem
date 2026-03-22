using ChristinaTicketingSystem.Api.Data;
using ChristinaTicketingSystem.Api.Models;
using ChristinaTicketingSystem.Api.Models.Dtos;
using ChristinaTicketingSystem.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;

namespace ChristinaTicketingSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".bmp",
        ".webp",
        ".pdf",
        ".txt",
        ".csv",
        ".doc",
        ".docx",
        ".xls",
        ".xlsx"
    };

    private readonly AuthSessionStore _sessionStore;
    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public TicketsController(
        AuthSessionStore sessionStore,
        AppDbContext dbContext,
        IWebHostEnvironment environment)
    {
        _sessionStore = sessionStore;
        _dbContext = dbContext;
        _environment = environment;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TicketReadDto>>> GetAll([FromQuery] TicketStatus? status)
    {
        var unauthorized = EnsureAuthenticated(out var session);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var query = _dbContext.Tickets
            .Include(ticket => ticket.Comments)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(ticket => ticket.Status == status.Value);
        }

        var tickets = await query
            .OrderByDescending(ticket => ticket.CreatedDate)
            .ToListAsync();

        var visibleTickets = tickets
            .Where(ticket => CanAccessTicket(session!, ticket))
            .Select(ToReadDto)
            .ToList();

        return Ok(visibleTickets);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketReadDto>> GetById(int id)
    {
        var unauthorized = EnsureAuthenticated(out var session);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var ticket = await _dbContext.Tickets
            .Include(t => t.Comments)
            .SingleOrDefaultAsync(t => t.Id == id);

        if (ticket is null)
        {
            return NotFound();
        }

        if (!CanAccessTicket(session!, ticket))
        {
            return Forbid();
        }

        return Ok(ToReadDto(ticket));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("application/json")]
    public async Task<ActionResult<TicketReadDto>> CreateJson([FromBody] TicketCreateDto dto)
    {
        return await CreateTicketAsync(dto, null);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxAttachmentBytes + (1024 * 1024))]
    public async Task<ActionResult<TicketReadDto>> CreateWithAttachment([FromForm] TicketCreateDto dto)
    {
        return await CreateTicketAsync(dto, dto.Attachment);
    }

    private async Task<ActionResult<TicketReadDto>> CreateTicketAsync(TicketCreateDto dto, IFormFile? attachment)
    {
        var unauthorized = EnsureAuthenticated(out var session);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var ticket = new Ticket
        {
            Title = dto.Title,
            Description = dto.Description,
            Category = dto.Category,
            CreatedByUsername = session!.Username,
            CreatedByDisplayName = session.DisplayName,
            CreatedByRole = session.Role,
            AssignedTo = dto.AssignedTo,
            Status = TicketStatus.Open,
            Priority = dto.Priority,
            CreatedDate = DateTime.UtcNow,
            DueDate = CanManagePlanningFields(session) ? dto.DueDate : null,
            Overview = IsAdmin(session) ? dto.Overview : null,
            ReviewNotes = IsAdmin(session) ? dto.ReviewNotes : null
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

        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ToReadDto(ticket));
    }

    [HttpGet("{id:int}/attachment")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(int id)
    {
        var unauthorized = EnsureAuthenticated(out var session);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var ticket = await _dbContext.Tickets.SingleOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
        {
            return NotFound();
        }

        if (!CanAccessTicket(session!, ticket))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(ticket.AttachmentRelativePath) ||
            string.IsNullOrWhiteSpace(ticket.AttachmentFileName))
        {
            return NotFound();
        }

        var absolutePath = Path.Combine(_environment.ContentRootPath, ticket.AttachmentRelativePath);
        if (!System.IO.File.Exists(absolutePath))
        {
            return NotFound();
        }

        var contentType = string.IsNullOrWhiteSpace(ticket.AttachmentContentType)
            ? "application/octet-stream"
            : ticket.AttachmentContentType;

        return PhysicalFile(absolutePath, contentType, ticket.AttachmentFileName);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] TicketUpdateDto dto)
    {
        var unauthorized = EnsureAuthenticated(out var session);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        if (!CanWorkTickets(session))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var ticket = await _dbContext.Tickets
            .Include(t => t.Comments)
            .SingleOrDefaultAsync(t => t.Id == id);

        if (ticket is null)
        {
            return NotFound();
        }

        if (!CanAccessTicket(session!, ticket))
        {
            return Forbid();
        }

        ticket.Title = dto.Title;
        ticket.Description = dto.Description;
        ticket.Category = dto.Category;
        ticket.AssignedTo = dto.AssignedTo;
        ticket.Status = dto.Status;
        ticket.Priority = dto.Priority;
        ticket.DueDate = dto.DueDate;
        ticket.Overview = IsAdmin(session) ? dto.Overview : ticket.Overview;
        ticket.ReviewNotes = IsAdmin(session) ? dto.ReviewNotes : ticket.ReviewNotes;

        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var unauthorized = EnsureAuthenticated(out var session);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        var ticket = await _dbContext.Tickets
            .Include(t => t.Comments)
            .SingleOrDefaultAsync(t => t.Id == id);

        if (ticket is null)
        {
            return NotFound();
        }

        if (!CanAccessTicket(session!, ticket) || !IsAdmin(session))
        {
            return Forbid();
        }

        _dbContext.Tickets.Remove(ticket);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] TicketStatusUpdateDto dto)
    {
        var unauthorized = EnsureAuthenticated(out var session);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        if (!CanWorkTickets(session))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var ticket = await _dbContext.Tickets.SingleOrDefaultAsync(t => t.Id == id);
        if (ticket is null)
        {
            return NotFound();
        }

        if (!CanAccessTicket(session!, ticket))
        {
            return Forbid();
        }

        ticket.Status = dto.Status;
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/comments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketReadDto>> AddComment(int id, [FromBody] TicketCommentCreateDto dto)
    {
        var unauthorized = EnsureAuthenticated(out var session);
        if (unauthorized is not null)
        {
            return unauthorized;
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var ticket = await _dbContext.Tickets
            .Include(t => t.Comments)
            .SingleOrDefaultAsync(t => t.Id == id);

        if (ticket is null)
        {
            return NotFound();
        }

        if (!CanAccessTicket(session!, ticket))
        {
            return Forbid();
        }

        ticket.Comments.Add(new TicketComment
        {
            TicketId = ticket.Id,
            AuthorName = session!.DisplayName,
            Message = dto.Message.Trim(),
            CreatedDate = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();
        return Ok(ToReadDto(ticket));
    }

    private static TicketReadDto ToReadDto(Ticket ticket) =>
        new()
        {
            Id = ticket.Id,
            Title = ticket.Title,
            Description = ticket.Description,
            Category = ticket.Category,
            CreatedByUsername = ticket.CreatedByUsername,
            CreatedByDisplayName = ticket.CreatedByDisplayName,
            CreatedByRole = ticket.CreatedByRole,
            Status = ticket.Status,
            Priority = ticket.Priority,
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
                .Select(comment => new TicketCommentReadDto
                {
                    AuthorName = comment.AuthorName,
                    Message = comment.Message,
                    CreatedDate = comment.CreatedDate
                })
                .ToList()
        };

    private UnauthorizedObjectResult? EnsureAuthenticated(out AuthSession? session)
    {
        session = null;
        var authorizationHeader = Request.Headers.Authorization.ToString();

        if (authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authorizationHeader["Bearer ".Length..].Trim();
            if (_sessionStore.TryGetValidSession(token, out session))
            {
                return null;
            }
        }

        return Unauthorized(new { message = "Please log in to access tickets." });
    }

    private static bool IsAdmin(AuthSession? session) =>
        string.Equals(session?.Role, "Admin", StringComparison.OrdinalIgnoreCase);

    private static bool IsIt(AuthSession? session) =>
        string.Equals(session?.Role, "I.T", StringComparison.OrdinalIgnoreCase);

    private static bool IsCustomer(AuthSession? session) =>
        !IsAdmin(session) && !IsIt(session);

    private static bool CanWorkTickets(AuthSession? session) =>
        IsAdmin(session) || IsIt(session);

    private static bool CanManagePlanningFields(AuthSession? session) =>
        IsAdmin(session) || IsIt(session);

    private static bool CanAccessTicket(AuthSession session, Ticket ticket) =>
        IsAdmin(session) ||
        (IsIt(session) &&
         (IsCustomerCreatedTicket(ticket) ||
          IsAssignedToSession(session, ticket))) ||
        string.Equals(ticket.CreatedByUsername, session.Username, StringComparison.OrdinalIgnoreCase);

    private static bool IsCustomerCreatedTicket(Ticket ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket.CreatedByRole))
        {
            return true;
        }

        return !string.Equals(ticket.CreatedByRole, "Admin", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(ticket.CreatedByRole, "I.T", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAssignedToSession(AuthSession session, Ticket ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket.AssignedTo))
        {
            return false;
        }

        var assignedTo = ticket.AssignedTo.Trim();
        return string.Equals(assignedTo, session.Username, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(assignedTo, session.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ValidateAttachment(IFormFile attachment)
    {
        if (attachment.Length <= 0)
        {
            return "Attachment is empty.";
        }

        if (attachment.Length > MaxAttachmentBytes)
        {
            return "Attachment must be 10 MB or smaller.";
        }

        var extension = Path.GetExtension(attachment.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedAttachmentExtensions.Contains(extension))
        {
            return "Attachment type is not supported. Please upload an image, PDF, text, or Office document.";
        }

        return null;
    }

    private async Task SaveAttachmentAsync(Ticket ticket, IFormFile attachment)
    {
        var uploadsDirectory = Path.Combine(_environment.ContentRootPath, "Uploads", "tickets");
        Directory.CreateDirectory(uploadsDirectory);

        var extension = Path.GetExtension(attachment.FileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(uploadsDirectory, storedFileName);

        await using var stream = System.IO.File.Create(absolutePath);
        await attachment.CopyToAsync(stream);

        var relativePath = Path.Combine("Uploads", "tickets", storedFileName);
        ticket.AttachmentFileName = Path.GetFileName(attachment.FileName);
        ticket.AttachmentStoredFileName = storedFileName;
        ticket.AttachmentContentType = ResolveContentType(attachment, extension);
        ticket.AttachmentRelativePath = relativePath;
    }

    private string ResolveContentType(IFormFile attachment, string extension)
    {
        if (!string.IsNullOrWhiteSpace(attachment.ContentType) &&
            !string.Equals(attachment.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return attachment.ContentType;
        }

        if (_contentTypeProvider.TryGetContentType($"file{extension}", out var resolvedContentType))
        {
            return resolvedContentType;
        }

        return "application/octet-stream";
    }
}
