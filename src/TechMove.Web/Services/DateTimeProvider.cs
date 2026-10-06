namespace TechMove.Web.Services;

/// <summary>Wraps the clock so date-based business rules can be unit tested.</summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateTime Today { get; }
}

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Today => DateTime.Today;
}
