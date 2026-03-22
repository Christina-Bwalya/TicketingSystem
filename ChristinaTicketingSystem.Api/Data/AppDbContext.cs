using ChristinaTicketingSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChristinaTicketingSystem.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<AuthUser> Users => Set<AuthUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuthUser>()
            .HasIndex(user => user.Username)
            .IsUnique();

        modelBuilder.Entity<Ticket>()
            .HasMany(ticket => ticket.Comments)
            .WithOne()
            .HasForeignKey(comment => comment.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
