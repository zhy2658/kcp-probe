using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kcp.Core;
using KcpServer;
using KcpProbe.Models;
using KcpProbe.Services;
using Microsoft.UI.Dispatching;

namespace KcpProbe.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly DispatcherQueue _dispatcher;
        private readonly IKcpClient _kcpClient;
        private readonly ConnectionService _connectionService;
        private readonly StressTestService _stressTestService;
        private readonly RegressionService _regressionService;

        public KcpConfigViewModel Config { get; } = new KcpConfigViewModel();
        public KcpStatusViewModel Status { get; } = new KcpStatusViewModel();
        public BotViewModel Bot { get; }

        [ObservableProperty] private string _serverIp = KcpConstants.Config.DefaultIp;
        [ObservableProperty] private int _serverPort = KcpConstants.Config.DefaultPort;
        [ObservableProperty] private int _convId = KcpConstants.Config.DefaultConvId;

        private readonly ObservableCollection<LogEntry> _allLogs = new ObservableCollection<LogEntry>();
        public ObservableCollection<LogEntry> LogsView { get; } = new ObservableCollection<LogEntry>();

        [ObservableProperty] private string _filterKeyword = "";
        [ObservableProperty] private bool _showErrorsOnly;

        partial void OnFilterKeywordChanged(string value) => RefreshLogsView();
        partial void OnShowErrorsOnlyChanged(bool value) => RefreshLogsView();

        [ObservableProperty] private string _pingContent = "Hello KCP";
        [ObservableProperty] private string _apiMethod = "TestApi";
        [ObservableProperty] private string _apiParams = "{}";
        [ObservableProperty] private string _worldInfo = "Waiting for snapshots...";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StressButtonText))]
        private bool _isStressing;

        public string StressButtonText => IsStressing ? "Stop Stress" : "Start Stress";

        public event Action<double>? RttUpdated;

        public MainViewModel(
            IKcpClient kcpClient,
            ConnectionService connectionService,
            StressTestService stressTestService,
            RegressionService regressionService,
            BotManager botManager)
        {
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            _kcpClient = kcpClient;
            _connectionService = connectionService;
            _stressTestService = stressTestService;
            _regressionService = regressionService;

            _kcpClient.OnLog += Log;
            _connectionService.StatsUpdated += stats => _dispatcher.TryEnqueue(() => Status.UpdateStats(stats));
            _connectionService.HealthStatusChanged += status => _dispatcher.TryEnqueue(() => Status.HealthStatus = status);
            _connectionService.RttUpdated += rtt => _dispatcher.TryEnqueue(() => RttUpdated?.Invoke(rtt));
            _connectionService.WorldSnapshotReceived += OnWorldSnapshotReceived;

            _stressTestService.Log += Log;
            _stressTestService.IsStressingChanged += isStressing => _dispatcher.TryEnqueue(() => IsStressing = isStressing);

            _regressionService.Log += Log;
            _regressionService.IsRunningChanged += _ => _dispatcher.TryEnqueue(() =>
            {
                OnPropertyChanged(nameof(IsRunningRegression));
                OnPropertyChanged(nameof(RunRegressionButtonText));
                RunRegressionCommand.NotifyCanExecuteChanged();
                StopRegressionCommand.NotifyCanExecuteChanged();
            });

            var runtimeConfig = RuntimeConfig.LoadFromDisk();
            ServerIp = runtimeConfig.ServerIp;
            ServerPort = runtimeConfig.ServerPort;
            ConvId = runtimeConfig.ConvId;
            Config.NoDelay = runtimeConfig.Kcp.NoDelay;
            Config.Interval = runtimeConfig.Kcp.Interval;
            Config.Resend = runtimeConfig.Kcp.Resend;
            Config.Nc = runtimeConfig.Kcp.Nc;
            Config.SndWnd = runtimeConfig.Kcp.SndWnd;
            Config.RcvWnd = runtimeConfig.Kcp.RcvWnd;

            Bot = new BotViewModel(
                botManager,
                Config,
                () => (ServerIp, ServerPort, ConvId));

            RefreshLogsView();
        }

        private void Log(LogLevel level, string message)
        {
            _dispatcher.TryEnqueue(() =>
            {
                _allLogs.Add(new LogEntry(DateTime.Now, message, level));
                if (_allLogs.Count > 1000)
                {
                    _allLogs.RemoveAt(0);
                }
                RefreshLogsView();
            });
        }

        private void Log(string message)
        {
            Log(LogLevel.Info, message);
        }

        private void OnWorldSnapshotReceived(WorldSnapshot snapshot)
        {
            _dispatcher.TryEnqueue(() =>
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[Snapshot] Seq: {snapshot.Seq} | Time: {snapshot.ServerTime} | Count: {snapshot.Entities.Count}");
                foreach (var entity in snapshot.Entities)
                {
                    sb.AppendLine($" - ID:{entity.EntityId} HP:{entity.Hp} Pos:({entity.Pos?.X:F1}, {entity.Pos?.Y:F1}, {entity.Pos?.Z:F1})");
                }
                WorldInfo = sb.ToString();
            });
        }

        private void RefreshLogsView()
        {
            var query = _allLogs.AsEnumerable();
            if (ShowErrorsOnly)
            {
                query = query.Where(log => log.Level == LogLevel.Error);
            }
            if (!string.IsNullOrWhiteSpace(FilterKeyword))
            {
                query = query.Where(log => log.Message.Contains(FilterKeyword, StringComparison.OrdinalIgnoreCase));
            }

            var ordered = query.OrderByDescending(log => log.Time).ToList();
            LogsView.Clear();
            foreach (var log in ordered)
            {
                LogsView.Add(log);
            }
        }

        [RelayCommand]
        public void ClearLogs()
        {
            _allLogs.Clear();
        }

        [RelayCommand]
        private async Task ToggleConnect()
        {
            if (Status.IsConnected)
            {
                _connectionService.Disconnect();
                Status.SetConnected(false);
            }
            else
            {
                Status.ConnectionStatus = KcpConstants.ConnectionStatus.Connecting;
                try
                {
                    var config = Config.GetConfig();
                    await _connectionService.ConnectAsync(ServerIp, ServerPort, ConvId, config);
                    Status.SetConnected(true);
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Error, $"Connect Error: {ex.Message}");
                    Status.SetConnected(false);
                }
            }
        }

        [RelayCommand]
        private async Task SendPing()
        {
            if (!Status.IsConnected)
            {
                Log(LogLevel.Warning, "Not connected");
                return;
            }

            try
            {
                var ping = new Ping
                {
                    Content = PingContent,
                    SendTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                await _kcpClient.SendAsync(KcpConstants.MessageIds.Ping, ping);
                Log(LogLevel.Info, $"Sent Ping: {PingContent}");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Send Error: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SendRpc()
        {
            if (!Status.IsConnected)
            {
                Log(LogLevel.Warning, "Not connected");
                return;
            }

            try
            {
                var req = new RpcRequest
                {
                    Method = ApiMethod,
                    Params = ApiParams
                };
                await _kcpClient.SendAsync(KcpConstants.MessageIds.RpcRequest, req);
                Log(LogLevel.Info, $"Sent RPC: {ApiMethod} {ApiParams}");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Send RPC Error: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ToggleStress()
        {
            if (_stressTestService.IsStressing)
            {
                _stressTestService.StopStress();
            }
            else
            {
                _ = _stressTestService.StartStressAsync();
            }
        }

        // Regression
        public bool IsRunningRegression => _regressionService.IsRunningRegression;
        public string RunRegressionButtonText => IsRunningRegression ? "Running..." : "Run All Tests";

        [ObservableProperty]
        private bool _skipServerForRegression = true;

        [RelayCommand(CanExecute = nameof(CanRunRegression))]
        private async Task RunRegression()
        {
             await _regressionService.StartRegressionAsync(ServerIp, ServerPort, ConvId, SkipServerForRegression);
        }

        private bool CanRunRegression() => !IsRunningRegression;

        [RelayCommand(CanExecute = nameof(CanStopRegression))]
        private void StopRegression()
        {
            _regressionService.StopRegression();
        }

        private bool CanStopRegression() => IsRunningRegression;
    }
}
