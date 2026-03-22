using System.Text.Json.Serialization;
using ChristinaTicketingSystem.Api.Data;
using ChristinaTicketingSystem.Api.Models;
using ChristinaTicketingSystem.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.Configure<AuthSettings>(
    builder.Configuration.GetSection(AuthSettings.SectionName));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddScoped<AuthUserStore>();
builder.Services.AddSingleton<AuthSessionStore>();
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.InitializeAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// For this beginner-friendly API+UI, keep everything on HTTP.
// If you later add HTTPS, re-enable UseHttpsRedirection here.

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();
