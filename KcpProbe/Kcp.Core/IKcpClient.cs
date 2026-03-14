using System;
using System.Threading.Tasks;
using Google.Protobuf;

namespace Kcp.Core
{
    public interface IKcpClient : IDisposable
    {
        event Action<byte[]>? OnMessageReceived;
        event Action<string>? OnLog;
        event Action? OnConnected;
        event Action? OnDisconnected;

        bool IsConnected { get; }

        Task ConnectAsync(string ip, int port, int convId, KcpConfig? config = null);
        void Disconnect();
        Task SendAsync(uint msgId, IMessage message);
        KcpStats? GetStats();
    }
}
