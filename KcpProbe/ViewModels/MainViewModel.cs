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

namespace KcpProbe.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private KcpClient _client;
        private DispatcherQueue _dispatcher;

        public event PropertyChangedEventHandler? PropertyChanged;

        private string _serverIp = "127.0.0.1";
        public string ServerIp
        {
            get => _serverIp;
            set { _serverIp = value; OnPropertyChanged(nameof(ServerIp)); }
        }

        private int _serverPort = 8888;
        public int ServerPort
        {
            get => _serverPort;
            set { _serverPort = value; OnPropertyChanged(nameof(ServerPort)); }
        }

        private int _convId = 1001;
        public int ConvId
        {
            get => _convId;
            set { _convId = value; OnPropertyChanged(nameof(ConvId)); }
        }

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
            _client.OnConnected += () => { IsConnected = true; Log("Connected"); };
            _client.OnDisconnected += () => { IsConnected = false; Log("Disconnected"); };
            _client.OnMessageReceived += OnMessageReceived;

            PacketDispatcher.Instance.RegisterHandler(2, OnPong); // Register Pong handler
            PacketDispatcher.Instance.RegisterHandler(101, OnRpcResponse); // Register RpcResponse handler
        }

        private void OnMessageReceived(byte[] data)
        {
            PacketDispatcher.Instance.Dispatch(data);
        }

        private void OnPong(BaseMessage msg)
        {
            var pong = PacketDispatcher.Instance.ParsePayload<Pong>(msg);
            // ... (rest of the method)
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

                await _client.SendAsync(100, req); // MsgId 100 is RpcRequest
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
                try
                {
                    await _client.ConnectAsync(ServerIp, ServerPort, ConvId);
                }
                catch (Exception ex)
                {
                    Log($"Connect Error: {ex.Message}");
                }
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

                await _client.SendAsync(1, ping); // MsgId 1 is Ping
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
                 await _client.SendAsync(1, ping);
                 await Task.Delay(10); // 100 QPS roughly per thread
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
                Log("Regression script not found: run-regression.ps1");
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
                var candidate = Path.Combine(current.FullName, "run-regression.ps1");
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
