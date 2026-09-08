using System.Threading;
using System.Threading.Tasks;

namespace EFCoreHelper.Core.Services
{
    public interface IDotnetEfDetector
    {
        Task<DotnetEfStatus> CheckStatusAsync(CancellationToken cancellationToken = default);
        string GetInstallToolCommand();
        string GetUpdateToolCommand();
    }

    public class DotnetEfStatus
    {
        public bool IsDotnetInstalled { get; set; }
        public bool IsDotnetEfInstalled { get; set; }
        public string? DotnetVersion { get; set; }
        public string? DotnetEfVersion { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
