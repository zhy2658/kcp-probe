using System;
using System.Threading;
using System.Threading.Tasks;
using Kcp.Core;
using KcpServer;

namespace KcpProbe.Services
{
    public class StressTestService
    {
        private readonly IKcpClient _client;
        private CancellationTokenSource? _stressCts;
        private bool _isStressing;

        public event Action<bool>? IsStressingChanged;
        public event Action<LogLevel, string>? Log;

        public StressTestService(IKcpClient client)
        {
            _client = client;
        }

        public bool IsStressing
        {
            get => _isStressing;
            private set
            {
                if (_isStressing != value)
                {
                    _isStressing = value;
                    IsStressingChanged?.Invoke(value);
                }
            }
        }

        public async Task StartStressAsync()
        {
            if (IsStressing) return;
            if (!_client.IsConnected)
            {
                Log?.Invoke(LogLevel.Warning, "Please connect first");
                return;
            }

            IsStressing = true;
            Log?.Invoke(LogLevel.Info, "Starting Stress Test...");
            _stressCts = new CancellationTokenSource();

            try
            {
                int count = 0;
                while (!_stressCts.Token.IsCancellationRequested && _client.IsConnected)
                {
                    var ping = new Ping { Content = $"Stress {count++}", SendTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                    await _client.SendAsync(KcpConstants.MessageIds.Ping, ping);
                    
                    if (count % 50 == 0) // Log every 50 packets (approx 0.5s)
                    {
                        Log?.Invoke(LogLevel.Info, $"[Stress] Sent {count} pings...");
                    }
                    
                    await Task.Delay(KcpConstants.Timeouts.StressIntervalMs, _stressCts.Token);
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                Log?.Invoke(LogLevel.Error, $"Stress Test Error: {ex.Message}");
            }
            finally
            {
                IsStressing = false;
                Log?.Invoke(LogLevel.Info, "Stress Test Stopped");
            }
        }

        public void StopStress()
        {
            if (!IsStressing) return;
            _stressCts?.Cancel();
        }
    }
}
