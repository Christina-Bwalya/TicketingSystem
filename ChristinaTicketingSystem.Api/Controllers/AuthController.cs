using ChristinaTicketingSystem.Api.Models;
using ChristinaTicketingSystem.Api.Models.Dtos;
using ChristinaTicketingSystem.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChristinaTicketingSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const string BearerScheme = "Bearer ";

    private readonly AuthSessionStore _sessionStore;
    private readonly AuthUserStore _userStore;
    private readonly PasswordService _passwordService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AuthSessionStore sessionStore,
        AuthUserStore userStore,
        PasswordService passwordService,
        ILogger<AuthController> logger)
    {
        _sessionStore = sessionStore;
        _userStore = userStore;
        _passwordService = passwordService;
        _logger = logger;
    }

    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto dto)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var user = await _userStore.TryGetUserAsync(dto.Username);
        if (user is null || !_passwordService.VerifyPassword(dto.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for username: {Username}", dto.Username);
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var session = _sessionStore.CreateSession(user.Username, user.DisplayName, user.Role);
        _logger.LogInformation("User {Username} logged in", user.Username);

        return Ok(new LoginResponseDto
        {
            Token = session.Token,
            Username = session.Username,
            DisplayName = session.DisplayName,
            Role = session.Role,
            ExpiresAtUtc = session.ExpiresAtUtc
        });
    }

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoginResponseDto>> Register([FromBody] RegisterRequestDto dto)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var username = dto.Username.Trim();
        var displayName = dto.DisplayName.Trim();
        var role = NormalizeRequestedRole(dto.Role);

        if (!await _userStore.RegisterUserAsync(username, displayName, dto.Password, role))
        {
            _logger.LogWarning("Registration conflict for username: {Username}", username);
            return Conflict(new { message = "That username is already registered." });
        }

        var user = await _userStore.TryGetUserAsync(username);
        var session = _sessionStore.CreateSession(username, displayName, user?.Role ?? "Customer");

        _logger.LogInformation("New user registered: {Username} with role {Role}", username, session.Role);

        return Created(string.Empty, new LoginResponseDto
        {
            Token = session.Token,
            Username = session.Username,
            DisplayName = session.DisplayName,
            Role = session.Role,
            ExpiresAtUtc = session.ExpiresAtUtc
        });
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        var token = TryReadBearerToken();
        _sessionStore.RemoveSession(token);
        return NoContent();
    }

    private string TryReadBearerToken()
    {
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith(BearerScheme, StringComparison.OrdinalIgnoreCase))
            return header[BearerScheme.Length..].Trim();
        return string.Empty;
    }

    private static string NormalizeRequestedRole(string? role) =>
        string.Equals(role, "I.T", StringComparison.OrdinalIgnoreCase) ? "I.T" : "Customer";
}
