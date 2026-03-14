using KcpServer;
using Kcp.Core;

namespace Kcp.SmokeTests.Cases;

internal sealed class RpcSuccessCase : SmokeTestCaseBase
{
    public override string Name => "RPC 成功返回";

    protected override async Task<string> ExecuteCoreAsync(SmokeTestContext context, CancellationToken cancellationToken)
    {
        var payload = $"{{\"trace\":\"{Guid.NewGuid():N}\"}}";
        context.ClearMessageQueue(KcpConstants.MessageIds.RpcResponse);

        await context.Client.SendAsync(KcpConstants.MessageIds.RpcRequest, new RpcRequest
        {
            Method = "TestApi",
            Params = payload
        });

        var baseMessage = await context.WaitForMessageAsync(
            KcpConstants.MessageIds.RpcResponse,
            TimeSpan.FromMilliseconds(context.Options.TimeoutMs),
            _ => true,
            cancellationToken);

        if (baseMessage == null)
        {
            throw new TimeoutException("未收到 RpcResponse");
        }

        var response = RpcResponse.Parser.ParseFrom(baseMessage.Payload);
        if (response.Code != 0)
        {
            throw new InvalidOperationException($"RPC 返回失败码: {response.Code}");
        }

        if (!response.Result.Contains(payload, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"RPC 返回内容不符合预期: {response.Result}");
        }

        return "RPC 成功返回";
    }
}
