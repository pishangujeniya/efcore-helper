using System;
using System.Text;

namespace EFCoreHelper.Core.Models
{
    public class ExecutionResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public TimeSpan ElapsedTime { get; set; }
        public string CommandText { get; set; } = string.Empty;
        public bool WasCancelled { get; set; }

        public static ExecutionResult CreateSuccess(string commandText, string output, TimeSpan elapsed)
        {
            return new ExecutionResult
            {
                Success = true,
                ExitCode = 0,
                CommandText = commandText,
                Output = output,
                ElapsedTime = elapsed
            };
        }

        public static ExecutionResult CreateFailure(string commandText, int exitCode, string output, string error, TimeSpan elapsed)
        {
            return new ExecutionResult
            {
                Success = false,
                ExitCode = exitCode,
                CommandText = commandText,
                Output = output,
                Error = error,
                ElapsedTime = elapsed
            };
        }

        public static ExecutionResult CreateCancelled(string commandText, TimeSpan elapsed)
        {
            return new ExecutionResult
            {
                Success = false,
                ExitCode = -1,
                CommandText = commandText,
                WasCancelled = true,
                Error = "Operation was cancelled by user.",
                ElapsedTime = elapsed
            };
        }
    }
}
