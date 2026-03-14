using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kcp.Core;

namespace KcpProbe.Services
{
    public class RegressionService
    {
        private Process? _regressionProcess;
        private bool _isRunningRegression;
        private bool _regressionStopRequested;

        public event Action<bool>? IsRunningChanged;
        public event Action<LogLevel, string>? Log;

        public bool IsRunningRegression
        {
            get => _isRunningRegression;
            private set
            {
                if (_isRunningRegression != value)
                {
                    _isRunningRegression = value;
                    IsRunningChanged?.Invoke(value);
                }
            }
        }

        public async Task StartRegressionAsync(string serverIp, int serverPort, int convId, bool skipServer)
        {
            if (IsRunningRegression) return;

            var scriptPath = ResolveRegressionScriptPath();
            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                Log?.Invoke(LogLevel.Error, $"Regression script not found: {KcpConstants.Scripts.RegressionScript}");
                return;
            }

            IsRunningRegression = true;
            _regressionStopRequested = false;

            try
            {
                Log?.Invoke(LogLevel.Info, "Starting regression...");
                var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -Ip {serverIp} -Port {serverPort} -Conv {convId}";
                if (skipServer)
                {
                    args += " -SkipServer";
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };
                _regressionProcess = process;
                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        var level = LogLevel.Info;
                        if (e.Data.Contains("FAIL") || e.Data.Contains("Error", StringComparison.OrdinalIgnoreCase))
                            level = LogLevel.Error;
                        else if (e.Data.Contains("PASS"))
                            level = LogLevel.Success;
                        
                        Log?.Invoke(level, $"[REG] {e.Data}");
                    }
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        Log?.Invoke(LogLevel.Error, $"[REG-ERR] {e.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Set a hard timeout of 60 seconds to prevent hanging
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                try 
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    Log?.Invoke(LogLevel.Error, "Regression timed out (60s). Killing process...");
                    process.Kill(true);
                }

                if (_regressionStopRequested)
                {
                    Log?.Invoke(LogLevel.Warning, "Regression stopped by user");
                }
                else if (process.ExitCode == 0)
                {
                    Log?.Invoke(LogLevel.Success, "Regression finished: PASS");
                }
                else
                {
                    Log?.Invoke(LogLevel.Error, $"Regression finished: FAIL (ExitCode={process.ExitCode})");
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke(LogLevel.Error, $"Regression Error: {ex.Message}");
            }
            finally
            {
                _regressionProcess = null;
                _regressionStopRequested = false;
                IsRunningRegression = false;
            }
        }

        public void StopRegression()
        {
            if (!IsRunningRegression || _regressionProcess == null) return;

            try
            {
                if (!_regressionProcess.HasExited)
                {
                    _regressionStopRequested = true;
                    Log?.Invoke(LogLevel.Warning, "Stopping regression...");
                    _regressionProcess.Kill(true);
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke(LogLevel.Error, $"Stop regression error: {ex.Message}");
            }
        }

        private string? ResolveRegressionScriptPath()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 8 && current != null; i++)
            {
                var candidate = Path.Combine(current.FullName, KcpConstants.Scripts.RegressionScript);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            return null;
        }
    }
}
