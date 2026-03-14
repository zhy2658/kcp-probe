using Kcp.SmokeTests;
using Kcp.SmokeTests.Cases;
using System.Text;
using Kcp.Core;

Console.OutputEncoding = new UTF8Encoding(false);

var options = ParseOptions(args);
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var cases = new List<ISmokeTestCase>
{
    new ConnectedCase(),
    new PingPongCase(),
    new RpcSuccessCase(),
    new RpcUnknownMethodCase(),
    new UnknownMsgIdCase(),
    new StressPingCase()
};

var results = new List<SmokeTestCaseResult>();
using (var context = new SmokeTestContext(options))
{
    try
    {
        Console.WriteLine($"连接目标 {options.Ip}:{options.Port} conv={options.ConvId}");
        await context.ConnectAsync(cts.Token);
        Console.WriteLine("连接成功");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"连接失败: {ex.Message}");
        Environment.ExitCode = 1;
        return;
    }

    foreach (var testCase in cases)
    {
        if (cts.IsCancellationRequested)
        {
            break;
        }

        Console.WriteLine($"[RUN ] {testCase.Name}");
        var result = await testCase.RunAsync(context, cts.Token);
        results.Add(result);
        var tag = result.Passed ? "PASS" : "FAIL";
        Console.WriteLine($"[{tag}] {result.Name} ({result.DurationMs}ms) {result.Message}");
    }
}

var failed = results.Count(r => !r.Passed);
Console.WriteLine($"完成: 总计 {results.Count}，通过 {results.Count - failed}，失败 {failed}");
Environment.ExitCode = failed == 0 ? 0 : 1;

static SmokeTestOptions ParseOptions(string[] args)
{
    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length - 1; i += 2)
    {
        if (args[i].StartsWith("--", StringComparison.Ordinal))
        {
            dict[args[i]] = args[i + 1];
        }
    }

    return new SmokeTestOptions
    {
        Ip = GetValue(dict, "--ip", KcpConstants.Config.DefaultIp),
        Port = GetIntValue(dict, "--port", KcpConstants.Config.DefaultPort),
        ConvId = GetIntValue(dict, "--conv", KcpConstants.Config.DefaultConvId),
        StressCount = GetIntValue(dict, "--count", 200),
        TimeoutMs = GetIntValue(dict, "--timeout", KcpConstants.Timeouts.HealthCriticalMs)
    };
}

static string GetValue(Dictionary<string, string> dict, string key, string defaultValue)
{
    return dict.TryGetValue(key, out var value) ? value : defaultValue;
}

static int GetIntValue(Dictionary<string, string> dict, string key, int defaultValue)
{
    return dict.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : defaultValue;
}
