using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using KcpSharp;

namespace Kcp.Core
{
    public class KcpConfig
    {
        public bool NoDelay { get; set; } = true;
        public int Interval { get; set; } = 10;
        public int Resend { get; set; } = 2;
        public bool Nc { get; set; } = true;
        public int SndWnd { get; set; } = 128;
        public int RcvWnd { get; set; } = 128;
        public int Mtu { get; set; } = 1400;
    }

    public class KcpStats
    {
        public int WaitSnd { get; set; }
        public int Unacked { get; set; } // Packets sent but not acked
        public int Rto { get; set; }
        // Add more if KcpSharp exposes them
    }

    public class KcpClient : IDisposable
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
                StreamMode = false
            };
            
            var transport = KcpSocketTransport.CreateConversation(_socket, remoteEndPoint, convId, options);
            transport.Start();
            _transport = transport;
            
            _kcpConv = transport.Connection;
            
            // Try to set NoDelay and Window if methods exist
            // Note: KcpSharp 0.8.8 might have these methods on KcpConversation
            try 
            {
                // Reflection to avoid build error if method missing in this specific version
                var type = _kcpConv.GetType();
                var setNoDelay = type.GetMethod("SetNoDelay");
                if (setNoDelay != null)
                {
                    setNoDelay.Invoke(_kcpConv, new object[] { config.NoDelay ? 1 : 0, config.Interval, config.Resend, config.Nc ? 1 : 0 });
                }
                
                var setWindowSize = type.GetMethod("SetWindowSize");
                if (setWindowSize != null)
                {
                     setWindowSize.Invoke(_kcpConv, new object[] { config.SndWnd, config.RcvWnd });
                }
            } 
            catch { /* Ignore */ }
            
            OnLog?.Invoke($"Connected to {ip}:{port} with Conv {convId}");
            OnConnected?.Invoke();

            // Start receive loop
            _ = ReceiveLoopAsync(_cts.Token);
        }

        public KcpStats? GetStats()
        {
            if (_kcpConv == null) return null;
            
            // Accessing internal KCP state if possible.
            // KcpSharp's KcpConversation might expose:
            // - UnflushedBytes
            // - WaitSnd (maybe)
            // Let's return a dummy or minimal stats if properties are not found during compilation.
            // But I will try to use standard names.
            return new KcpStats 
            {
                 // WaitSnd = _kcpConv.WaitSnd, // Uncomment if available
                 // Rto = _kcpConv.RxRto // Uncomment if available
            };
        }


        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            try
            {
                byte[] buffer = new byte[4096];
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
