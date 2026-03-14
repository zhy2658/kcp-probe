using KcpServer;

namespace Kcp.SmokeTests.Cases;

internal sealed class RpcUnknownMethodCase : SmokeTestCaseBase
{
    public override string Name => "RPC 未知方法";

    protected override async Task<string> ExecuteCoreAsync(SmokeTestContext context, CancellationToken cancellationToken)
    {
        var method = $"Unknown_{Guid.NewGuid():N}";
        context.ClearMessageQueue(101);

        await context.Client.SendAsync(100, new RpcRequest
        {
            Method = method,
            Params = "{}"
        });

        var baseMessage = await context.WaitForMessageAsync(
            101,
            TimeSpan.FromMilliseconds(context.Options.TimeoutMs),
            _ => true,
            cancellationToken);

        if (baseMessage == null)
        {
            throw new TimeoutException("未收到未知方法的 RpcResponse");
        }

        var response = RpcResponse.Parser.ParseFrom(baseMessage.Payload);
        if (response.Code == 0)
        {
            throw new InvalidOperationException("未知方法不应返回成功码");
        }

        if (!response.ErrorMessage.Contains(method, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"错误信息未包含方法名: {response.ErrorMessage}");
        }

        return "未知方法处理正常";
    }
}
