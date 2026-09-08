using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using EFCoreHelper.Core.Services;
using EFCoreHelper.Vsix.Services;
using EFCoreHelper.Vsix.ToolWindows;
using Microsoft.VisualStudio.Shell;

namespace EFCoreHelper.Vsix.Commands
{
    public sealed class SolutionExplorerCommands
    {
        private readonly AsyncPackage _package;
        private readonly IVsOutputWindowService _outputService;
        private readonly IVsSolutionService _solutionService;

        private SolutionExplorerCommands(AsyncPackage package, OleMenuCommandService commandService, IVsOutputWindowService outputService, IVsSolutionService solutionService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            _outputService = outputService ?? throw new ArgumentNullException(nameof(outputService));
            _solutionService = solutionService ?? throw new ArgumentNullException(nameof(solutionService));

            RegisterCommand(commandService, PackageIds.cmdidAddMigration, OnAddMigration);
            RegisterCommand(commandService, PackageIds.cmdidProjectAddMigration, OnAddMigration);
            RegisterCommand(commandService, PackageIds.cmdidUpdateDatabase, OnUpdateDatabase);
            RegisterCommand(commandService, PackageIds.cmdidProjectUpdateDatabase, OnUpdateDatabase);
            RegisterCommand(commandService, PackageIds.cmdidRemoveMigration, OnRemoveMigration);
            RegisterCommand(commandService, PackageIds.cmdidScriptMigration, OnScriptMigration);
            RegisterCommand(commandService, PackageIds.cmdidBundleMigrations, OnBundleMigrations);
            RegisterCommand(commandService, PackageIds.cmdidScaffoldDbContext, OnScaffoldDbContext);
            RegisterCommand(commandService, PackageIds.cmdidProjectScaffold, OnScaffoldDbContext);
            RegisterCommand(commandService, PackageIds.cmdidDropDatabase, OnDropDatabase);
            RegisterCommand(commandService, PackageIds.cmdidOptimizeDbContext, OnOptimizeDbContext);
        }

        public static async Task InitializeAsync(AsyncPackage package, IVsOutputWindowService outputService, IVsSolutionService solutionService)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                _ = new SolutionExplorerCommands(package, commandService, outputService, solutionService);
            }
        }

        private void RegisterCommand(OleMenuCommandService commandService, int commandId, EventHandler handler)
        {
            var menuCommandId = new CommandID(PackageGuids.CommandSetGuid, commandId);
            var menuItem = new MenuCommand(handler, menuCommandId);
            commandService.AddCommand(menuItem);
        }

        private async Task ExecuteActionAsync(Action<EfCoreToolWindowControl> action)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                var window = _package.FindToolWindow(typeof(EfCoreToolWindow), 0, true);
                if (window?.Frame == null)
                {
                    throw new NotSupportedException("Cannot create Entity Framework Core tool window.");
                }

                var windowFrame = (Microsoft.VisualStudio.Shell.Interop.IVsWindowFrame)window.Frame;
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());

                if (window.Content is EfCoreToolWindowControl control)
                {
                    control.SetServices(_outputService, _solutionService);

                    // Ensure solution projects are discovered
                    await control.EnsureInitializedAsync();

                    // Pre-select the project selected in Solution Explorer (if any)
                    var selectedProj = await _solutionService.GetSelectedProjectFilePathAsync();
                    if (!string.IsNullOrEmpty(selectedProj))
                    {
                        control.SelectProjectByPath(selectedProj);
                    }

                    action(control);
                }
            }
            catch (Exception ex)
            {
                await _outputService.WriteLineAsync($"[EF Core Helper Error] {ex}");
                VsShellUtilities.ShowMessageBox(
                    _package,
                    $"EF Core Helper encountered an error:\n\n{ex.Message}\n\nSee the Output window ('Entity Framework Core' pane) for details.",
                    "EF Core Helper",
                    Microsoft.VisualStudio.Shell.Interop.OLEMSGICON.OLEMSGICON_CRITICAL,
                    Microsoft.VisualStudio.Shell.Interop.OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    Microsoft.VisualStudio.Shell.Interop.OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        private void OnAddMigration(object sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ExecuteActionAsync(c => c.OpenAddMigrationDialog());
            });
        }

        private void OnUpdateDatabase(object sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ExecuteActionAsync(c => c.OpenUpdateDatabaseDialog());
            });
        }

        private void OnRemoveMigration(object sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ExecuteActionAsync(c => c.OpenRemoveMigrationDialog());
            });
        }

        private void OnScriptMigration(object sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ExecuteActionAsync(c => c.OpenScriptMigrationDialog());
            });
        }

        private void OnBundleMigrations(object sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ExecuteActionAsync(c => c.OpenBundleMigrationsDialog());
            });
        }

        private void OnScaffoldDbContext(object sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ExecuteActionAsync(c => c.OpenScaffoldDbContextDialog());
            });
        }

        private void OnDropDatabase(object sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ExecuteActionAsync(c => c.OpenDropDatabaseDialog());
            });
        }

        private void OnOptimizeDbContext(object sender, EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ExecuteActionAsync(c => c.OpenOptimizeDbContextDialog());
            });
        }
    }
}
