using System.Diagnostics;

namespace Kcp.SmokeTests;

internal abstract class SmokeTestCaseBase : ISmokeTestCase
{
    public abstract string Name { get; }

    public async Task<SmokeTestCaseResult> RunAsync(SmokeTestContext context, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var message = await ExecuteCoreAsync(context, cancellationToken);
            return new SmokeTestCaseResult
            {
                Name = Name,
                Passed = true,
                Message = message,
                DurationMs = watch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            return new SmokeTestCaseResult
            {
                Name = Name,
                Passed = false,
                Message = ex.Message,
                DurationMs = watch.ElapsedMilliseconds
            };
        }
    }

    protected abstract Task<string> ExecuteCoreAsync(SmokeTestContext context, CancellationToken cancellationToken);
}
