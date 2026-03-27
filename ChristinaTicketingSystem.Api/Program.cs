using System.Text.Json.Serialization;
using ChristinaTicketingSystem.Api.Data;
using ChristinaTicketingSystem.Api.Models;
using ChristinaTicketingSystem.Api.Services;
using Microsoft.AspNetCore.Http.Features;
// Railway injects PORT — bind to it if present
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://+:{port}");

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

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
app.MapControllers();

app.Run();
