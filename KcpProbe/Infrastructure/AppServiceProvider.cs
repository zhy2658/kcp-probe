using Kcp.Core;
using KcpProbe.Services;
using KcpProbe.ViewModels;

namespace KcpProbe.Infrastructure
{
    public sealed class AppServiceProvider
    {
        public IKcpClient KcpClient { get; }
        public PacketDispatcher PacketDispatcher { get; }
        public ConnectionService ConnectionService { get; }
        public StressTestService StressTestService { get; }
        public RegressionService RegressionService { get; }
        public BotManager BotManager { get; }

        public AppServiceProvider()
        {
            KcpClient = new KcpClient();
            PacketDispatcher = new PacketDispatcher();
            ConnectionService = new ConnectionService(KcpClient, PacketDispatcher);
            StressTestService = new StressTestService(KcpClient);
            RegressionService = new RegressionService();
            BotManager = new BotManager();
        }

        public MainViewModel CreateMainViewModel()
        {
            return new MainViewModel(
                KcpClient,
                ConnectionService,
                StressTestService,
                RegressionService,
                BotManager);
        }
    }
}
