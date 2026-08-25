using Npgsql;
using WorkTimeBot.Models;

namespace WorkTimeBot.Services;

public static class PostgresConnectionString
{
    public static string Resolve(BotConfig config)
    {
        var explicitConnectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(explicitConnectionString))
        {
            return explicitConnectionString;
        }

        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            return FromDatabaseUrl(databaseUrl);
        }

        if (!string.IsNullOrWhiteSpace(config.ConnectionString))
        {
            return config.ConnectionString;
        }

        throw new InvalidOperationException(
            "No Postgres connection string found. Set POSTGRES_CONNECTION_STRING or DATABASE_URL, or add ConnectionString to config.json.");
    }

    private static string FromDatabaseUrl(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            SslMode = SslMode.Prefer,
        };

        return builder.ConnectionString;
    }
}
