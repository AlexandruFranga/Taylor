using Microsoft.EntityFrameworkCore;
using WorkTimeBot.Models;

namespace WorkTimeBot.Services;

public class WorkTimeDbContext : DbContext
{
    public DbSet<UserRecord> Users => Set<UserRecord>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<BotSetting> Settings => Set<BotSetting>();

    public WorkTimeDbContext(DbContextOptions<WorkTimeDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRecord>(entity =>
        {
            entity.HasKey(u => u.UserId);
            entity.Property(u => u.UserId).HasConversion<long>().ValueGeneratedNever();

            entity.HasMany(u => u.Sessions)
                .WithOne()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.UserId).HasConversion<long>();
        });

        modelBuilder.Entity<BotSetting>(entity =>
        {
            entity.HasKey(s => s.Key);
        });
    }
}
