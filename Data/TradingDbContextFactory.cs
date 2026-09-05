using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TradingEngine.Data;

/// <summary>
/// Design-time factory used exclusively by EF Core tooling (dotnet ef migrations add/update).
/// It is never invoked at runtime — the real context is configured in Program.cs via DATABASE_URL.
/// Set MIGRATIONS_CONNECTION_STRING or rely on the localhost default when running migrations locally.
/// </summary>
public class TradingDbContextFactory : IDesignTimeDbContextFactory<TradingDbContext>
{
    public TradingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MIGRATIONS_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=tradingengine;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<TradingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new TradingDbContext(options);
    }
}
