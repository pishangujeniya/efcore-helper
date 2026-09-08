using System;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EFCoreHelper.Vsix.Commands;
using EFCoreHelper.Vsix.Services;
using EFCoreHelper.Vsix.ToolWindows;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

[assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.MainAssembly)]
[assembly: ProvideCodeBase(AssemblyName = "EFCoreHelper.Core")]

namespace EFCoreHelper.Vsix
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuids.PackageGuidString)]
    [ProvideBindingPath]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(EfCoreToolWindow), Style = VsDockStyle.Tabbed, Window = EnvDTE.Constants.vsWindowKindSolutionExplorer)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class EFCoreHelperPackage : AsyncPackage
    {
        private IVsOutputWindowService? _outputService;
        private VsSolutionService? _solutionService;

        static EFCoreHelperPackage()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        public static void EnsureAssemblyResolver()
        {
            // Static constructor will execute
        }

        private static Assembly? CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                var requestedName = new AssemblyName(args.Name).Name;
                if (string.IsNullOrEmpty(requestedName)) return null;

                var location = typeof(EFCoreHelperPackage).Assembly.Location;
                if (string.IsNullOrEmpty(location)) return null;

                var extensionDir = Path.GetDirectoryName(location);
                if (string.IsNullOrEmpty(extensionDir)) return null;

                var candidatePath = Path.Combine(extensionDir, requestedName + ".dll");
                if (File.Exists(candidatePath))
                {
                    return Assembly.LoadFrom(candidatePath);
                }
            }
            catch
            {
                // Silently fallback if assembly cannot be resolved
            }

            return null;
        }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            // Initialize services
            _outputService = new VsOutputWindowService(this);
            _solutionService = new VsSolutionService(this);

            await _solutionService.InitializeAsync();

            // Switch to Main thread to initialize commands
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            await OpenToolWindowCommand.InitializeAsync(this, _outputService, _solutionService);
            await SolutionExplorerCommands.InitializeAsync(this, _outputService, _solutionService);

            await _outputService.WriteLineAsync("EF Core Helper extension initialized successfully.");
        }
    }
}
