using System;
using System.Threading;
using System.Threading.Tasks;
using EFCoreHelper.Core.Models;
using EFCoreHelper.Core.Services;
using Xunit;

namespace EFCoreHelper.Core.Tests
{
    public class DotnetEfDetectorTests
    {
        private class FakeCliRunner : IEfCliRunner
        {
#pragma warning disable CS0067
            public event Action<string>? OutputLineReceived;
            public event Action<string>? ErrorLineReceived;
#pragma warning restore CS0067

            public Func<string, Task<ExecutionResult>>? Handler { get; set; }

            public Task<ExecutionResult> ExecuteDotnetAsync(string dotnetArgs, string? workingDirectory = null, CancellationToken cancellationToken = default)
            {
                if (Handler != null)
                    return Handler(dotnetArgs);

                return Task.FromResult(new ExecutionResult
                {
                    ExitCode = 0,
                    Output = "10.0.12",
                    Success = true
                });
            }

            public Task<ExecutionResult> ExecuteAsync(string fullCommandLine, string? workingDirectory = null, CancellationToken cancellationToken = default)
            {
                return ExecuteDotnetAsync(fullCommandLine, workingDirectory, cancellationToken);
            }
        }

        [Theory]
        [InlineData("10.0.8", "10.0.12", true)]
        [InlineData("9.0.0", "10.0.0", true)]
        [InlineData("8.0.1", "8.0.11", true)]
        [InlineData("10.0.12-preview.1", "10.0.12", true)]
        [InlineData("10.0.12", "10.0.8", false)]
        [InlineData("10.0.12", "10.0.12", false)]
        [InlineData("10.0.12", "9.0.0", false)]
        [InlineData("", "10.0.12", false)]
        [InlineData(null, "10.0.12", false)]
        [InlineData("10.0.8", null, false)]
        public void IsNewerVersion_ComparesCorrectly(string? current, string? latest, bool expected)
        {
            var result = DotnetEfDetector.IsNewerVersion(current, latest);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Commands_ReturnExpectedStrings()
        {
            var runner = new FakeCliRunner();
            var detector = new DotnetEfDetector(runner);

            Assert.Equal("dotnet tool install --global dotnet-ef", detector.GetInstallToolCommand());
            Assert.Equal("dotnet tool update --global dotnet-ef", detector.GetUpdateToolCommand());
        }

        [Fact]
        public async Task CheckStatusAsync_WhenUpdateAvailable_FlagsUpdate()
        {
            var runner = new FakeCliRunner
            {
                Handler = args =>
                {
                    if (args == "--version")
                        return Task.FromResult(new ExecutionResult { Success = true, Output = "10.0.400" });
                    if (args == "ef --version")
                        return Task.FromResult(new ExecutionResult { Success = true, Output = "10.0.8" });
                    if (args.Contains("search"))
                    {
                        var table = "Package ID  Latest Version\n--------------------------\ndotnet-ef   10.0.12\n";
                        return Task.FromResult(new ExecutionResult { Success = true, Output = table });
                    }
                    return Task.FromResult(new ExecutionResult { Success = false });
                }
            };

            var detector = new DotnetEfDetector(runner);
            var status = await detector.CheckStatusAsync();

            Assert.True(status.IsDotnetInstalled);
            Assert.True(status.IsDotnetEfInstalled);
            Assert.Equal("10.0.8", status.DotnetEfVersion);
            Assert.True(status.IsUpdateAvailable);
            Assert.Equal("10.0.12", status.LatestDotnetEfVersion);
        }

        [Fact]
        public async Task CheckStatusAsync_WhenUpToDate_DoesNotFlagUpdate()
        {
            var runner = new FakeCliRunner
            {
                Handler = args =>
                {
                    if (args == "--version")
                        return Task.FromResult(new ExecutionResult { Success = true, Output = "10.0.400" });
                    if (args == "ef --version")
                        return Task.FromResult(new ExecutionResult { Success = true, Output = "10.0.12" });
                    if (args.Contains("search"))
                    {
                        var table = "Package ID  Latest Version\n--------------------------\ndotnet-ef   10.0.12\n";
                        return Task.FromResult(new ExecutionResult { Success = true, Output = table });
                    }
                    return Task.FromResult(new ExecutionResult { Success = false });
                }
            };

            var detector = new DotnetEfDetector(runner);
            var status = await detector.CheckStatusAsync();

            Assert.True(status.IsDotnetInstalled);
            Assert.True(status.IsDotnetEfInstalled);
            Assert.Equal("10.0.12", status.DotnetEfVersion);
            Assert.False(status.IsUpdateAvailable);
        }

        [Fact]
        public async Task CheckStatusAsync_WhenNotInstalled_ReturnsFalse()
        {
            var runner = new FakeCliRunner
            {
                Handler = args =>
                {
                    if (args == "--version")
                        return Task.FromResult(new ExecutionResult { Success = true, Output = "10.0.400" });
                    if (args == "ef --version")
                        return Task.FromResult(new ExecutionResult { Success = false, Output = "Command not found" });
                    return Task.FromResult(new ExecutionResult { Success = false });
                }
            };

            var detector = new DotnetEfDetector(runner);
            var status = await detector.CheckStatusAsync();

            Assert.True(status.IsDotnetInstalled);
            Assert.False(status.IsDotnetEfInstalled);
            Assert.False(status.IsUpdateAvailable);
        }
    }
}
