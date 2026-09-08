using System;
using System.Collections.Generic;
using System.Text;
using EFCoreHelper.Core.Models;

namespace EFCoreHelper.Core.Services
{
    public class EfCliCommandBuilder : IEfCliCommandBuilder
    {
        public string BuildAddMigrationCommand(AddMigrationOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.MigrationName))
                throw new ArgumentException("Migration name cannot be empty.", nameof(options.MigrationName));

            var sb = new StringBuilder();
            sb.Append("dotnet ef migrations add ");
            sb.Append(EscapeArgument(options.MigrationName));

            AppendBaseOptions(sb, options);

            if (!string.IsNullOrWhiteSpace(options.OutputDir))
            {
                sb.Append(" --output-dir ").Append(EscapeArgument(options.OutputDir!));
            }

            if (!string.IsNullOrWhiteSpace(options.Namespace))
            {
                sb.Append(" --namespace ").Append(EscapeArgument(options.Namespace!));
            }

            AppendAdditionalArgs(sb, options.AdditionalArgs);
            return sb.ToString().Trim();
        }

        public string BuildRemoveMigrationCommand(RemoveMigrationOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var sb = new StringBuilder();
            sb.Append("dotnet ef migrations remove");

            AppendBaseOptions(sb, options);

            if (options.Force)
            {
                sb.Append(" --force");
            }

            AppendAdditionalArgs(sb, options.AdditionalArgs);
            return sb.ToString().Trim();
        }

        public string BuildUpdateDatabaseCommand(UpdateDatabaseOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var sb = new StringBuilder();
            sb.Append("dotnet ef database update");

            if (!string.IsNullOrWhiteSpace(options.TargetMigration))
            {
                sb.Append(' ').Append(EscapeArgument(options.TargetMigration!));
            }

            AppendBaseOptions(sb, options);

            if (!string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                sb.Append(" --connection ").Append(EscapeArgument(options.ConnectionString!));
            }

            AppendAdditionalArgs(sb, options.AdditionalArgs);
            return sb.ToString().Trim();
        }

        public string BuildScriptMigrationCommand(ScriptMigrationOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var sb = new StringBuilder();
            sb.Append("dotnet ef migrations script");

            if (!string.IsNullOrWhiteSpace(options.FromMigration))
            {
                sb.Append(' ').Append(EscapeArgument(options.FromMigration!));

                if (!string.IsNullOrWhiteSpace(options.ToMigration))
                {
                    sb.Append(' ').Append(EscapeArgument(options.ToMigration!));
                }
            }
            else if (!string.IsNullOrWhiteSpace(options.ToMigration))
            {
                // If From is not specified but To is, From defaults to 0
                sb.Append(" 0 ").Append(EscapeArgument(options.ToMigration!));
            }

            AppendBaseOptions(sb, options);

            if (!string.IsNullOrWhiteSpace(options.OutputFilePath))
            {
                sb.Append(" --output ").Append(EscapeArgument(options.OutputFilePath!));
            }

            if (options.Idempotent)
            {
                sb.Append(" --idempotent");
            }

            if (options.NoTransactions)
            {
                sb.Append(" --no-transactions");
            }

            AppendAdditionalArgs(sb, options.AdditionalArgs);
            return sb.ToString().Trim();
        }

        public string BuildBundleMigrationCommand(BundleMigrationOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var sb = new StringBuilder();
            sb.Append("dotnet ef migrations bundle");

            AppendBaseOptions(sb, options);

            if (!string.IsNullOrWhiteSpace(options.OutputFilePath))
            {
                sb.Append(" --output ").Append(EscapeArgument(options.OutputFilePath!));
            }

            if (!string.IsNullOrWhiteSpace(options.TargetRuntime))
            {
                sb.Append(" --target-runtime ").Append(EscapeArgument(options.TargetRuntime!));
            }

            if (options.SelfContained)
            {
                sb.Append(" --self-contained");
            }

            if (options.Force)
            {
                sb.Append(" --force");
            }

            AppendAdditionalArgs(sb, options.AdditionalArgs);
            return sb.ToString().Trim();
        }

        public string BuildDropDatabaseCommand(DropDatabaseOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var sb = new StringBuilder();
            sb.Append("dotnet ef database drop");

            AppendBaseOptions(sb, options);

            if (options.Force)
            {
                sb.Append(" --force");
            }

            if (options.DryRun)
            {
                sb.Append(" --dry-run");
            }

            AppendAdditionalArgs(sb, options.AdditionalArgs);
            return sb.ToString().Trim();
        }

        public string BuildScaffoldDbContextCommand(ScaffoldDbContextOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
                throw new ArgumentException("Connection string cannot be empty.", nameof(options.ConnectionString));
            if (string.IsNullOrWhiteSpace(options.Provider))
                throw new ArgumentException("Database provider cannot be empty.", nameof(options.Provider));

            var sb = new StringBuilder();
            sb.Append("dotnet ef dbcontext scaffold ");
            sb.Append(EscapeArgument(options.ConnectionString));
            sb.Append(' ');
            sb.Append(EscapeArgument(options.Provider));

            AppendBaseOptions(sb, options);

            if (!string.IsNullOrWhiteSpace(options.OutputDir))
            {
                sb.Append(" --output-dir ").Append(EscapeArgument(options.OutputDir!));
            }

            if (!string.IsNullOrWhiteSpace(options.ContextDir))
            {
                sb.Append(" --context-dir ").Append(EscapeArgument(options.ContextDir!));
            }

            if (!string.IsNullOrWhiteSpace(options.ContextName))
            {
                sb.Append(" --context ").Append(EscapeArgument(options.ContextName!));
            }

            if (options.Tables != null)
            {
                foreach (var table in options.Tables)
                {
                    if (!string.IsNullOrWhiteSpace(table))
                    {
                        sb.Append(" --table ").Append(EscapeArgument(table));
                    }
                }
            }

            if (options.Schemas != null)
            {
                foreach (var schema in options.Schemas)
                {
                    if (!string.IsNullOrWhiteSpace(schema))
                    {
                        sb.Append(" --schema ").Append(EscapeArgument(schema));
                    }
                }
            }

            if (options.UseDatabaseNames)
            {
                sb.Append(" --use-database-names");
            }

            if (options.DataAnnotations)
            {
                sb.Append(" --data-annotations");
            }

            if (options.NoPluralize)
            {
                sb.Append(" --no-pluralize");
            }

            if (options.Force)
            {
                sb.Append(" --force");
            }

            AppendAdditionalArgs(sb, options.AdditionalArgs);
            return sb.ToString().Trim();
        }

        public string BuildOptimizeDbContextCommand(OptimizeDbContextOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var sb = new StringBuilder();
            sb.Append("dotnet ef dbcontext optimize");

            AppendBaseOptions(sb, options);

            if (!string.IsNullOrWhiteSpace(options.OutputDir))
            {
                sb.Append(" --output-dir ").Append(EscapeArgument(options.OutputDir!));
            }

            if (!string.IsNullOrWhiteSpace(options.Namespace))
            {
                sb.Append(" --namespace ").Append(EscapeArgument(options.Namespace!));
            }

            if (options.ScaffoldModel)
            {
                sb.Append(" --scaffold-model");
            }

            AppendAdditionalArgs(sb, options.AdditionalArgs);
            return sb.ToString().Trim();
        }

        public string BuildListMigrationsCommand(BaseCommandOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var sb = new StringBuilder();
            sb.Append("dotnet ef migrations list");
            AppendBaseOptions(sb, options);
            AppendAdditionalArgs(sb, options.AdditionalArgs);
            return sb.ToString().Trim();
        }

        public string BuildListDbContextsCommand(BaseCommandOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var sb = new StringBuilder();
            sb.Append("dotnet ef dbcontext list");
            AppendBaseOptions(sb, options);
            AppendAdditionalArgs(sb, options.AdditionalArgs);
            return sb.ToString().Trim();
        }

        public string BuildHasPendingModelChangesCommand(HasPendingModelChangesOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var sb = new StringBuilder();
            sb.Append("dotnet ef migrations has-pending-model-changes");
            AppendBaseOptions(sb, options);
            AppendAdditionalArgs(sb, options.AdditionalArgs);
            return sb.ToString().Trim();
        }

        public List<string> BuildArguments(string commandLineWithoutDotnetEf)
        {
            var args = new List<string>();
            if (string.IsNullOrWhiteSpace(commandLineWithoutDotnetEf))
                return args;

            bool inQuotes = false;
            var current = new StringBuilder();

            for (int i = 0; i < commandLineWithoutDotnetEf.Length; i++)
            {
                char c = commandLineWithoutDotnetEf[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        args.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                args.Add(current.ToString());
            }

            return args;
        }

        private void AppendBaseOptions(StringBuilder sb, BaseCommandOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.Project))
            {
                sb.Append(" --project ").Append(EscapeArgument(options.Project!));
            }

            if (!string.IsNullOrWhiteSpace(options.StartupProject))
            {
                sb.Append(" --startup-project ").Append(EscapeArgument(options.StartupProject!));
            }

            if (!string.IsNullOrWhiteSpace(options.Context))
            {
                sb.Append(" --context ").Append(EscapeArgument(options.Context!));
            }

            if (options.Verbose)
            {
                sb.Append(" --verbose");
            }

            if (options.NoBuild)
            {
                sb.Append(" --no-build");
            }
        }

        private void AppendAdditionalArgs(StringBuilder sb, string? additionalArgs)
        {
            if (!string.IsNullOrWhiteSpace(additionalArgs))
            {
                sb.Append(' ').Append(additionalArgs!.Trim());
            }
        }

        private string EscapeArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
                return "\"\"";

            if (argument.IndexOf(' ') >= 0 || argument.IndexOf('\t') >= 0 || argument.IndexOf('"') >= 0 || argument.IndexOf(';') >= 0)
            {
                return "\"" + argument.Replace("\"", "\\\"") + "\"";
            }

            return argument;
        }
    }
}
