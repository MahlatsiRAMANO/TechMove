using Microsoft.EntityFrameworkCore;

namespace TechMove.Api.Data;

/// <summary>
/// Applies EF Core migrations when the API starts. In Docker the SQL Server container can take
/// a while to accept connections, so this retries instead of crashing the API.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitialiseAsync(IServiceProvider services, ILogger logger, CancellationToken ct = default)
    {
        const int maxAttempts = 12;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                if (db.Database.IsRelational())
                {
                    await db.Database.MigrateAsync(ct);
                }
                else
                {
                    // In-memory provider (integration tests): creates the schema and seed data.
                    await db.Database.EnsureCreatedAsync(ct);
                }

                logger.LogInformation("Database is ready.");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && !ct.IsCancellationRequested)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}/{Max}): {Message}", attempt, maxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }
}
