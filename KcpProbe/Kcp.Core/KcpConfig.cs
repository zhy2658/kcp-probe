namespace Kcp.Core
{
    public class KcpConfig
    {
        public bool NoDelay { get; set; } = KcpConstants.Config.DefaultNoDelay;
        public int Interval { get; set; } = KcpConstants.Config.DefaultInterval;
        public int Resend { get; set; } = KcpConstants.Config.DefaultResend;
        public bool Nc { get; set; } = KcpConstants.Config.DefaultNc;
        public int SndWnd { get; set; } = KcpConstants.Config.DefaultSndWnd;
        public int RcvWnd { get; set; } = KcpConstants.Config.DefaultRcvWnd;
        public int Mtu { get; set; } = KcpConstants.Config.DefaultMtu;
    }
}
