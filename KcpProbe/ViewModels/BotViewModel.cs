using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kcp.Core;
using System;
using System.Threading.Tasks;

namespace KcpProbe.ViewModels
{
    public partial class BotViewModel : ObservableObject
    {
        private readonly BotManager _botManager;
        private readonly KcpConfigViewModel _configVm;
        private readonly Func<(string ip, int port, int convId)> _endpointProvider;

        public BotViewModel(BotManager botManager, KcpConfigViewModel configVm, Func<(string ip, int port, int convId)> endpointProvider)
        {
            _botManager = botManager;
            _configVm = configVm;
            _endpointProvider = endpointProvider;
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
                var endpoint = _endpointProvider.Invoke();
                await _botManager.StartBots(BotCount, endpoint.ip, endpoint.port, endpoint.convId + 100, config);
                IsRunningBots = true;
            }
        }
    }
}
