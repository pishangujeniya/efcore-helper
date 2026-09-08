using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace EFCoreHelper.Vsix.Services
{
    public interface IVsOutputWindowService
    {
        Task WriteLineAsync(string message);
        Task WriteAsync(string message);
        Task ClearAsync();
        Task ActivateAsync();
    }

    public class VsOutputWindowService : IVsOutputWindowService
    {
        private readonly AsyncPackage _package;
        private static readonly Guid EfCorePaneGuid = new Guid("98b3c4a1-5d2e-4b71-8409-1a3b4c5d6e7f");
        private const string PaneTitle = "Entity Framework Core";
        private IVsOutputWindowPane? _pane;

        public VsOutputWindowService(AsyncPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        private async Task<IVsOutputWindowPane?> GetPaneAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (_pane != null)
                return _pane;

            var outputWindow = await _package.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow == null)
                return null;

            var guid = EfCorePaneGuid;
            int hr = outputWindow.GetPane(ref guid, out _pane);

            if (hr != VSConstants.S_OK || _pane == null)
            {
                outputWindow.CreatePane(ref guid, PaneTitle, 1, 1);
                outputWindow.GetPane(ref guid, out _pane);
            }

            return _pane;
        }

        public async Task WriteLineAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var pane = await GetPaneAsync().ConfigureAwait(true);
            pane?.OutputStringThreadSafe(message + Environment.NewLine);
        }

        public async Task WriteAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var pane = await GetPaneAsync().ConfigureAwait(true);
            pane?.OutputStringThreadSafe(message);
        }

        public async Task ClearAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var pane = await GetPaneAsync().ConfigureAwait(true);
            pane?.Clear();
        }

        public async Task ActivateAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var pane = await GetPaneAsync().ConfigureAwait(true);
            pane?.Activate();
        }
    }
}
