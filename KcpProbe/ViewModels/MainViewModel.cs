using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Kcp.Core;
using KcpServer;
using Microsoft.UI.Dispatching;

namespace KcpProbe.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private KcpClient _client;
        private DispatcherQueue _dispatcher;

        public event PropertyChangedEventHandler PropertyChanged;

        private string _serverIp = "127.0.0.1";
        public string ServerIp
        {
            get => _serverIp;
            set { _serverIp = value; OnPropertyChanged(nameof(ServerIp)); }
        }

        private int _serverPort = 12345;
        public int ServerPort
        {
            get => _serverPort;
            set { _serverPort = value; OnPropertyChanged(nameof(ServerPort)); }
        }

        private int _convId = 1001;
        public int ConvId
        {
            get => _convId;
            set { _convId = value; OnPropertyChanged(nameof(ConvId)); }
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(nameof(IsConnected)); OnPropertyChanged(nameof(ConnectButtonText)); }
        }

        public string ConnectButtonText => IsConnected ? "Disconnect" : "Connect";

        private string _pingContent = "Hello KCP";
        public string PingContent
        {
            get => _pingContent;
            set { _pingContent = value; OnPropertyChanged(nameof(PingContent)); }
        }

        public ObservableCollection<string> Logs { get; } = new ObservableCollection<string>();

        public MainViewModel()
        {
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            _client = new KcpClient();
            _client.OnLog += Log;
            _client.OnConnected += () => { IsConnected = true; Log("Connected"); };
            _client.OnDisconnected += () => { IsConnected = false; Log("Disconnected"); };
            _client.OnMessageReceived += OnMessageReceived;

            PacketDispatcher.Instance.RegisterHandler(2, OnPong); // Register Pong handler
        }

        private void OnMessageReceived(byte[] data)
        {
            PacketDispatcher.Instance.Dispatch(data);
        }

        private void OnPong(BaseMessage msg)
        {
            var pong = PacketDispatcher.Instance.ParsePayload<Pong>(msg);
            var rtt = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - pong.RecvTime; 
            // Note: server sets recv_time, usually RTT is current - send_time. 
            // But let's just log what we got.
            // Wait, server logic:
            // pong.set_recv_time(current_ms());
            // So pong.RecvTime is server time.
            // To calc RTT, we need to know when we sent it.
            // For simple test, just show content.
            Log($"Recv Pong: {pong.Content}, ServerTime: {pong.RecvTime}");
        }

        public async void ToggleConnect()
        {
            if (IsConnected)
            {
                _client.Disconnect();
            }
            else
            {
                try
                {
                    await _client.ConnectAsync(ServerIp, ServerPort, ConvId);
                }
                catch (Exception ex)
                {
                    Log($"Connect Error: {ex.Message}");
                }
            }
        }

        public async void SendPing()
        {
            if (!IsConnected)
            {
                Log("Not connected");
                return;
            }

            try
            {
                var ping = new Ping
                {
                    Content = PingContent,
                    SendTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                await _client.SendAsync(1, ping); // MsgId 1 is Ping
                Log($"Sent Ping: {PingContent}");
            }
            catch (Exception ex)
            {
                Log($"Send Error: {ex.Message}");
            }
        }
        
        // Stress Test Logic (Simplified)
        private bool _isStressing;
        public async void ToggleStress()
        {
             if (_isStressing)
             {
                 _isStressing = false;
                 Log("Stopping Stress Test...");
                 return;
             }
             
             if (!IsConnected)
             {
                 Log("Please connect first");
                 return;
             }

             _isStressing = true;
             Log("Starting Stress Test...");
             
             int count = 0;
             while (_isStressing && IsConnected)
             {
                 var ping = new Ping { Content = $"Stress {count++}", SendTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                 await _client.SendAsync(1, ping);
                 await Task.Delay(10); // 100 QPS roughly per thread
             }
             _isStressing = false;
        }

        private void Log(string message)
        {
            _dispatcher.TryEnqueue(() =>
            {
                Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
                if (Logs.Count > 100) Logs.RemoveAt(Logs.Count - 1);
            });
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
