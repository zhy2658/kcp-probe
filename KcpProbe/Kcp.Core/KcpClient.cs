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
        public bool NoDelay { get; set; } = KcpConstants.Config.DefaultNoDelay;
        public int Interval { get; set; } = KcpConstants.Config.DefaultInterval;
        public int Resend { get; set; } = KcpConstants.Config.DefaultResend;
        public bool Nc { get; set; } = KcpConstants.Config.DefaultNc;
        public int SndWnd { get; set; } = KcpConstants.Config.DefaultSndWnd;
        public int RcvWnd { get; set; } = KcpConstants.Config.DefaultRcvWnd;
        public int Mtu { get; set; } = KcpConstants.Config.DefaultMtu;
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
            catch (Exception ex)
            { 
                // Ignore reflection errors as these methods might not exist in all KcpSharp versions
                OnLog?.Invoke($"[Warning] Failed to set KCP options via reflection: {ex.Message}");
            }
            
            OnLog?.Invoke($"Connected to {ip}:{port} with Conv {convId}");
            OnConnected?.Invoke();

            // Start receive loop
            _ = ReceiveLoopAsync(_cts.Token);
        }

        public KcpStats? GetStats()
        {
            if (_kcpConv == null) return null;
            
            var stats = new KcpStats();
            try 
            {
                var type = _kcpConv.GetType();
                var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

                // WaitSnd: _sndBuf count + _sendQueue count
                // Based on log: Field: _sndBuf = LinkedList, Field: _sendQueue = KcpSendQueue
                // We need to count items in these queues if possible, or look for a count field.
                // KcpSharp usually doesn't expose a simple "WaitSnd" count directly on Conversation.
                // But let's look at log again: Field: _snd_nxt = 2, Field: _snd_una = 2.
                // WaitSnd usually equals (snd_nxt - snd_una) + snd_queue_size
                
                var f_snd_nxt = type.GetField("_snd_nxt", flags);
                var f_snd_una = type.GetField("_snd_una", flags);
                if (f_snd_nxt != null && f_snd_una != null)
                {
                    uint snd_nxt = (uint)f_snd_nxt.GetValue(_kcpConv);
                    uint snd_una = (uint)f_snd_una.GetValue(_kcpConv);
                    stats.WaitSnd = (int)(snd_nxt - snd_una);
                }

                // UnflushedBytes: Prop: UnflushedBytes = 0
                var p_unflushed = type.GetProperty("UnflushedBytes", flags);
                if (p_unflushed != null) stats.Unacked = Convert.ToInt32(p_unflushed.GetValue(_kcpConv));

                // RTO: Field: _rx_rto = 100
                // Use Convert.ToInt32 to handle potential uint/int mismatch during unboxing
                var f_rx_rto = type.GetField("_rx_rto", flags);
                if (f_rx_rto != null) stats.Rto = Convert.ToInt32(f_rx_rto.GetValue(_kcpConv));
            }
            catch (Exception)
            {
                // Reflection might fail if internal fields change in newer KcpSharp versions.
                // We ignore this to avoid crashing the stats polling.
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
