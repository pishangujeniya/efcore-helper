using System.Collections.Generic;
using EFCoreHelper.Core.Models;

namespace EFCoreHelper.Core.Services
{
    public interface IEfCliCommandBuilder
    {
        string BuildAddMigrationCommand(AddMigrationOptions options);
        string BuildRemoveMigrationCommand(RemoveMigrationOptions options);
        string BuildUpdateDatabaseCommand(UpdateDatabaseOptions options);
        string BuildScriptMigrationCommand(ScriptMigrationOptions options);
        string BuildBundleMigrationCommand(BundleMigrationOptions options);
        string BuildDropDatabaseCommand(DropDatabaseOptions options);
        string BuildScaffoldDbContextCommand(ScaffoldDbContextOptions options);
        string BuildOptimizeDbContextCommand(OptimizeDbContextOptions options);
        string BuildListMigrationsCommand(BaseCommandOptions options);
        string BuildListDbContextsCommand(BaseCommandOptions options);
        string BuildHasPendingModelChangesCommand(HasPendingModelChangesOptions options);

        List<string> BuildArguments(string commandLineWithoutDotnetEf);
    }
}
