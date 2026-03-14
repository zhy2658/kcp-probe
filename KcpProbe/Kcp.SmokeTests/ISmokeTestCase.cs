namespace Kcp.SmokeTests;

internal interface ISmokeTestCase
{
    string Name { get; }
    Task<SmokeTestCaseResult> RunAsync(SmokeTestContext context, CancellationToken cancellationToken);
}
