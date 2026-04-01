using System.Text.Json.Serialization;
using ChristinaTicketingSystem.Api.Data;
using ChristinaTicketingSystem.Api.Models;
using ChristinaTicketingSystem.Api.Services;
using Microsoft.AspNetCore.Http.Features;
using Serilog;
using Serilog.Events;
// Railway injects PORT — bind to it if present
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://+:{port}");

// ── Serilog structured logging ────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ChristinaTicketingSystem")
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/cts-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// ── Configuration ─────────────────────────────────────────────────────────
builder.Services.Configure<AuthSettings>(
    builder.Configuration.GetSection(AuthSettings.SectionName));

builder.Services.Configure<HelpdeskOptions>(
    builder.Configuration.GetSection(HelpdeskOptions.SectionName));

var supabaseUrl = builder.Configuration["Supabase:Url"]
    ?? throw new InvalidOperationException("Supabase:Url is not configured.");
var supabaseKey = builder.Configuration["Supabase:ServiceRoleKey"]
    ?? throw new InvalidOperationException("Supabase:ServiceRoleKey is not configured.");

// ── Supabase ──────────────────────────────────────────────────────────────
var supabaseClient = new Supabase.Client(supabaseUrl, supabaseKey, new Supabase.SupabaseOptions
{
    AutoConnectRealtime = false
});
await supabaseClient.InitializeAsync();

// ── Services ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton(supabaseClient);
builder.Services.AddSingleton<SupabaseService>();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<AuthSessionStore>();
builder.Services.AddScoped<AuthUserStore>();
builder.Services.AddScoped<DatabaseSeeder>();

// External helpdesk integration
builder.Services.AddHttpClient<ExternalHelpdeskClient>(client =>
{
    var timeout = builder.Configuration.GetValue<int>("ExternalHelpdesk:TimeoutSeconds", 10);
    client.Timeout = TimeSpan.FromSeconds(timeout);
});

// Global request size limit (11 MB — attachments are max 10 MB + overhead)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 11 * 1024 * 1024;
});

builder.Services.AddOpenApi();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var app = builder.Build();

// ── Database Seeding ──────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.InitializeAsync();
}

// ── Middleware ────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0}ms";
});
app.MapControllers();

app.Run();
