using System.Web;
using Npgsql;

namespace AuditAI.Infrastructure.Persistence;

public static class NeonConnectionStringHelper
{
    public static string Normalize(string? rawConnectionString)
    {
        if (string.IsNullOrWhiteSpace(rawConnectionString))
        {
            throw new InvalidOperationException(
                "No se encontró cadena de conexión PostgreSQL. Configura ConnectionStrings:DefaultConnection o DATABASE_URL.");
        }

        var trimmed = rawConnectionString.Trim();
        if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var uri = new Uri(trimmed);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.Trim('/');

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = database,
            Username = username,
            Password = password,
            SslMode = SslMode.Require,
            Pooling = true,
            MinPoolSize = 0,
            MaxPoolSize = 20,
            Timeout = 30,
            CommandTimeout = 45
        };

        var query = HttpUtility.ParseQueryString(uri.Query);
        var sslMode = query["sslmode"];
        if (string.Equals(sslMode, "disable", StringComparison.OrdinalIgnoreCase))
        {
            builder.SslMode = SslMode.Disable;
        }
        else if (string.Equals(sslMode, "verify-full", StringComparison.OrdinalIgnoreCase))
        {
            builder.SslMode = SslMode.VerifyFull;
        }

        var channelBinding = query["channel_binding"];
        if (string.Equals(channelBinding, "require", StringComparison.OrdinalIgnoreCase))
        {
            builder.ChannelBinding = ChannelBinding.Require;
        }
        else if (string.Equals(channelBinding, "prefer", StringComparison.OrdinalIgnoreCase))
        {
            builder.ChannelBinding = ChannelBinding.Prefer;
        }

        return builder.ConnectionString;
    }
}
