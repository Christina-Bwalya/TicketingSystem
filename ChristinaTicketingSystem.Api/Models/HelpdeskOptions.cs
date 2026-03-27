namespace ChristinaTicketingSystem.Api.Models;

public class HelpdeskOptions
{
    public const string SectionName = "ExternalHelpdesk";

    /// <summary>External helpdesk base URL e.g. https://ticketing-system-frontend-hazel.vercel.app</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>API key we send to them in Authorization: Bearer header</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Secret we use to sign outbound requests to them (X-Webhook-Signature)</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>Secret they use to sign inbound requests to us — we verify this</summary>
    public string InboundSecret { get; set; } = string.Empty;

    /// <summary>Timeout in seconds for outbound HTTP calls</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Categories we forward to the external system</summary>
    public HashSet<string> ForwardCategories { get; set; } =
        new(StringComparer.OrdinalIgnoreCase) { "NETWORK", "ACCOUNT", "ACCESS" };

    /// <summary>Categories we accept from the external system</summary>
    public HashSet<string> InboundCategories { get; set; } =
        new(StringComparer.OrdinalIgnoreCase) { "HARDWARE", "SOFTWARE", "OTHER" };
}
