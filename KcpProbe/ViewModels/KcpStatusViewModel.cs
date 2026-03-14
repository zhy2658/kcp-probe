using CommunityToolkit.Mvvm.ComponentModel;
using Kcp.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace KcpProbe.ViewModels
{
    public partial class KcpStatusViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusColor))]
        private string _connectionStatus = KcpConstants.ConnectionStatus.Disconnected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HealthColor))]
        private string _healthStatus = KcpConstants.HealthStatus.Unknown;

        [ObservableProperty]
        private string _statsInfo = "WaitSnd: 0 | Unacked: 0 | RTO: 0";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ConnectButtonText))]
        private bool _isConnected;

        public string ConnectButtonText => IsConnected ? "Disconnect" : "Connect";

        public void UpdateStats(KcpStats stats)
        {
            if (stats == null) return;
            StatsInfo = $"WaitSnd: {stats.WaitSnd} | Unacked: {stats.Unacked} | RTO: {stats.Rto}";
        }

        public void SetConnected(bool connected)
        {
            IsConnected = connected;
            ConnectionStatus = connected ? KcpConstants.ConnectionStatus.Connected : KcpConstants.ConnectionStatus.Disconnected;
            if (!connected)
            {
                HealthStatus = KcpConstants.HealthStatus.Unknown;
            }
            else
            {
                HealthStatus = KcpConstants.HealthStatus.Checking;
            }
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(HealthColor));
        }

        public Brush StatusColor => new SolidColorBrush(ConnectionStatus switch
        {
            KcpConstants.ConnectionStatus.Connected => Colors.LightGreen,
            KcpConstants.ConnectionStatus.Connecting => Colors.Yellow,
            _ => Colors.Red
        });

        public Brush HealthColor => new SolidColorBrush(HealthStatus switch
        {
            KcpConstants.HealthStatus.Good => Colors.LightGreen,
            KcpConstants.HealthStatus.Fair => Colors.Yellow,
            KcpConstants.HealthStatus.Poor => Colors.Orange,
            KcpConstants.HealthStatus.Critical => Colors.Red,
            _ => Colors.Gray
        });
    }
}
