using Microsoft.Extensions.Configuration;

namespace EquipmentBorrowing.Infrastructure.Persistence;

public static class DatabasePaths
{
    public const string ConnectionStringName = "EquipmentBorrowing";
    public const string ConnectionStringEnvironmentVariable = "EQUIPMENT_BORROWING_CONNECTION_STRING";

    public static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = TryGetConnectionString(configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set {ConnectionStringEnvironmentVariable} or ConnectionStrings:EquipmentBorrowing in appsettings.json before starting the application.");
        }

        return connectionString;
    }

    public static string? TryGetConnectionString(IConfiguration configuration)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = configuration.GetConnectionString(ConnectionStringName);
        }
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__EquipmentBorrowing");
        }

        return string.IsNullOrWhiteSpace(connectionString) ? null : connectionString;
    }

    public static string GetDesignTimeConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)
                               ?? Environment.GetEnvironmentVariable("ConnectionStrings__EquipmentBorrowing");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set {ConnectionStringEnvironmentVariable} to a PostgreSQL connection string before running EF Core tools.");
        }

        return connectionString;
    }
}
