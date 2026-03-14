using Kcp.Core;

namespace Kcp.SmokeTests;

internal sealed class SmokeTestOptions
{
    public string Ip { get; init; } = KcpConstants.Config.DefaultIp;
    public int Port { get; init; } = KcpConstants.Config.DefaultPort;
    public int ConvId { get; init; } = KcpConstants.Config.DefaultConvId;
    public int StressCount { get; init; } = 200;
    public int TimeoutMs { get; init; } = KcpConstants.Timeouts.HealthCriticalMs;
}
