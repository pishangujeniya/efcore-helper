using System;
using System.Threading;
using System.Threading.Tasks;
using EFCoreHelper.Core.Models;

namespace EFCoreHelper.Core.Services
{
    public interface IEfCliRunner
    {
        event Action<string>? OutputLineReceived;
        event Action<string>? ErrorLineReceived;

        Task<ExecutionResult> ExecuteAsync(
            string fullCommandLine,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default);

        Task<ExecutionResult> ExecuteDotnetAsync(
            string dotnetArgs,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default);
    }
}
