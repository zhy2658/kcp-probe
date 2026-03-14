namespace Kcp.Core
{
    public class KcpStats
    {
        public int WaitSnd { get; set; }
        public int Unacked { get; set; } // Packets sent but not acked
        public int Rto { get; set; }
    }
}
