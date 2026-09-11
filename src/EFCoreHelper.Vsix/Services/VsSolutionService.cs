using System;
using System.IO;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace EFCoreHelper.Vsix.Services
{
    public interface IVsSolutionService
    {
        event Action? SolutionChanged;

        Task<string?> GetActiveSolutionPathAsync();
        Task<string?> GetStartupProjectFilePathAsync();
        Task<string?> GetSelectedProjectFilePathAsync();
        Task OpenFileAsync(string filePath);
    }

    public class VsSolutionService : IVsSolutionService
    {
        private readonly AsyncPackage _package;
        private DTE2? _dte;
        private SolutionEvents? _solutionEvents;

        public event Action? SolutionChanged;

        public VsSolutionService(AsyncPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        public async Task InitializeAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _dte = await _package.GetServiceAsync(typeof(DTE)) as DTE2;
            if (_dte != null)
            {
                _solutionEvents = _dte.Events.SolutionEvents;
                _solutionEvents.Opened += () => SolutionChanged?.Invoke();
                _solutionEvents.AfterClosing += () => SolutionChanged?.Invoke();
            }
        }

        public async Task<string?> GetActiveSolutionPathAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (_dte == null)
            {
                _dte = await _package.GetServiceAsync(typeof(DTE)) as DTE2;
            }

            var fullName = _dte?.Solution?.FullName;
            return string.IsNullOrEmpty(fullName) ? null : fullName;
        }

        public async Task<string?> GetStartupProjectFilePathAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (_dte == null)
            {
                _dte = await _package.GetServiceAsync(typeof(DTE)) as DTE2;
            }

            try
            {
                var sb = _dte?.Solution?.SolutionBuild;
                if (sb?.StartupProjects != null && sb.StartupProjects is Array startupProjects && startupProjects.Length > 0)
                {
                    string startupProjName = startupProjects.GetValue(0)?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(startupProjName))
                    {
                        var slnDir = Path.GetDirectoryName(_dte.Solution.FullName);
                        if (!string.IsNullOrEmpty(slnDir))
                        {
                            var fullPath = Path.GetFullPath(Path.Combine(slnDir, startupProjName));
                            if (File.Exists(fullPath))
                                return fullPath;
                        }
                    }
                }
            }
            catch
            {
                // Fallback
            }

            return null;
        }

        public async Task<string?> GetSelectedProjectFilePathAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (_dte == null)
            {
                _dte = await _package.GetServiceAsync(typeof(DTE)) as DTE2;
            }

            try
            {
                var selectedItems = _dte?.SelectedItems;
                if (selectedItems != null && selectedItems.Count > 0)
                {
                    foreach (SelectedItem item in selectedItems)
                    {
                        if (item.Project != null && !string.IsNullOrEmpty(item.Project.FullName))
                        {
                            return item.Project.FullName;
                        }
                        if (item.ProjectItem?.ContainingProject != null && !string.IsNullOrEmpty(item.ProjectItem.ContainingProject.FullName))
                        {
                            return item.ProjectItem.ContainingProject.FullName;
                        }
                    }
                }
            }
            catch
            {
                // Fallback
            }

            return null;
        }

        public async Task OpenFileAsync(string filePath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (_dte == null)
            {
                _dte = await _package.GetServiceAsync(typeof(DTE)) as DTE2;
            }

            try
            {
                if (File.Exists(filePath))
                {
                    _dte?.ItemOperations?.OpenFile(filePath);
                }
            }
            catch
            {
                // Ignore if unable to open in editor
            }
        }
    }
}
