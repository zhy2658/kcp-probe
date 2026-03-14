using System;

namespace KcpProbe.Models
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Success
    }

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
