namespace Kcp.SmokeTests;

internal sealed class SmokeTestOptions
{
    public string Ip { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 8888;
    public int ConvId { get; init; } = 1001;
    public int StressCount { get; init; } = 200;
    public int TimeoutMs { get; init; } = 5000;
}
