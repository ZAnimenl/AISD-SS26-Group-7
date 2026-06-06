namespace Backend.Persistence;

public static class DatabaseProviders
{
    public const string PostgreSql = "PostgreSql";
    public const string Sqlite = "Sqlite";

    public static bool IsSqlite(string? value)
    {
        return string.Equals(value, Sqlite, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSqliteProviderName(string? value)
    {
        return value?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
    }
}
