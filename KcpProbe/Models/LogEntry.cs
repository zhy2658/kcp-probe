using System;
using Kcp.Core;

namespace KcpProbe.Models
{
    public record LogEntry(DateTime Time, string Message, LogLevel Level)
    {
        public string FormattedTime => Time.ToString("HH:mm:ss");
        public string Color => Level switch
        {
            LogLevel.Error => "Red",
            LogLevel.Warning => "Orange",
            LogLevel.Success => "LightGreen",
            _ => "White"
        };
    }
}
