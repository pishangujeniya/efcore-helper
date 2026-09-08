using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EFCoreHelper.Core.Models;

namespace EFCoreHelper.Core.Services
{
    public interface ISolutionScanner
    {
        Task<IReadOnlyList<ProjectInfo>> ScanSolutionAsync(string solutionDirectoryOrPath, CancellationToken cancellationToken = default);
        Task<ProjectInfo?> ScanProjectAsync(string projectFilePath, CancellationToken cancellationToken = default);
    }
}
