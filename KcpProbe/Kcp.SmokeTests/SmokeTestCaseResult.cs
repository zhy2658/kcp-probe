namespace Kcp.SmokeTests;

internal sealed class SmokeTestCaseResult
{
    public required string Name { get; init; }
    public required bool Passed { get; init; }
    public string Message { get; init; } = string.Empty;
    public long DurationMs { get; init; }
}
