using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
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
        
        // Reflection cache
        private static FieldInfo? _f_snd_nxt;
        private static FieldInfo? _f_snd_una;
        private static FieldInfo? _f_rx_rto;
        private static bool _reflectionInitialized;

        public event Action<ReadOnlyMemory<byte>>? OnMessageReceived;
        public event Action<LogLevel, string>? OnLog;
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
            
            // Initialize reflection cache once
            if (!_reflectionInitialized)
            {
                InitializeReflection(_kcpConv.GetType());
            }

            OnLog?.Invoke(LogLevel.Success, $"Connected to {ip}:{port} with Conv {convId}");
            OnConnected?.Invoke();

            // Start receive loop
            _ = ReceiveLoopAsync(_cts.Token);
        }

        private static void InitializeReflection(Type type)
        {
            try
            {
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                _f_snd_nxt = type.GetField("_snd_nxt", flags);
                _f_snd_una = type.GetField("_snd_una", flags);
                _f_rx_rto = type.GetField("_rx_rto", flags);
                _reflectionInitialized = true;
            }
            catch
            {
                // Ignore reflection errors
            }
        }

        public KcpStats? GetStats()
        {
            if (_kcpConv == null) return null;
            
            var stats = new KcpStats();
            
            // UnflushedBytes is public
            stats.Unacked = (int)_kcpConv.UnflushedBytes;

            try 
            {
                if (_f_snd_nxt != null && _f_snd_una != null)
                {
                    uint snd_nxt = (uint)_f_snd_nxt.GetValue(_kcpConv)!;
                    uint snd_una = (uint)_f_snd_una.GetValue(_kcpConv)!;
                    stats.WaitSnd = (int)(snd_nxt - snd_una);
                }

                if (_f_rx_rto != null) 
                {
                    stats.Rto = Convert.ToInt32(_f_rx_rto.GetValue(_kcpConv));
                }
            }
            catch (Exception ex)
            {
                // Log only in debug to avoid spam
                #if DEBUG
                System.Diagnostics.Debug.WriteLine($"Stats Error: {ex.Message}");
                #endif
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
                        // Optimization: Use ReadOnlyMemory slice to avoid allocation.
                        // Note: Subscribers must not hold onto this memory beyond the synchronous callback execution.
                        var slice = new ReadOnlyMemory<byte>(buffer, 0, result.BytesReceived);
                        OnMessageReceived?.Invoke(slice);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    OnLog?.Invoke(LogLevel.Error, $"KCP Receive Error: {ex.Message}");
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
