using System;
using EFCoreHelper.Core.Models;
using EFCoreHelper.Core.Services;
using Xunit;

namespace EFCoreHelper.Core.Tests
{
    public class SolutionScannerTests
    {
        [Theory]
        [InlineData("net8.0", true)]
        [InlineData("net10.0", true)]
        [InlineData("net6.0", true)]
        [InlineData("net7.0", true)]
        [InlineData("netcoreapp3.1", true)]
        [InlineData("netstandard2.0", true)]
        [InlineData("net8.0-windows", true)]
        [InlineData("net48", false)]
        [InlineData("net472", false)]
        [InlineData("net461", false)]
        [InlineData("v4.8", false)]
        public void IsModernDotNetProject_CorrectlyIdentifiesModernDotNet(string tfm, bool expected)
        {
            var result = SolutionScanner.IsModernDotNetProject(tfm);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void MigrationInfo_FromFileName_ParsesEfCoreTimestampsCorrectly()
        {
            var fileName = "20260906153000_AddInitialSchema.cs";
            var migration = MigrationInfo.FromFileName(fileName, "/path/" + fileName, "/path/project.csproj");

            Assert.Equal("20260906153000_AddInitialSchema", migration.MigrationId);
            Assert.Equal("AddInitialSchema", migration.Name);
            Assert.True(migration.CreatedDate.HasValue);
            Assert.Equal(2026, migration.CreatedDate!.Value.Year);
            Assert.Equal(9, migration.CreatedDate!.Value.Month);
            Assert.Equal(6, migration.CreatedDate!.Value.Day);
            Assert.Equal(15, migration.CreatedDate!.Value.Hour);
            Assert.Equal(30, migration.CreatedDate!.Value.Minute);
        }

        [Fact]
        public void MigrationInfo_FromFileName_HandlesDesignerFile()
        {
            var fileName = "20260906153000_AddInitialSchema.Designer.cs";
            var migration = MigrationInfo.FromFileName(fileName, "/path/" + fileName, "/path/project.csproj");

            Assert.Equal("20260906153000_AddInitialSchema", migration.MigrationId);
            Assert.Equal("AddInitialSchema", migration.Name);
        }

        [Fact]
        public async System.Threading.Tasks.Task ScanProjectAsync_ContosoUniversity_DiscoversAllMetadata()
        {
            var baseDir = AppContext.BaseDirectory;
            // Navigate up to solution root
            var currentDir = new System.IO.DirectoryInfo(baseDir);
            while (currentDir != null && !System.IO.File.Exists(System.IO.Path.Combine(currentDir.FullName, "samples", "ContosoUniversity", "ContosoUniversity.csproj")))
            {
                currentDir = currentDir.Parent;
            }

            Assert.NotNull(currentDir);
            var projectPath = System.IO.Path.Combine(currentDir!.FullName, "samples", "ContosoUniversity", "ContosoUniversity.csproj");

            var scanner = new SolutionScanner();
            var projectInfo = await scanner.ScanProjectAsync(projectPath);

            Assert.NotNull(projectInfo);
            Assert.Equal("ContosoUniversity", projectInfo!.Name);
            Assert.True(projectInfo.IsStartupProject);
            Assert.True(projectInfo.HasEfCoreReference);
            Assert.True(projectInfo.HasEfDesignReference);

            // DbContext detection
            Assert.Single(projectInfo.DbContexts);
            Assert.Equal("SchoolContext", projectInfo.DbContexts[0].Name);

            // Migrations detection
            Assert.True(projectInfo.Migrations.Count >= 1);
            Assert.Contains(projectInfo.Migrations, m => m.Name == "InitialCreate");

            // Connection string detection
            Assert.Contains(projectInfo.ConnectionStrings, c => c.Key == "DefaultConnection");
        }

        [Fact]
        public async System.Threading.Tasks.Task ScanSolutionAsync_WithSlnxFile_DiscoversProjects()
        {
            var baseDir = AppContext.BaseDirectory;
            var currentDir = new System.IO.DirectoryInfo(baseDir);
            while (currentDir != null && !System.IO.File.Exists(System.IO.Path.Combine(currentDir.FullName, "EFCoreHelper.slnx")))
            {
                currentDir = currentDir.Parent;
            }

            Assert.NotNull(currentDir);
            var slnxPath = System.IO.Path.Combine(currentDir!.FullName, "EFCoreHelper.slnx");

            var scanner = new SolutionScanner();
            var projects = await scanner.ScanSolutionAsync(slnxPath);

            Assert.NotEmpty(projects);
            Assert.Contains(projects, p => p.Name == "ContosoUniversity");
        }

        [Fact]
        public async System.Threading.Tasks.Task ScanProjectAsync_SortsMigrationsDescending_LatestFirst()
        {
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "EFCoreHelper_Test_" + Guid.NewGuid().ToString("N"));
            var migrationsDir = System.IO.Path.Combine(tempDir, "Migrations");
            System.IO.Directory.CreateDirectory(migrationsDir);

            try
            {
                var csprojContent = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup></Project>";
                var csprojPath = System.IO.Path.Combine(tempDir, "TestApp.csproj");
                System.IO.File.WriteAllText(csprojPath, csprojContent);

                // Create 3 migrations with different timestamps
                System.IO.File.WriteAllText(System.IO.Path.Combine(migrationsDir, "20240101100000_OldMigration.cs"), "// migration 1");
                System.IO.File.WriteAllText(System.IO.Path.Combine(migrationsDir, "20250601120000_MiddleMigration.cs"), "// migration 2");
                System.IO.File.WriteAllText(System.IO.Path.Combine(migrationsDir, "20260911150000_LatestMigration.cs"), "// migration 3");

                var scanner = new SolutionScanner();
                var projectInfo = await scanner.ScanProjectAsync(csprojPath);

                Assert.NotNull(projectInfo);
                Assert.Equal(3, projectInfo!.Migrations.Count);
                Assert.Equal("LatestMigration", projectInfo.Migrations[0].Name);
                Assert.Equal("MiddleMigration", projectInfo.Migrations[1].Name);
                Assert.Equal("OldMigration", projectInfo.Migrations[2].Name);
            }
            finally
            {
                if (System.IO.Directory.Exists(tempDir))
                {
                    System.IO.Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
