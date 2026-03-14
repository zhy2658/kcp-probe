using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Kcp.Core;
using KcpServer;
using Microsoft.UI.Dispatching;
using System.Linq;

namespace KcpProbe.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private KcpClient _client;
        private DispatcherQueue _dispatcher;

        public event PropertyChangedEventHandler? PropertyChanged;

        private string _serverIp = KcpConstants.Config.DefaultIp;
        public string ServerIp
        {
            get => _serverIp;
            set { _serverIp = value; OnPropertyChanged(nameof(ServerIp)); }
        }

        private int _serverPort = KcpConstants.Config.DefaultPort;
        public int ServerPort
        {
            get => _serverPort;
            set { _serverPort = value; OnPropertyChanged(nameof(ServerPort)); }
        }

        private int _convId = KcpConstants.Config.DefaultConvId;
        public int ConvId
        {
            get => _convId;
            set { _convId = value; OnPropertyChanged(nameof(ConvId)); }
        }

        #region KCP Config
        private bool _noDelay = KcpConstants.Config.DefaultNoDelay;
        public bool NoDelay { get => _noDelay; set { _noDelay = value; OnPropertyChanged(nameof(NoDelay)); } }

        private int _interval = KcpConstants.Config.DefaultInterval;
        public int Interval { get => _interval; set { _interval = value; OnPropertyChanged(nameof(Interval)); } }

        private int _resend = KcpConstants.Config.DefaultResend;
        public int Resend { get => _resend; set { _resend = value; OnPropertyChanged(nameof(Resend)); } }

        private bool _nc = KcpConstants.Config.DefaultNc;
        public bool Nc { get => _nc; set { _nc = value; OnPropertyChanged(nameof(Nc)); } }

        private int _sndWnd = KcpConstants.Config.DefaultSndWnd;
        public int SndWnd { get => _sndWnd; set { _sndWnd = value; OnPropertyChanged(nameof(SndWnd)); } }

        private int _rcvWnd = KcpConstants.Config.DefaultRcvWnd;
        public int RcvWnd { get => _rcvWnd; set { _rcvWnd = value; OnPropertyChanged(nameof(RcvWnd)); } }
        
        private string _statsInfo = "WaitSnd: 0 | Unacked: 0 | RTO: 0";
        public string StatsInfo { get => _statsInfo; set { _statsInfo = value; OnPropertyChanged(nameof(StatsInfo)); } }
        #endregion

        #region Status & Health
        private string _connectionStatus = KcpConstants.ConnectionStatus.Disconnected;
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set { _connectionStatus = value; OnPropertyChanged(nameof(ConnectionStatus)); OnPropertyChanged(nameof(StatusColor)); }
        }

        private string _healthStatus = KcpConstants.HealthStatus.Unknown;
        public string HealthStatus
        {
            get => _healthStatus;
            set { _healthStatus = value; OnPropertyChanged(nameof(HealthStatus)); OnPropertyChanged(nameof(HealthColor)); }
        }

        public string StatusColor => ConnectionStatus switch
        {
            KcpConstants.ConnectionStatus.Connected => "LightGreen",
            KcpConstants.ConnectionStatus.Connecting => "Yellow",
            _ => "Red"
        };

        public string HealthColor => HealthStatus switch
        {
            KcpConstants.HealthStatus.Good => "LightGreen",
            KcpConstants.HealthStatus.Fair => "Yellow",
            KcpConstants.HealthStatus.Poor => "Orange",
            KcpConstants.HealthStatus.Critical => "Red",
            _ => "Gray"
        };

        private long _lastPongTime = 0;
        #endregion

        // Bots
        private BotManager _botManager = new BotManager();
        private int _botCount = 10;
        public int BotCount { get => _botCount; set { _botCount = value; OnPropertyChanged(nameof(BotCount)); } }
        public bool IsRunningBots => _botManager.IsRunning;
        public string BotButtonText => IsRunningBots ? "Stop Bots" : "Start Bots";

        public async void ToggleBots()
        {
            if (_botManager.IsRunning)
            {
                _botManager.StopBots();
            }
            else
            {
                var config = new KcpConfig 
                {
                     NoDelay = NoDelay, Interval = Interval, Resend = Resend, Nc = Nc, SndWnd = SndWnd, RcvWnd = RcvWnd
                };
                // Start bots with offset ConvId to avoid collision with main client
                await _botManager.StartBots(BotCount, ServerIp, ServerPort, ConvId + 100, config);
            }
            OnPropertyChanged(nameof(IsRunningBots));
            OnPropertyChanged(nameof(BotButtonText));
        }

        // Visualization
        public event Action<double>? RttUpdated;

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(nameof(IsConnected)); OnPropertyChanged(nameof(ConnectButtonText)); }
        }

        public string ConnectButtonText => IsConnected ? "Disconnect" : "Connect";

        private string _pingContent = "Hello KCP";
        public string PingContent
        {
            get => _pingContent;
            set { _pingContent = value; OnPropertyChanged(nameof(PingContent)); }
        }

        public ObservableCollection<string> Logs { get; } = new ObservableCollection<string>();

        public MainViewModel()
        {
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            _client = new KcpClient();
            _client.OnLog += Log;
            _client.OnConnected += () => 
            { 
                IsConnected = true; 
                Log("Connected");
                ConnectionStatus = KcpConstants.ConnectionStatus.Connected;
                HealthStatus = KcpConstants.HealthStatus.Checking;
                _lastPongTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            };
            _client.OnDisconnected += () => 
            { 
                IsConnected = false; 
                Log("Disconnected"); 
                ConnectionStatus = KcpConstants.ConnectionStatus.Disconnected;
                HealthStatus = KcpConstants.HealthStatus.Unknown;
            };
            _client.OnMessageReceived += OnMessageReceived;

            PacketDispatcher.Instance.RegisterHandler(KcpConstants.MessageIds.Pong, OnPong); // Register Pong handler
            PacketDispatcher.Instance.RegisterHandler(KcpConstants.MessageIds.RpcResponse, OnRpcResponse); // Register RpcResponse handler
        }

        private void OnMessageReceived(byte[] data)
        {
            PacketDispatcher.Instance.Dispatch(data);
        }

        private void OnPong(BaseMessage msg)
        {
            _lastPongTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            var pong = PacketDispatcher.Instance.ParsePayload<Pong>(msg);
            
            // Handle both standard Probe:{ts} and server echoed Pong: Probe:{ts}
            string content = pong.Content;
            int probeIndex = content.IndexOf("Probe:");
            
            if (probeIndex >= 0)
            {
                string tsStr = content.Substring(probeIndex + 6);
                // Extract digits only in case there are suffixes
                var digits = new string(tsStr.TakeWhile(char.IsDigit).ToArray());
                
                if (long.TryParse(digits, out long sendTime))
                {
                    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var rtt = now - sendTime;
                    // Filter unrealistic RTTs (e.g. clock sync issues)
                    if (rtt >= 0 && rtt < 10000) 
                    {
                        _dispatcher.TryEnqueue(() => RttUpdated?.Invoke(rtt));
                    }
                }
            }

            Log($"Recv Pong: {pong.Content}, ServerTime: {pong.RecvTime}");
        }

        private void OnRpcResponse(BaseMessage msg)
        {
            var resp = PacketDispatcher.Instance.ParsePayload<RpcResponse>(msg);
            Log($"Recv RPC: {resp.Method} Code:{resp.Code} Result:{resp.Result} Error:{resp.ErrorMessage}");
        }

        private string _apiMethod = "TestApi";
        public string ApiMethod
        {
            get => _apiMethod;
            set { _apiMethod = value; OnPropertyChanged(nameof(ApiMethod)); }
        }

        private string _apiParams = "{}";
        public string ApiParams
        {
            get => _apiParams;
            set { _apiParams = value; OnPropertyChanged(nameof(ApiParams)); }
        }

        public async void SendRpc()
        {
            if (!IsConnected)
            {
                Log("Not connected");
                return;
            }

            try
            {
                var req = new RpcRequest
                {
                    Method = ApiMethod,
                    Params = ApiParams
                };

                await _client.SendAsync(KcpConstants.MessageIds.RpcRequest, req); // MsgId 100 is RpcRequest
                Log($"Sent RPC: {ApiMethod} {ApiParams}");
            }
            catch (Exception ex)
            {
                Log($"Send RPC Error: {ex.Message}");
            }
        }


        public async void ToggleConnect()
        {
            if (IsConnected)
            {
                _client.Disconnect();
            }
            else
            {
                ConnectionStatus = KcpConstants.ConnectionStatus.Connecting;
                try
                {
                    var config = new KcpConfig 
                    {
                        NoDelay = NoDelay,
                        Interval = Interval,
                        Resend = Resend,
                        Nc = Nc,
                        SndWnd = SndWnd,
                        RcvWnd = RcvWnd
                    };
                    await _client.ConnectAsync(ServerIp, ServerPort, ConvId, config);
                    StartStatsPolling();
                }
                catch (Exception ex)
                {
                    Log($"Connect Error: {ex.Message}");
                    ConnectionStatus = KcpConstants.ConnectionStatus.Disconnected;
                }
            }
        }
        
        private async void StartStatsPolling()
        {
            while (IsConnected)
            {
                var stats = _client.GetStats();
                if (stats != null)
                {
                    _dispatcher.TryEnqueue(() => 
                    {
                        StatsInfo = $"WaitSnd: {stats.WaitSnd} | Unacked: {stats.Unacked} | RTO: {stats.Rto}";
                    });
                }
                
                // Probe RTT
                try 
                {
                    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var ping = new Ping { Content = $"Probe:{now}", SendTime = (ulong)now };
                    await _client.SendAsync(KcpConstants.MessageIds.Ping, ping);
                    
                    // Health Check
                    long timeSinceLastPong = now - _lastPongTime;
                    if (timeSinceLastPong > KcpConstants.Timeouts.HealthCriticalMs)
                    {
                        HealthStatus = KcpConstants.HealthStatus.Critical;
                    }
                    else if (timeSinceLastPong > KcpConstants.Timeouts.HealthPoorMs)
                    {
                        HealthStatus = KcpConstants.HealthStatus.Poor;
                    }
                    else if (timeSinceLastPong > KcpConstants.Timeouts.HealthFairMs)
                    {
                        HealthStatus = KcpConstants.HealthStatus.Fair;
                    }
                    else
                    {
                        HealthStatus = KcpConstants.HealthStatus.Good;
                    }
                }
                catch (Exception)
                {
                     // Ignore send errors during polling to avoid log spam
                }
                
                await Task.Delay(KcpConstants.Timeouts.StatsPollingIntervalMs);
            }
        }

        public async void SendPing()
        {
            if (!IsConnected)
            {
                Log("Not connected");
                return;
            }

            try
            {
                var ping = new Ping
                {
                    Content = PingContent,
                    SendTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                await _client.SendAsync(KcpConstants.MessageIds.Ping, ping); // MsgId 1 is Ping
                Log($"Sent Ping: {PingContent}");
            }
            catch (Exception ex)
            {
                Log($"Send Error: {ex.Message}");
            }
        }
        
        // Stress Test Logic (Simplified)
        private bool _isStressing;
        private bool _isRunningRegression;
        private bool _skipServerForRegression = true;
        private Process? _regressionProcess;
        private bool _regressionStopRequested;

        public bool IsRunningRegression
        {
            get => _isRunningRegression;
            set
            {
                _isRunningRegression = value;
                OnPropertyChanged(nameof(IsRunningRegression));
                OnPropertyChanged(nameof(RunRegressionButtonText));
            }
        }

        public bool SkipServerForRegression
        {
            get => _skipServerForRegression;
            set
            {
                _skipServerForRegression = value;
                OnPropertyChanged(nameof(SkipServerForRegression));
            }
        }

        public string RunRegressionButtonText => IsRunningRegression ? "Running..." : "Run All Tests";

        public async void ToggleStress()
        {
             if (_isStressing)
             {
                 _isStressing = false;
                 Log("Stopping Stress Test...");
                 return;
             }
             
             if (!IsConnected)
             {
                 Log("Please connect first");
                 return;
             }

             _isStressing = true;
             Log("Starting Stress Test...");
             
             int count = 0;
             while (_isStressing && IsConnected)
             {
                 var ping = new Ping { Content = $"Stress {count++}", SendTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                 await _client.SendAsync(KcpConstants.MessageIds.Ping, ping);
                 await Task.Delay(KcpConstants.Timeouts.StressIntervalMs); // 100 QPS roughly per thread
             }
             _isStressing = false;
        }

        public async void RunRegression()
        {
            if (IsRunningRegression)
            {
                return;
            }

            var scriptPath = ResolveRegressionScriptPath();
            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                Log($"Regression script not found: {KcpConstants.Scripts.RegressionScript}");
                return;
            }

            IsRunningRegression = true;
            _regressionStopRequested = false;
            try
            {
                Log("Starting regression...");
                var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Ip {ServerIp} -Port {ServerPort} -Conv {ConvId}";
                if (SkipServerForRegression)
                {
                    args += " -SkipServer";
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true
                };

                var process = new Process { StartInfo = psi };
                _regressionProcess = process;
                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        Log($"[REG] {e.Data}");
                    }
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        Log($"[REG-ERR] {e.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                if (_regressionStopRequested)
                {
                    Log("Regression stopped by user");
                }
                else if (process.ExitCode == 0)
                {
                    Log("Regression finished: PASS");
                }
                else
                {
                    Log($"Regression finished: FAIL (ExitCode={process.ExitCode})");
                }

                process.Dispose();
            }
            catch (Exception ex)
            {
                Log($"Regression Error: {ex.Message}");
            }
            finally
            {
                _regressionProcess = null;
                _regressionStopRequested = false;
                IsRunningRegression = false;
            }
        }

        public void StopRegression()
        {
            if (!IsRunningRegression || _regressionProcess == null)
            {
                return;
            }

            try
            {
                if (!_regressionProcess.HasExited)
                {
                    _regressionStopRequested = true;
                    Log("Stopping regression...");
                    _regressionProcess.Kill(true);
                }
            }
            catch (Exception ex)
            {
                Log($"Stop regression error: {ex.Message}");
            }
        }

        private string? ResolveRegressionScriptPath()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 8 && current != null; i++)
            {
                var candidate = Path.Combine(current.FullName, KcpConstants.Scripts.RegressionScript);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            return null;
        }

        private void Log(string message)
        {
            _dispatcher.TryEnqueue(() =>
            {
                Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
                if (Logs.Count > 100) Logs.RemoveAt(Logs.Count - 1);
            });
        }

        public void ClearLogs()
        {
            _dispatcher.TryEnqueue(() => Logs.Clear());
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
