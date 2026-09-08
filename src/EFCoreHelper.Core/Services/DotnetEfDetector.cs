using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EFCoreHelper.Core.Services
{
    public class DotnetEfDetector : IDotnetEfDetector
    {
        private readonly IEfCliRunner _cliRunner;
        private static readonly Regex VersionRegex = new Regex(@"\b(\d+\.\d+\.\d+(?:-[a-zA-Z0-9.]+)?)\b", RegexOptions.Compiled);

        public DotnetEfDetector(IEfCliRunner cliRunner)
        {
            _cliRunner = cliRunner ?? throw new ArgumentNullException(nameof(cliRunner));
        }

        public async Task<DotnetEfStatus> CheckStatusAsync(CancellationToken cancellationToken = default)
        {
            var status = new DotnetEfStatus();

            // 1. Check dotnet --version
            try
            {
                var dotnetResult = await _cliRunner.ExecuteDotnetAsync("--version", null, cancellationToken).ConfigureAwait(false);
                if (dotnetResult.Success)
                {
                    status.IsDotnetInstalled = true;
                    status.DotnetVersion = dotnetResult.Output.Trim();
                }
                else
                {
                    status.IsDotnetInstalled = false;
                    status.ErrorMessage = "The 'dotnet' CLI could not be found on PATH.";
                    return status;
                }
            }
            catch (Exception ex)
            {
                status.IsDotnetInstalled = false;
                status.ErrorMessage = $"Error checking dotnet CLI: {ex.Message}";
                return status;
            }

            // 2. Check dotnet ef --version
            try
            {
                var efResult = await _cliRunner.ExecuteDotnetAsync("ef --version", null, cancellationToken).ConfigureAwait(false);
                if (efResult.Success)
                {
                    status.IsDotnetEfInstalled = true;
                    var match = VersionRegex.Match(efResult.Output);
                    status.DotnetEfVersion = match.Success ? match.Groups[1].Value : efResult.Output.Trim();
                }
                else
                {
                    status.IsDotnetEfInstalled = false;
                    status.ErrorMessage = "The 'dotnet-ef' tool is not installed or not available on PATH.";
                }
            }
            catch (Exception ex)
            {
                status.IsDotnetEfInstalled = false;
                status.ErrorMessage = $"Error checking dotnet-ef CLI: {ex.Message}";
            }

            return status;
        }

        public string GetInstallToolCommand() => "dotnet tool install --global dotnet-ef";

        public string GetUpdateToolCommand() => "dotnet tool update --global dotnet-ef";
    }
}
