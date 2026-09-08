using System.Collections.Generic;

namespace EFCoreHelper.Core.Models
{
    public abstract class BaseCommandOptions
    {
        public string? Project { get; set; }
        public string? StartupProject { get; set; }
        public string? Context { get; set; }
        public bool Verbose { get; set; }
        public bool NoBuild { get; set; }
        public string? AdditionalArgs { get; set; }
    }

    public class AddMigrationOptions : BaseCommandOptions
    {
        public string MigrationName { get; set; } = string.Empty;
        public string? OutputDir { get; set; }
        public string? Namespace { get; set; }
    }

    public class RemoveMigrationOptions : BaseCommandOptions
    {
        public bool Force { get; set; }
    }

    public class UpdateDatabaseOptions : BaseCommandOptions
    {
        public string? TargetMigration { get; set; }
        public string? ConnectionString { get; set; }
    }

    public class ScriptMigrationOptions : BaseCommandOptions
    {
        public string? FromMigration { get; set; }
        public string? ToMigration { get; set; }
        public string? OutputFilePath { get; set; }
        public bool Idempotent { get; set; }
        public bool NoTransactions { get; set; }
    }

    public class BundleMigrationOptions : BaseCommandOptions
    {
        public string? OutputFilePath { get; set; }
        public string? TargetRuntime { get; set; }
        public bool SelfContained { get; set; }
        public bool Force { get; set; }
    }

    public class DropDatabaseOptions : BaseCommandOptions
    {
        public bool Force { get; set; }
        public bool DryRun { get; set; }
    }

    public class ScaffoldDbContextOptions : BaseCommandOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string? OutputDir { get; set; }
        public string? ContextDir { get; set; }
        public string? ContextName { get; set; }
        public List<string> Tables { get; set; } = new List<string>();
        public List<string> Schemas { get; set; } = new List<string>();
        public bool UseDatabaseNames { get; set; }
        public bool DataAnnotations { get; set; }
        public bool Force { get; set; }
        public bool NoPluralize { get; set; }
    }

    public class OptimizeDbContextOptions : BaseCommandOptions
    {
        public string? OutputDir { get; set; }
        public string? Namespace { get; set; }
        public bool ScaffoldModel { get; set; }
    }

    public class HasPendingModelChangesOptions : BaseCommandOptions
    {
    }
}
