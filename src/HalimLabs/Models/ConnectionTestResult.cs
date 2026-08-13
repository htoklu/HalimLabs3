namespace HalimLabs.Models;

public sealed class ConnectionTestResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }

    public static ConnectionTestResult Ok(string message, TimeSpan duration) =>
        new() { Success = true, Message = message, Duration = duration };

    public static ConnectionTestResult Fail(string message, TimeSpan duration) =>
        new() { Success = false, Message = message, Duration = duration };
}
