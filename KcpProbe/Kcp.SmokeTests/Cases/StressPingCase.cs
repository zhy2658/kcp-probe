using KcpServer;
using Kcp.Core;

namespace Kcp.SmokeTests.Cases;

internal sealed class StressPingCase : SmokeTestCaseBase
{
    public override string Name => "基础压力冒烟";

    protected override async Task<string> ExecuteCoreAsync(SmokeTestContext context, CancellationToken cancellationToken)
    {
        var prefix = $"stress-{Guid.NewGuid():N}";
        var count = context.Options.StressCount;
        var received = 0;
        context.ClearMessageQueue(KcpConstants.MessageIds.Pong);

        for (var i = 0; i < count; i++)
        {
            await context.Client.SendAsync(KcpConstants.MessageIds.Ping, new Ping
            {
                Content = $"{prefix}-{i}",
                SendTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(context.Options.TimeoutMs * 2);
        while (DateTimeOffset.UtcNow < timeoutAt && received < count)
        {
            var message = await context.WaitForMessageAsync(
                KcpConstants.MessageIds.Pong,
                TimeSpan.FromMilliseconds(100),
                m =>
                {
                    var pong = Pong.Parser.ParseFrom(m.Payload);
                    return pong.Content.StartsWith($"Pong: {prefix}-", StringComparison.Ordinal);
                },
                cancellationToken);

            if (message != null)
            {
                received++;
            }
        }

        if (received != count)
        {
            throw new InvalidOperationException($"压力冒烟未收齐: {received}/{count}");
        }

        return $"压力冒烟通过 {received}/{count}";
    }
}
