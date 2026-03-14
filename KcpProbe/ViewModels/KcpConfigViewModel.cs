using CommunityToolkit.Mvvm.ComponentModel;
using Kcp.Core;

namespace KcpProbe.ViewModels
{
    public partial class KcpConfigViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _noDelay = KcpConstants.Config.DefaultNoDelay;

        [ObservableProperty]
        private int _interval = KcpConstants.Config.DefaultInterval;

        [ObservableProperty]
        private int _resend = KcpConstants.Config.DefaultResend;

        [ObservableProperty]
        private bool _nc = KcpConstants.Config.DefaultNc;

        [ObservableProperty]
        private int _sndWnd = KcpConstants.Config.DefaultSndWnd;

        [ObservableProperty]
        private int _rcvWnd = KcpConstants.Config.DefaultRcvWnd;

        public KcpConfig GetConfig()
        {
            return new KcpConfig
            {
                NoDelay = NoDelay,
                Interval = Interval,
                Resend = Resend,
                Nc = Nc,
                SndWnd = SndWnd,
                RcvWnd = RcvWnd,
                Mtu = KcpConstants.Config.DefaultMtu
            };
        }
    }
}
