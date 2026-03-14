using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kcp.Core;
using KcpServer;

namespace Kcp.Core
{
    public class BotManager
    {
        private List<IKcpClient> _bots = new List<IKcpClient>();
        public bool IsRunning { get; private set; }

        public async Task StartBots(int count, string ip, int port, int startConvId, KcpConfig config)
        {
            StopBots();
            IsRunning = true;

            for (int i = 0; i < count; i++)
            {
                if (!IsRunning) break;
                
                var bot = new KcpClient();
                // Simple bot logic: connect, then loop ping
                try 
                {
                    await bot.ConnectAsync(ip, port, startConvId + i, config);
                    _bots.Add(bot);
                    // Fire and forget, but keep task reference if needed?
                    // Original code: _ = BotLoop(bot, i);
                    _ = BotLoop(bot, i);
                }
                catch
                {
                    // Ignore connection failures for individual bots
                }
                
                // Stagger connections slightly to avoid thundering herd
                if (i % 10 == 0) await Task.Delay(10);
            }
        }

        private async Task BotLoop(IKcpClient bot, int index)
        {
            while (IsRunning && bot.IsConnected)
            {
                try
                {
                    var ping = new Ping 
                    { 
                        Content = $"Bot{index}", 
                        SendTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() 
                    };
                    await bot.SendAsync(KcpConstants.MessageIds.Ping, ping);
                    await Task.Delay(100); // 10Hz
                }
                catch
                {
                    break;
                }
            }
        }

        public void StopBots()
        {
            IsRunning = false;
            foreach (var bot in _bots)
            {
                bot.Dispose();
            }
            _bots.Clear();
        }
    }
}
