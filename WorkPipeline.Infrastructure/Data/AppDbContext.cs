using Microsoft.EntityFrameworkCore;
using WorkPipeline.Core.Entities;

namespace WorkPipeline.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkSession> WorkSessions => Set<WorkSession>();
    public DbSet<TicketWork> TicketWorks => Set<TicketWork>();
    public DbSet<ClaudeTranscriptEntry> TranscriptEntries => Set<ClaudeTranscriptEntry>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkSession>()
            .HasOne(s => s.Workspace)
            .WithMany(w => w.Sessions)
            .HasForeignKey(s => s.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TicketWork>()
            .HasOne(t => t.Session)
            .WithMany(s => s.Tickets)
            .HasForeignKey(t => t.SessionId);

        modelBuilder.Entity<ClaudeTranscriptEntry>()
            .HasOne(e => e.TicketWork)
            .WithMany(t => t.Transcript)
            .HasForeignKey(e => e.TicketWorkId);

        modelBuilder.Entity<WebhookEvent>()
            .HasOne(w => w.TicketWork)
            .WithMany()
            .HasForeignKey(w => w.TicketWorkId)
            .IsRequired(false);
    }
}
