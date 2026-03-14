using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Collections;
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
        private readonly PacketDispatcher _packetDispatcher;
        
        // Services
        private readonly IKcpClient _kcpClient;
        private readonly ConnectionService _connectionService;
        private readonly StressTestService _stressTestService;
        private readonly RegressionService _regressionService;

        // Child ViewModels
        public KcpConfigViewModel Config { get; } = new KcpConfigViewModel();
        public KcpStatusViewModel Status { get; } = new KcpStatusViewModel();
        public BotViewModel Bot { get; }

        // Connection Params
        [ObservableProperty] private string _serverIp = KcpConstants.Config.DefaultIp;
        [ObservableProperty] private int _serverPort = KcpConstants.Config.DefaultPort;
        [ObservableProperty] private int _convId = KcpConstants.Config.DefaultConvId;

        // Logging
        private readonly ObservableCollection<LogEntry> _allLogs = new ObservableCollection<LogEntry>();
        public AdvancedCollectionView LogsView { get; }

        [ObservableProperty]
        private string _filterKeyword = "";

        [ObservableProperty]
        private bool _showErrorsOnly;

        partial void OnFilterKeywordChanged(string value) => LogsView.Refresh();
        partial void OnShowErrorsOnlyChanged(bool value) => LogsView.Refresh();

        // Interface Test
        [ObservableProperty] private string _pingContent = "Hello KCP";
        [ObservableProperty] private string _apiMethod = "TestApi";
        [ObservableProperty] private string _apiParams = "{}";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StressButtonText))]
        private bool _isStressing;

        public string StressButtonText => IsStressing ? "Stop Stress" : "Start Stress";

        // Events
        public event Action<double>? RttUpdated;

        public MainViewModel()
        {
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            _packetDispatcher = new PacketDispatcher();
            _kcpClient = new KcpClient();

            // Initialize Logs View
            LogsView = new AdvancedCollectionView(_allLogs, true);
            LogsView.SortDescriptions.Add(new SortDescription("Time", SortDirection.Descending));
            LogsView.Filter = item =>
            {
                if (item is not LogEntry log) return false;

                if (ShowErrorsOnly && log.Level != LogLevel.Error)
                    return false;

                if (!string.IsNullOrWhiteSpace(FilterKeyword))
                {
                    if (!log.Message.Contains(FilterKeyword, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                return true;
            };
            
            // Log forwarding
            _kcpClient.OnLog += Log;

            // Initialize Services
            _connectionService = new ConnectionService(_kcpClient, _packetDispatcher);
            _connectionService.StatsUpdated += stats => _dispatcher.TryEnqueue(() => Status.UpdateStats(stats));
            _connectionService.HealthStatusChanged += status => _dispatcher.TryEnqueue(() => Status.HealthStatus = status);
            _connectionService.RttUpdated += rtt => _dispatcher.TryEnqueue(() => RttUpdated?.Invoke(rtt));

            _stressTestService = new StressTestService(_kcpClient);
            _stressTestService.Log += Log;
            _stressTestService.IsStressingChanged += isStressing => _dispatcher.TryEnqueue(() => IsStressing = isStressing);

            _regressionService = new RegressionService();
            _regressionService.Log += Log;
            _regressionService.IsRunningChanged += running => _dispatcher.TryEnqueue(() => 
            {
                 OnPropertyChanged(nameof(IsRunningRegression));
                 OnPropertyChanged(nameof(RunRegressionButtonText));
                 RunRegressionCommand.NotifyCanExecuteChanged();
                 StopRegressionCommand.NotifyCanExecuteChanged();
            });

            // Initialize Bot VM
            var botManager = new BotManager(); 
            Bot = new BotViewModel(botManager, Config, ServerIp, ServerPort, ConvId);
        }

        private void Log(LogLevel level, string message)
        {
            _dispatcher.TryEnqueue(() =>
            {
                // Optimization: Use Add (O(1)) instead of Insert(0) (O(N)).
                // LogsView handles sorting (Time Descending).
                _allLogs.Add(new LogEntry(DateTime.Now, message, level));
                
                if (_allLogs.Count > 1000)
                {
                    // Remove oldest (which was added first, so index 0)
                    _allLogs.RemoveAt(0);
                }
            });
        }
        
        // Helper for internal logging without level (default to Info or Error based on simple check if needed, but better to be explicit)
        private void Log(string message)
        {
             Log(LogLevel.Info, message);
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
        [ObservableProperty] private bool _skipServerForRegression = true;

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
