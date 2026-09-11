namespace FtoConsulting.PortfolioManager.Application.Utilities;

/// <summary>Invocation-local date override for reproducible scenarios. AsyncLocal flows through concurrent tool scopes.</summary>
public static class PortfolioClock
{
    private static readonly AsyncLocal<DateTimeOffset?> Override = new();
    public static DateTime UtcNow => (Override.Value ?? DateTimeOffset.UtcNow).UtcDateTime;
    public static IDisposable Use(DateTimeOffset? time)
    {
        var previous = Override.Value;
        Override.Value = time;
        return new Restore(() => Override.Value = previous);
    }
    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}
