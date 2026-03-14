using KcpServer;
using Kcp.Core;

namespace Kcp.SmokeTests.Cases;

internal sealed class PingPongCase : SmokeTestCaseBase
{
    public override string Name => "Ping/Pong";

    protected override async Task<string> ExecuteCoreAsync(SmokeTestContext context, CancellationToken cancellationToken)
    {
        var content = $"smoke-{Guid.NewGuid():N}";
        context.ClearMessageQueue(KcpConstants.MessageIds.Pong);

        await context.Client.SendAsync(KcpConstants.MessageIds.Ping, new Ping
        {
            Content = content,
            SendTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        var baseMessage = await context.WaitForMessageAsync(
            KcpConstants.MessageIds.Pong,
            TimeSpan.FromMilliseconds(context.Options.TimeoutMs),
            _ => true,
            cancellationToken);

        if (baseMessage == null)
        {
            throw new TimeoutException("未收到 Pong");
        }

        var pong = Pong.Parser.ParseFrom(baseMessage.Payload);
        if (pong.Content != $"Pong: {content}")
        {
            throw new InvalidOperationException($"Pong 内容不匹配: {pong.Content}");
        }

        return "Pong 返回正常";
    }
}
