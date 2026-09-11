using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using EFCoreHelper.Vsix.Services;
using EFCoreHelper.Vsix.ToolWindows;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace EFCoreHelper.Vsix.Commands
{
    public sealed class OpenToolWindowCommand
    {
        private readonly AsyncPackage _package;
        private readonly IVsOutputWindowService _outputService;
        private readonly IVsSolutionService _solutionService;

        public static OpenToolWindowCommand? Instance { get; private set; }

        private OpenToolWindowCommand(AsyncPackage package, OleMenuCommandService commandService, IVsOutputWindowService outputService, IVsSolutionService solutionService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            _outputService = outputService ?? throw new ArgumentNullException(nameof(outputService));
            _solutionService = solutionService ?? throw new ArgumentNullException(nameof(solutionService));

            var menuCommandId = new CommandID(PackageGuids.CommandSetGuid, PackageIds.cmdidOpenToolWindow);
            var menuItem = new MenuCommand(Execute, menuCommandId);
            commandService.AddCommand(menuItem);

            var projMenuCommandId = new CommandID(PackageGuids.CommandSetGuid, PackageIds.cmdidProjectOpenToolWindow);
            var projMenuItem = new MenuCommand(Execute, projMenuCommandId);
            commandService.AddCommand(projMenuItem);
        }

        public static async Task InitializeAsync(AsyncPackage package, IVsOutputWindowService outputService, IVsSolutionService solutionService)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                Instance = new OpenToolWindowCommand(package, commandService, outputService, solutionService);
            }
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var window = _package.FindToolWindow(typeof(EfCoreToolWindow), 0, true);
                if (window?.Frame == null)
                {
                    throw new NotSupportedException("Cannot create EF Core Helper tool window.");
                }

                if (window.Content is EfCoreToolWindowControl control)
                {
                    control.SetServices(_outputService, _solutionService);
                }

                var windowFrame = (IVsWindowFrame)window.Frame;
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
            }
            catch (Exception ex)
            {
                _outputService.WriteLineAsync($"[EF Core Helper Error] Failed to open tool window: {ex}");
                VsShellUtilities.ShowMessageBox(
                    _package,
                    $"Failed to open EF Core Helper tool window:\n\n{ex.Message}\n\nSee Output window for details.",
                    "EF Core Helper",
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }
    }
}
