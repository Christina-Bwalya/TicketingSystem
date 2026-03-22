using System.Collections.Concurrent;

namespace ChristinaTicketingSystem.Api.Services;

public class AuthSessionStore
{
    private readonly ConcurrentDictionary<string, AuthSession> _sessions = new();
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    public AuthSession CreateSession(string username, string displayName, string role)
    {
        var session = new AuthSession(
            Guid.NewGuid().ToString("N"),
            username,
            displayName,
            role,
            DateTime.UtcNow.Add(SessionLifetime));

        _sessions[session.Token] = session;
        return session;
    }

    public bool TryGetValidSession(string token, out AuthSession? session)
    {
        session = null;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (!_sessions.TryGetValue(token, out var storedSession))
        {
            return false;
        }

        if (storedSession.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _sessions.TryRemove(token, out _);
            return false;
        }

        session = storedSession;
        return true;
    }

    public void RemoveSession(string token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            _sessions.TryRemove(token, out _);
        }
    }
}

public sealed record AuthSession(
    string Token,
    string Username,
    string DisplayName,
    string Role,
    DateTime ExpiresAtUtc);
