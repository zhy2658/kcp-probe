using System;
using System.IO;

namespace Kcp.Core
{
    public sealed class RuntimeConfig
    {
        public string ServerIp { get; set; } = KcpConstants.Config.DefaultIp;
        public int ServerPort { get; set; } = KcpConstants.Config.DefaultPort;
        public int ConvId { get; set; } = KcpConstants.Config.DefaultConvId;
        public KcpConfig Kcp { get; set; } = new KcpConfig();

        public static RuntimeConfig LoadFromDisk()
        {
            var config = new RuntimeConfig();
            var yamlPath = ResolveConfigPath();
            if (string.IsNullOrWhiteSpace(yamlPath) || !File.Exists(yamlPath))
            {
                return config;
            }

            string section = string.Empty;
            foreach (var rawLine in File.ReadLines(yamlPath))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.EndsWith(":", StringComparison.Ordinal))
                {
                    section = line[..^1];
                    continue;
                }

                var kv = line.Split(':', 2);
                if (kv.Length != 2)
                {
                    continue;
                }

                var key = kv[0].Trim();
                var value = kv[1].Trim().Trim('"');

                if (section.Equals("server", StringComparison.OrdinalIgnoreCase))
                {
                    if (key.Equals("ip", StringComparison.OrdinalIgnoreCase))
                    {
                        config.ServerIp = value == "0.0.0.0" ? "127.0.0.1" : value;
                    }
                    else if (key.Equals("port", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var port))
                    {
                        config.ServerPort = port;
                    }
                }
                else if (section.Equals("kcp", StringComparison.OrdinalIgnoreCase))
                {
                    switch (key.ToLowerInvariant())
                    {
                        case "nodelay":
                            config.Kcp.NoDelay = ParseBool(value, config.Kcp.NoDelay);
                            break;
                        case "interval":
                            config.Kcp.Interval = ParseInt(value, config.Kcp.Interval);
                            break;
                        case "resend":
                            config.Kcp.Resend = ParseInt(value, config.Kcp.Resend);
                            break;
                        case "nc":
                            config.Kcp.Nc = ParseBool(value, config.Kcp.Nc);
                            break;
                        case "sndwnd":
                            config.Kcp.SndWnd = ParseInt(value, config.Kcp.SndWnd);
                            break;
                        case "rcvwnd":
                            config.Kcp.RcvWnd = ParseInt(value, config.Kcp.RcvWnd);
                            break;
                        case "mtu":
                            config.Kcp.Mtu = ParseInt(value, config.Kcp.Mtu);
                            break;
                    }
                }
            }

            return config;
        }

        private static string? ResolveConfigPath()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 12 && dir != null; i++)
            {
                var candidate = Path.Combine(dir.FullName, "cpp-server", "config.yaml");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }

            return null;
        }

        private static int ParseInt(string value, int fallback)
        {
            return int.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private static bool ParseBool(string value, bool fallback)
        {
            if (int.TryParse(value, out var parsed))
            {
                return parsed != 0;
            }
            if (bool.TryParse(value, out var parsedBool))
            {
                return parsedBool;
            }
            return fallback;
        }
    }
}
