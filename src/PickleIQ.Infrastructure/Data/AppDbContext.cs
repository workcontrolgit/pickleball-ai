using Microsoft.EntityFrameworkCore;
using PickleIQ.Core.Entities;

namespace PickleIQ.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<VideoJob> VideoJobs => Set<VideoJob>();
    public DbSet<RallySegment> RallySegments => Set<RallySegment>();
    public DbSet<CoachingReport> CoachingReports => Set<CoachingReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VideoJob>(e =>
        {
            e.HasKey(v => v.Id);
            e.Property(v => v.Status).HasConversion<string>();
            e.HasMany(v => v.RallySegments).WithOne(r => r.VideoJob).HasForeignKey(r => r.VideoJobId);
            e.HasOne(v => v.CoachingReport).WithOne(r => r.VideoJob).HasForeignKey<CoachingReport>(r => r.VideoJobId);
        });

        modelBuilder.Entity<RallySegment>(e => e.HasKey(r => r.Id));
        modelBuilder.Entity<CoachingReport>(e => e.HasKey(r => r.Id));
    }
}
