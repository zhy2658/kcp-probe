using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using KcpSharp;

namespace Kcp.Core
{
    public class KcpClient : IKcpClient
    {
        private Socket? _socket;
        private IDisposable? _transport;
        private KcpConversation? _kcpConv;
        private CancellationTokenSource? _cts;
        
        public event Action<byte[]>? OnMessageReceived;
        public event Action<string>? OnLog;
        public event Action? OnConnected;
        public event Action? OnDisconnected;

        public bool IsConnected => _kcpConv != null && !_kcpConv.TransportClosed;

        public async Task ConnectAsync(string ip, int port, int convId, KcpConfig? config = null)
        {
            Disconnect();

            config ??= new KcpConfig();

            _cts = new CancellationTokenSource();
            var remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            await _socket.ConnectAsync(remoteEndPoint);

            var options = new KcpConversationOptions
            {
                Mtu = config.Mtu,
                UpdateInterval = config.Interval,
                StreamMode = false,
                NoDelay = config.NoDelay,
                FastResend = config.Resend,
                DisableCongestionControl = config.Nc,
                SendWindow = config.SndWnd,
                ReceiveWindow = config.RcvWnd
            };
            
            var transport = KcpSocketTransport.CreateConversation(_socket, remoteEndPoint, convId, options);
            transport.Start();
            _transport = transport;
            
            _kcpConv = transport.Connection;
            
            OnLog?.Invoke($"Connected to {ip}:{port} with Conv {convId}");
            OnConnected?.Invoke();

            // Start receive loop
            _ = ReceiveLoopAsync(_cts.Token);
        }

        public KcpStats? GetStats()
        {
            if (_kcpConv == null) return null;
            
            var stats = new KcpStats();
            
            // UnflushedBytes is public
            stats.Unacked = (int)_kcpConv.UnflushedBytes;

            try 
            {
                // WaitSnd and RTO are not exposed publicly in KcpSharp 0.8.8
                // We have to use reflection to get them for monitoring purpose.
                var type = _kcpConv.GetType();
                var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                var f_snd_nxt = type.GetField("_snd_nxt", flags);
                var f_snd_una = type.GetField("_snd_una", flags);
                if (f_snd_nxt != null && f_snd_una != null)
                {
                    uint snd_nxt = (uint)f_snd_nxt.GetValue(_kcpConv);
                    uint snd_una = (uint)f_snd_una.GetValue(_kcpConv);
                    stats.WaitSnd = (int)(snd_nxt - snd_una);
                }

                var f_rx_rto = type.GetField("_rx_rto", flags);
                if (f_rx_rto != null) stats.Rto = Convert.ToInt32(f_rx_rto.GetValue(_kcpConv));
            }
            catch (Exception)
            {
            }
            
            return stats;
        }


        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            try
            {
                byte[] buffer = new byte[KcpConstants.Config.ReceiveBufferSize];
                while (!token.IsCancellationRequested && _kcpConv != null)
                {
                    var result = await _kcpConv.ReceiveAsync(buffer, token);
                    if (result.TransportClosed)
                    {
                        break;
                    }

                    if (result.BytesReceived > 0)
                    {
                        byte[] data = new byte[result.BytesReceived];
                        Array.Copy(buffer, data, result.BytesReceived);
                        OnMessageReceived?.Invoke(data);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    OnLog?.Invoke($"KCP Receive Error: {ex.Message}");
                    Disconnect();
                }
            }
        }

        public async Task SendAsync(uint msgId, IMessage message)
        {
            var baseMsg = new KcpServer.BaseMessage
            {
                MsgId = msgId,
                Payload = message.ToByteString(),
                Seq = 0,
                Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            await SendBaseMessageAsync(baseMsg);
        }

        public async Task SendBaseMessageAsync(KcpServer.BaseMessage baseMessage)
        {
            if (_kcpConv == null) return;

            byte[] data = baseMessage.ToByteArray();
            await _kcpConv.SendAsync(data, CancellationToken.None);
        }

        public void Disconnect()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts = null;
            }
            
            if (_transport != null)
            {
                _transport.Dispose();
                _transport = null;
                _kcpConv = null; 
            }

            if (_socket != null)
            {
                _socket.Dispose();
                _socket = null;
            }

            OnDisconnected?.Invoke();
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
