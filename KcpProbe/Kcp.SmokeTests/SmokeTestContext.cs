using System.Collections.Concurrent;
using System.Diagnostics;
using Kcp.Core;
using KcpServer;

namespace Kcp.SmokeTests;

internal sealed class SmokeTestContext : IDisposable
{
    private readonly ConcurrentDictionary<uint, ConcurrentQueue<BaseMessage>> _messageQueues = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public SmokeTestContext(SmokeTestOptions options)
    {
        Options = options;
        Client = new KcpClient();
        Client.OnMessageReceived += OnMessageReceived;
        Client.OnLog += m => Console.WriteLine($"[KcpClient] {m}");
    }

    public SmokeTestOptions Options { get; }
    public KcpClient Client { get; }

    private void OnMessageReceived(byte[] data)
    {
        try
        {
            var baseMessage = BaseMessage.Parser.ParseFrom(data);
            var queue = _messageQueues.GetOrAdd(baseMessage.MsgId, _ => new ConcurrentQueue<BaseMessage>());
            queue.Enqueue(baseMessage);
        }
        catch
        {
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await Client.ConnectAsync(Options.Ip, Options.Port, Options.ConvId);
        var timeoutAt = _stopwatch.ElapsedMilliseconds + Options.TimeoutMs;
        while (!Client.IsConnected)
        {
            if (_stopwatch.ElapsedMilliseconds > timeoutAt)
            {
                throw new TimeoutException("连接超时");
            }

            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(20, cancellationToken);
        }
    }

    public void ClearMessageQueue(uint msgId)
    {
        _messageQueues.TryRemove(msgId, out _);
    }

    public async Task<BaseMessage?> WaitForMessageAsync(uint msgId, TimeSpan timeout, Func<BaseMessage, bool>? predicate, CancellationToken cancellationToken)
    {
        var timeoutAt = _stopwatch.Elapsed + timeout;
        while (_stopwatch.Elapsed < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_messageQueues.TryGetValue(msgId, out var queue))
            {
                while (queue.TryDequeue(out var message))
                {
                    if (predicate == null || predicate(message))
                    {
                        return message;
                    }
                }
            }

            await Task.Delay(10, cancellationToken);
        }

        return null;
    }

    public void Dispose()
    {
        Client.OnMessageReceived -= OnMessageReceived;
        Client.Dispose();
    }
}
