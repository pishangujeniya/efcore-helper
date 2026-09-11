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
        private static readonly Regex SearchRegex = new Regex(@"^\s*dotnet-ef\s+(\S+)", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

            // 3. Check for newer dotnet-ef version available globally
            if (status.IsDotnetEfInstalled)
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(5));
                    var searchResult = await _cliRunner.ExecuteDotnetAsync("tool search dotnet-ef", null, cts.Token).ConfigureAwait(false);
                    if (searchResult.Success && !string.IsNullOrWhiteSpace(searchResult.Output))
                    {
                        var match = SearchRegex.Match(searchResult.Output);
                        if (match.Success)
                        {
                            var latestVersion = match.Groups[1].Value.Trim();
                            if (!string.IsNullOrEmpty(latestVersion))
                            {
                                status.LatestDotnetEfVersion = latestVersion;
                                if (IsNewerVersion(status.DotnetEfVersion, latestVersion))
                                {
                                    status.IsUpdateAvailable = true;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Non-blocking: continue with installed version if offline or check times out
                }
            }

            return status;
        }

        public static bool IsNewerVersion(string? current, string? latest)
        {
            if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(latest))
                return false;

            var cleanCurrent = current!.Split('-')[0].Trim();
            var cleanLatest = latest!.Split('-')[0].Trim();

            if (Version.TryParse(cleanCurrent, out var vCurrent) && Version.TryParse(cleanLatest, out var vLatest))
            {
                if (vLatest > vCurrent)
                    return true;
                if (vLatest < vCurrent)
                    return false;

                // Same numeric version: if current has prerelease and latest does not, latest is newer
                return current.Contains("-") && !latest.Contains("-");
            }

            return false;
        }

        public string GetInstallToolCommand() => "dotnet tool install --global dotnet-ef";

        public string GetUpdateToolCommand() => "dotnet tool update --global dotnet-ef";
    }
}
