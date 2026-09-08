using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EFCoreHelper.Core.Models;

namespace EFCoreHelper.Core.Services
{
    public class EfCliRunner : IEfCliRunner
    {
        public event Action<string>? OutputLineReceived;
        public event Action<string>? ErrorLineReceived;

        public Task<ExecutionResult> ExecuteAsync(
            string fullCommandLine,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fullCommandLine))
                throw new ArgumentException("Command line cannot be empty.", nameof(fullCommandLine));

            string command = fullCommandLine.Trim();
            string dotnetArgs;

            if (command.StartsWith("dotnet ", StringComparison.OrdinalIgnoreCase))
            {
                dotnetArgs = command.Substring("dotnet ".Length).Trim();
            }
            else if (command.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                dotnetArgs = string.Empty;
            }
            else
            {
                dotnetArgs = command;
            }

            return ExecuteDotnetAsync(dotnetArgs, workingDirectory, cancellationToken);
        }

        public async Task<ExecutionResult> ExecuteDotnetAsync(
            string dotnetArgs,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            string fullDisplayCommand = $"dotnet {dotnetArgs}".Trim();

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = dotnetArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            using (var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true })
            {
                var tcs = new TaskCompletionSource<int>();

                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                        OutputLineReceived?.Invoke(e.Data);
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                        ErrorLineReceived?.Invoke(e.Data);
                    }
                };

                process.Exited += (s, e) =>
                {
                    tcs.TrySetResult(process.ExitCode);
                };

                try
                {
                    if (!process.Start())
                    {
                        stopwatch.Stop();
                        return ExecutionResult.CreateFailure(
                            fullDisplayCommand,
                            -1,
                            string.Empty,
                            "Failed to start dotnet process.",
                            stopwatch.Elapsed);
                    }
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    return ExecutionResult.CreateFailure(
                        fullDisplayCommand,
                        -1,
                        string.Empty,
                        $"Failed to start process: {ex.Message}",
                        stopwatch.Elapsed);
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            KillProcessTree(process);
                        }
                    }
                    catch
                    {
                        // Ignore errors during emergency kill
                    }
                    tcs.TrySetCanceled();
                }))
                {
                    try
                    {
                        int exitCode = await tcs.Task.ConfigureAwait(false);
                        stopwatch.Stop();

                        string outText = outputBuilder.ToString();
                        string errText = errorBuilder.ToString();

                        if (exitCode == 0)
                        {
                            return ExecutionResult.CreateSuccess(fullDisplayCommand, outText, stopwatch.Elapsed);
                        }

                        return ExecutionResult.CreateFailure(fullDisplayCommand, exitCode, outText, errText, stopwatch.Elapsed);
                    }
                    catch (OperationCanceledException)
                    {
                        stopwatch.Stop();
                        return ExecutionResult.CreateCancelled(fullDisplayCommand, stopwatch.Elapsed);
                    }
                }
            }
        }

        private static void KillProcessTree(Process process)
        {
            try
            {
#if NET8_0_OR_GREATER
                process.Kill(entireProcessTree: true);
#else
                try
                {
                    var killer = Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/PID {process.Id} /T /F",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    killer?.WaitForExit(3000);
                }
                catch
                {
                    process.Kill();
                }
#endif
            }
            catch
            {
                // Process may have already exited
            }
        }
    }
}
