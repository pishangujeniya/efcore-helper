using System;
using System.IO;
using EFCoreHelper.Core.Services;
using Xunit;

namespace EFCoreHelper.Core.Tests
{
    public class ConnectionStringsProviderTests : IDisposable
    {
        private readonly string _tempDir;

        public ConnectionStringsProviderTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "EFCoreHelper_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [Fact]
        public void GetConnectionStrings_FindsAndParsesAppsettings()
        {
            var json = @"{
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Information""
    }
  },
  ""ConnectionStrings"": {
    ""DefaultConnection"": ""Server=localhost;Database=DevDb;Trusted_Connection=True;"",
    ""SecondaryConnection"": ""Server=backup;Database=TestDb;""
  }
}";
            File.WriteAllText(Path.Combine(_tempDir, "appsettings.json"), json);

            var provider = new ConnectionStringsProvider();
            var connections = provider.GetConnectionStrings(_tempDir);

            Assert.Equal(2, connections.Count);
            Assert.Equal("DefaultConnection", connections[0].Key);
            Assert.Contains("DevDb", connections[0].Value);
            Assert.Equal("SecondaryConnection", connections[1].Key);
            Assert.Contains("backup", connections[1].Value);
        }

        [Fact]
        public void GetConnectionStrings_MalformedJson_DoesNotThrow()
        {
            File.WriteAllText(Path.Combine(_tempDir, "appsettings.json"), "{ invalid json syntax ");

            var provider = new ConnectionStringsProvider();
            var connections = provider.GetConnectionStrings(_tempDir);

            Assert.Empty(connections);
        }
    }
}
