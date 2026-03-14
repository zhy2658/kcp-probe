namespace Kcp.SmokeTests.Cases;

internal sealed class ConnectedCase : SmokeTestCaseBase
{
    public override string Name => "连接状态";

    protected override Task<string> ExecuteCoreAsync(SmokeTestContext context, CancellationToken cancellationToken)
    {
        if (!context.Client.IsConnected)
        {
            throw new InvalidOperationException("客户端未连接");
        }

        return Task.FromResult("连接可用");
    }
}
