using System;

namespace Kcp.Core
{
    public static class KcpConstants
    {
        public static class Config
        {
            public const string DefaultIp = "127.0.0.1";
            public const int DefaultPort = 8888;
            public const int DefaultConvId = 1001;
            public const int DefaultMtu = 1400;
            public const int DefaultSndWnd = 128;
            public const int DefaultRcvWnd = 128;
            public const int DefaultInterval = 10;
            public const int DefaultResend = 2;
            public const bool DefaultNoDelay = true;
            public const bool DefaultNc = true;
            public const int ReceiveBufferSize = 4096;
        }

        public static class MessageIds
        {
            public const uint Ping = 1;
            public const uint Pong = 2;
            public const uint RpcRequest = 100;
            public const uint RpcResponse = 101;
            public const uint WorldSnapshot = 2001;
            public const uint Unknown = 65000;
        }

        public static class ConnectionStatus
        {
            public const string Disconnected = "Disconnected";
            public const string Connected = "Connected";
            public const string Connecting = "Connecting...";
        }

        public static class HealthStatus
        {
            public const string Unknown = "Unknown";
            public const string Checking = "Checking...";
            public const string Good = "Good";
            public const string Fair = "Fair";
            public const string Poor = "Poor";
            public const string Critical = "Critical";
        }

        public static class Timeouts
        {
            public const int HealthCriticalMs = 5000;
            public const int HealthPoorMs = 2000;
            public const int HealthFairMs = 500;
            public const int StressIntervalMs = 10;
            public const int StatsPollingIntervalMs = 500;
        }

        public static class Scripts
        {
            public const string RegressionScript = "run-regression.ps1";
        }
    }
}
