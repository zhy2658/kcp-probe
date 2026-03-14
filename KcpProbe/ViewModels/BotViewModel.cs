using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kcp.Core;
using System.Threading.Tasks;

namespace KcpProbe.ViewModels
{
    public partial class BotViewModel : ObservableObject
    {
        private readonly BotManager _botManager;
        private readonly KcpConfigViewModel _configVm;
        private readonly string _serverIp;
        private readonly int _serverPort;
        private readonly int _startConvId;

        public BotViewModel(BotManager botManager, KcpConfigViewModel configVm, string serverIp, int serverPort, int startConvId)
        {
            _botManager = botManager;
            _configVm = configVm;
            _serverIp = serverIp;
            _serverPort = serverPort;
            _startConvId = startConvId;
        }

        [ObservableProperty]
        private int _botCount = 10;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BotButtonText))]
        private bool _isRunningBots;

        public string BotButtonText => IsRunningBots ? "Stop Bots" : "Start Bots";

        [RelayCommand]
        private async Task ToggleBots()
        {
            if (IsRunningBots)
            {
                _botManager.StopBots();
                IsRunningBots = false;
            }
            else
            {
                var config = _configVm.GetConfig();
                // Start bots with offset ConvId to avoid collision with main client
                await _botManager.StartBots(BotCount, _serverIp, _serverPort, _startConvId + 100, config);
                IsRunningBots = true;
            }
        }
    }
}
