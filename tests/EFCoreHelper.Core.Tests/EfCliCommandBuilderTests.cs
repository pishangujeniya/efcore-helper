using System;
using System.Collections.Generic;
using EFCoreHelper.Core.Models;
using EFCoreHelper.Core.Services;
using Xunit;

namespace EFCoreHelper.Core.Tests
{
    public class EfCliCommandBuilderTests
    {
        private readonly EfCliCommandBuilder _builder = new EfCliCommandBuilder();

        [Fact]
        public void BuildAddMigrationCommand_Basic_GeneratesCorrectCommand()
        {
            var options = new AddMigrationOptions
            {
                MigrationName = "InitialCreate",
                Project = "MyApp.Data",
                StartupProject = "MyApp.Web",
                Context = "AppDbContext"
            };

            var cmd = _builder.BuildAddMigrationCommand(options);

            Assert.Equal("dotnet ef migrations add InitialCreate --project MyApp.Data --startup-project MyApp.Web --context AppDbContext", cmd);
        }

        [Fact]
        public void BuildAddMigrationCommand_WithSpacesAndFlags_ProperlyEscaped()
        {
            var options = new AddMigrationOptions
            {
                MigrationName = "Add User Table",
                Project = "C:\\My Projects\\Data",
                StartupProject = "C:\\My Projects\\Web",
                Context = "AppDbContext",
                OutputDir = "Data/Migrations",
                Namespace = "MyCompany.Data.Migrations",
                Verbose = true,
                NoBuild = true
            };

            var cmd = _builder.BuildAddMigrationCommand(options);

            Assert.Contains("\"Add User Table\"", cmd);
            Assert.Contains("--project \"C:\\My Projects\\Data\"", cmd);
            Assert.Contains("--startup-project \"C:\\My Projects\\Web\"", cmd);
            Assert.Contains("--output-dir Data/Migrations", cmd);
            Assert.Contains("--namespace MyCompany.Data.Migrations", cmd);
            Assert.Contains("--verbose", cmd);
            Assert.Contains("--no-build", cmd);
        }

        [Fact]
        public void BuildRemoveMigrationCommand_WithForce_GeneratesCorrectCommand()
        {
            var options = new RemoveMigrationOptions
            {
                Project = "MyApp.Data",
                StartupProject = "MyApp.Web",
                Context = "AppDbContext",
                Force = true
            };

            var cmd = _builder.BuildRemoveMigrationCommand(options);

            Assert.Equal("dotnet ef migrations remove --project MyApp.Data --startup-project MyApp.Web --context AppDbContext --force", cmd);
        }

        [Fact]
        public void BuildUpdateDatabaseCommand_WithTargetMigrationAndConnection_GeneratesCorrectCommand()
        {
            var options = new UpdateDatabaseOptions
            {
                TargetMigration = "20260901120000_AddUsers",
                Project = "MyApp.Data",
                StartupProject = "MyApp.Web",
                ConnectionString = "Server=localhost;Database=TestDb;"
            };

            var cmd = _builder.BuildUpdateDatabaseCommand(options);

            Assert.Equal("dotnet ef database update 20260901120000_AddUsers --project MyApp.Data --startup-project MyApp.Web --connection \"Server=localhost;Database=TestDb;\"", cmd);
        }

        [Fact]
        public void BuildScriptMigrationCommand_Idempotent_GeneratesCorrectCommand()
        {
            var options = new ScriptMigrationOptions
            {
                FromMigration = "InitialCreate",
                ToMigration = "AddUsers",
                OutputFilePath = "C:\\My Scripts\\update.sql",
                Idempotent = true,
                NoTransactions = true,
                Project = "MyApp.Data"
            };

            var cmd = _builder.BuildScriptMigrationCommand(options);

            Assert.Equal("dotnet ef migrations script InitialCreate AddUsers --project MyApp.Data --output \"C:\\My Scripts\\update.sql\" --idempotent --no-transactions", cmd);
        }

        [Fact]
        public void BuildBundleMigrationCommand_SelfContained_GeneratesCorrectCommand()
        {
            var options = new BundleMigrationOptions
            {
                OutputFilePath = "bundle.exe",
                TargetRuntime = "win-x64",
                SelfContained = true,
                Force = true,
                Project = "MyApp.Data"
            };

            var cmd = _builder.BuildBundleMigrationCommand(options);

            Assert.Equal("dotnet ef migrations bundle --project MyApp.Data --output bundle.exe --target-runtime win-x64 --self-contained --force", cmd);
        }

        [Fact]
        public void BuildDropDatabaseCommand_WithForce_GeneratesCorrectCommand()
        {
            var options = new DropDatabaseOptions
            {
                Project = "MyApp.Data",
                StartupProject = "MyApp.Web",
                Force = true
            };

            var cmd = _builder.BuildDropDatabaseCommand(options);

            Assert.Equal("dotnet ef database drop --project MyApp.Data --startup-project MyApp.Web --force", cmd);
        }

        [Fact]
        public void BuildScaffoldDbContextCommand_AllOptions_GeneratesCorrectCommand()
        {
            var options = new ScaffoldDbContextOptions
            {
                ConnectionString = "Data Source=test.db",
                Provider = "Microsoft.EntityFrameworkCore.Sqlite",
                OutputDir = "Models",
                ContextDir = "Data",
                ContextName = "SqliteDbContext",
                Tables = new List<string> { "Users", "Orders" },
                Schemas = new List<string> { "dbo" },
                UseDatabaseNames = true,
                DataAnnotations = true,
                Force = true,
                NoPluralize = true,
                Project = "MyApp.Data"
            };

            var cmd = _builder.BuildScaffoldDbContextCommand(options);

            Assert.Contains("dotnet ef dbcontext scaffold \"Data Source=test.db\" Microsoft.EntityFrameworkCore.Sqlite", cmd);
            Assert.Contains("--project MyApp.Data", cmd);
            Assert.Contains("--output-dir Models", cmd);
            Assert.Contains("--context-dir Data", cmd);
            Assert.Contains("--context SqliteDbContext", cmd);
            Assert.Contains("--table Users", cmd);
            Assert.Contains("--table Orders", cmd);
            Assert.Contains("--schema dbo", cmd);
            Assert.Contains("--use-database-names", cmd);
            Assert.Contains("--data-annotations", cmd);
            Assert.Contains("--no-pluralize", cmd);
            Assert.Contains("--force", cmd);
        }

        [Fact]
        public void BuildOptimizeDbContextCommand_GeneratesCorrectCommand()
        {
            var options = new OptimizeDbContextOptions
            {
                Project = "MyApp.Data",
                StartupProject = "MyApp.Web",
                OutputDir = "CompiledModels",
                Namespace = "MyApp.Data.Compiled",
                ScaffoldModel = true
            };

            var cmd = _builder.BuildOptimizeDbContextCommand(options);

            Assert.Equal("dotnet ef dbcontext optimize --project MyApp.Data --startup-project MyApp.Web --output-dir CompiledModels --namespace MyApp.Data.Compiled --scaffold-model", cmd);
        }

        [Fact]
        public void BuildArguments_SplitsQuotedCommandLineProperly()
        {
            var line = "migrations add \"Add User Table\" --project \"C:\\My Dir\\Project.csproj\" --verbose";
            var args = _builder.BuildArguments(line);

            Assert.Equal(6, args.Count);
            Assert.Equal("migrations", args[0]);
            Assert.Equal("add", args[1]);
            Assert.Equal("Add User Table", args[2]);
            Assert.Equal("--project", args[3]);
            Assert.Equal("C:\\My Dir\\Project.csproj", args[4]);
            Assert.Equal("--verbose", args[5]);
        }

        [Fact]
        public void BuildCommands_WithStartupProject_AlwaysIncludesStartupProjectArgument()
        {
            var startup = "C:\\Apps\\MyWeb.csproj";
            var proj = "C:\\Apps\\MyData.csproj";

            var addCmd = _builder.BuildAddMigrationCommand(new AddMigrationOptions { MigrationName = "M1", Project = proj, StartupProject = startup });
            Assert.Contains($"--startup-project {startup}", addCmd);

            var removeCmd = _builder.BuildRemoveMigrationCommand(new RemoveMigrationOptions { Project = proj, StartupProject = startup });
            Assert.Contains($"--startup-project {startup}", removeCmd);

            var updateCmd = _builder.BuildUpdateDatabaseCommand(new UpdateDatabaseOptions { Project = proj, StartupProject = startup });
            Assert.Contains($"--startup-project {startup}", updateCmd);

            var scriptCmd = _builder.BuildScriptMigrationCommand(new ScriptMigrationOptions { Project = proj, StartupProject = startup });
            Assert.Contains($"--startup-project {startup}", scriptCmd);

            var bundleCmd = _builder.BuildBundleMigrationCommand(new BundleMigrationOptions { Project = proj, StartupProject = startup });
            Assert.Contains($"--startup-project {startup}", bundleCmd);

            var scaffoldCmd = _builder.BuildScaffoldDbContextCommand(new ScaffoldDbContextOptions { ConnectionString = "conn", Provider = "provider", Project = proj, StartupProject = startup });
            Assert.Contains($"--startup-project {startup}", scaffoldCmd);

            var dropCmd = _builder.BuildDropDatabaseCommand(new DropDatabaseOptions { Project = proj, StartupProject = startup });
            Assert.Contains($"--startup-project {startup}", dropCmd);

            var optimizeCmd = _builder.BuildOptimizeDbContextCommand(new OptimizeDbContextOptions { Project = proj, StartupProject = startup });
            Assert.Contains($"--startup-project {startup}", optimizeCmd);
        }
    }
}
