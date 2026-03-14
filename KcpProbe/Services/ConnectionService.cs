using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kcp.Core;
using KcpServer;

namespace KcpProbe.Services
{
    public class ConnectionService
    {
        private readonly IKcpClient _client;
        private CancellationTokenSource? _pollingCts;
        private long _lastPongTime;
        
        public event Action<KcpStats>? StatsUpdated;
        public event Action<string>? HealthStatusChanged;
        public event Action<double>? RttUpdated;

        private readonly PacketDispatcher _dispatcher;

        public ConnectionService(IKcpClient client, PacketDispatcher dispatcher)
        {
            _client = client;
            _dispatcher = dispatcher;
            _client.OnMessageReceived += OnMessageReceived;
            
            // Register handlers
            _dispatcher.RegisterHandler(KcpConstants.MessageIds.Pong, OnPong);
            _dispatcher.RegisterHandler(KcpConstants.MessageIds.RpcResponse, OnRpcResponse);
        }

        public async Task ConnectAsync(string ip, int port, int convId, KcpConfig config)
        {
            await _client.ConnectAsync(ip, port, convId, config);
            StartStatsPolling();
            _lastPongTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            HealthStatusChanged?.Invoke(KcpConstants.HealthStatus.Checking);
        }

        public void Disconnect()
        {
            StopStatsPolling();
            _client.Disconnect();
            HealthStatusChanged?.Invoke(KcpConstants.HealthStatus.Unknown);
        }

        private void StartStatsPolling()
        {
            _pollingCts = new CancellationTokenSource();
            _ = PollStatsAsync(_pollingCts.Token);
        }

        private void StopStatsPolling()
        {
            _pollingCts?.Cancel();
            _pollingCts = null;
        }

        private async Task PollStatsAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _client.IsConnected)
            {
                try
                {
                    var stats = _client.GetStats();
                    if (stats != null)
                    {
                        StatsUpdated?.Invoke(stats);
                    }

                    // Probe RTT & Health Check
                    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var ping = new Ping { Content = $"Probe:{now}", SendTime = (ulong)now };
                    await _client.SendAsync(KcpConstants.MessageIds.Ping, ping);

                    CheckHealth(now);

                    await Task.Delay(KcpConstants.Timeouts.StatsPollingIntervalMs, token);
                }
                catch (TaskCanceledException) { break; }
                catch (Exception) { /* Ignore polling errors */ }
            }
        }

        private void CheckHealth(long now)
        {
            long timeSinceLastPong = now - _lastPongTime;
            string status;
            
            if (timeSinceLastPong > KcpConstants.Timeouts.HealthCriticalMs)
                status = KcpConstants.HealthStatus.Critical;
            else if (timeSinceLastPong > KcpConstants.Timeouts.HealthPoorMs)
                status = KcpConstants.HealthStatus.Poor;
            else if (timeSinceLastPong > KcpConstants.Timeouts.HealthFairMs)
                status = KcpConstants.HealthStatus.Fair;
            else
                status = KcpConstants.HealthStatus.Good;
                
            HealthStatusChanged?.Invoke(status);
        }

        private void OnMessageReceived(ReadOnlyMemory<byte> data)
        {
            _dispatcher.Dispatch(data);
        }

        private void OnPong(BaseMessage msg)
        {
            _lastPongTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            var pong = _dispatcher.ParsePayload<Pong>(msg);
            
            // RTT Calculation
            string content = pong.Content;
            int probeIndex = content.IndexOf("Probe:");
            if (probeIndex >= 0)
            {
                string tsStr = content.Substring(probeIndex + 6);
                var digits = new string(tsStr.TakeWhile(char.IsDigit).ToArray());
                if (long.TryParse(digits, out long sendTime))
                {
                    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var rtt = now - sendTime;
                    if (rtt >= 0 && rtt < 10000) 
                    {
                        RttUpdated?.Invoke(rtt);
                    }
                }
            }
        }

        private void OnRpcResponse(BaseMessage msg)
        {
            // Log logic moved to ViewModel via event or similar?
            // ConnectionService shouldn't depend on UI logging directly.
            // But MainViewModel subscribed to _client.OnLog.
            // Here we handle business logic.
        }
    }
}
