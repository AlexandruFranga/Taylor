using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WorkTimeBot.Services;

public class WorkTimeDbContextFactory : IDesignTimeDbContextFactory<WorkTimeDbContext>
{
    public WorkTimeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=worktimebot;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<WorkTimeDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new WorkTimeDbContext(optionsBuilder.Options);
    }
}
