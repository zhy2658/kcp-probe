using KcpServer;
using Kcp.Core;

namespace Kcp.SmokeTests.Cases;

internal sealed class UnknownMsgIdCase : SmokeTestCaseBase
{
    public override string Name => "未知消息ID容错";

    protected override async Task<string> ExecuteCoreAsync(SmokeTestContext context, CancellationToken cancellationToken)
    {
        await context.Client.SendAsync(KcpConstants.MessageIds.Unknown, new RpcRequest
        {
            Method = "Noop",
            Params = "{}"
        });

        if (!context.Client.IsConnected)
        {
            throw new InvalidOperationException("发送未知消息后连接断开");
        }

        var content = $"guard-{Guid.NewGuid():N}";
        context.ClearMessageQueue(KcpConstants.MessageIds.Pong);
        await context.Client.SendAsync(KcpConstants.MessageIds.Ping, new Ping
        {
            Content = content,
            SendTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        var pongMessage = await context.WaitForMessageAsync(
            KcpConstants.MessageIds.Pong,
            TimeSpan.FromMilliseconds(context.Options.TimeoutMs),
            _ => true,
            cancellationToken);

        if (pongMessage == null)
        {
            throw new TimeoutException("未知消息后未收到后续 Pong");
        }

        return "未知消息后链路仍可用";
    }
}
