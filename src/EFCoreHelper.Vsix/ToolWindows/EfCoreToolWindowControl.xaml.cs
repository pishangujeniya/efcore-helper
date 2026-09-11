using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EFCoreHelper.Core.Models;
using EFCoreHelper.Core.Services;
using EFCoreHelper.Vsix.Dialogs;
using EFCoreHelper.Vsix.Services;
using Microsoft.VisualStudio.Shell;

namespace EFCoreHelper.Vsix.ToolWindows
{
    public partial class EfCoreToolWindowControl : UserControl
    {
        private readonly ISolutionScanner _scanner;
        private readonly IEfCliRunner _cliRunner;
        private readonly IEfCliCommandBuilder _commandBuilder;
        private readonly IDotnetEfDetector _detector;
        private IVsOutputWindowService? _outputWindowService;
        private IVsSolutionService? _solutionService;

        private IReadOnlyList<ProjectInfo> _projects = new List<ProjectInfo>();
        private CancellationTokenSource? _currentExecutionCts;

        public EfCoreToolWindowControl()
        {
            InitializeComponent();

            _cliRunner = new EfCliRunner();
            _scanner = new SolutionScanner();
            _commandBuilder = new EfCliCommandBuilder();
            _detector = new DotnetEfDetector(_cliRunner);

            _cliRunner.OutputLineReceived += OnCliOutputLineReceived;
            _cliRunner.ErrorLineReceived += OnCliErrorLineReceived;

            Loaded += async (s, e) => await InitializeAsync();
        }

        public void SetServices(IVsOutputWindowService outputWindowService, IVsSolutionService solutionService)
        {
            _outputWindowService = outputWindowService;
            _solutionService = solutionService;
            if (_solutionService != null)
            {
                _solutionService.SolutionChanged += () =>
                {
                    ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        await RefreshSolutionAsync();
                    });
                };
            }
        }

        private async Task InitializeAsync()
        {
            await CheckToolingAsync();
            await RefreshSolutionAsync();
        }

        private bool _isDotnetEfInstalled;

        private async Task CheckToolingAsync()
        {
            try
            {
                TxtFooterToolVersion.Text = "Checking tools...";
                BtnFooterUpdateTool.Visibility = Visibility.Collapsed;

                var status = await _detector.CheckStatusAsync().ConfigureAwait(true);
                if (!status.IsDotnetInstalled)
                {
                    _isDotnetEfInstalled = false;
                    ShowBanner(".NET CLI ('dotnet') was not found on PATH. Please install .NET Core SDK.", null, null);
                    TxtFooterToolVersion.Text = ".NET CLI not found";
                    BtnFooterUpdateTool.Visibility = Visibility.Collapsed;
                }
                else if (!status.IsDotnetEfInstalled)
                {
                    _isDotnetEfInstalled = false;
                    ShowBanner("The 'dotnet-ef' global tool is not installed. Click to install it now.", "Install dotnet-ef", async () =>
                    {
                        await InstallOrUpdateToolAsync(isInstall: true);
                    });
                    TxtFooterToolVersion.Text = "dotnet-ef: Not Installed";
                    BtnFooterUpdateTool.Content = "Install";
                    BtnFooterUpdateTool.ToolTip = "Install dotnet-ef global tool (dotnet tool install --global dotnet-ef)";
                    BtnFooterUpdateTool.Visibility = Visibility.Visible;
                }
                else if (status.IsUpdateAvailable)
                {
                    _isDotnetEfInstalled = true;
                    ShowBanner($"A newer version of dotnet-ef is available (v{status.LatestDotnetEfVersion}, installed v{status.DotnetEfVersion}). Click to update now.", "Update dotnet-ef", async () =>
                    {
                        await InstallOrUpdateToolAsync(isInstall: false);
                    });
                    TxtFooterToolVersion.Text = $"dotnet-ef v{status.DotnetEfVersion} (v{status.LatestDotnetEfVersion} available)";
                    BtnFooterUpdateTool.Content = "Update";
                    BtnFooterUpdateTool.ToolTip = $"Update dotnet-ef from v{status.DotnetEfVersion} to v{status.LatestDotnetEfVersion} (dotnet tool update --global dotnet-ef)";
                    BtnFooterUpdateTool.Visibility = Visibility.Visible;
                }
                else
                {
                    _isDotnetEfInstalled = true;
                    if (BannerWarning.Visibility == Visibility.Visible && _bannerAction != null)
                    {
                        BannerWarning.Visibility = Visibility.Collapsed;
                    }
                    TxtFooterToolVersion.Text = $"dotnet-ef v{status.DotnetEfVersion} (.NET v{status.DotnetVersion})";
                    BtnFooterUpdateTool.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                ShowBanner($"Error verifying EF Core tools: {ex.Message}", null, null);
            }
        }

        private async Task InstallOrUpdateToolAsync(bool isInstall)
        {
            string command = isInstall ? _detector.GetInstallToolCommand() : _detector.GetUpdateToolCommand();
            MainTabs.SelectedIndex = 1; // Switch to Execution Console tab
            await RunCommandLineAsync(command, null, async () =>
            {
                await CheckToolingAsync();
            });
        }

        private async void OnUpdateDotnetEfClicked(object sender, RoutedEventArgs e)
        {
            await InstallOrUpdateToolAsync(isInstall: !_isDotnetEfInstalled);
        }

        private Action? _bannerAction;
        private void ShowBanner(string message, string? actionText, Action? action)
        {
            BannerWarning.Visibility = Visibility.Visible;
            TxtBannerMessage.Text = message;
            _bannerAction = action;

            if (!string.IsNullOrEmpty(actionText) && action != null)
            {
                BtnBannerAction.Content = actionText;
                BtnBannerAction.Visibility = Visibility.Visible;
            }
            else
            {
                BtnBannerAction.Visibility = Visibility.Collapsed;
            }
        }

        private void OnBannerActionClicked(object sender, RoutedEventArgs e)
        {
            _bannerAction?.Invoke();
        }

        public async Task EnsureInitializedAsync()
        {
            if (_projects == null || _projects.Count == 0)
            {
                await RefreshSolutionAsync();
            }
        }

        public void SelectProjectByPath(string? projectFilePath)
        {
            if (string.IsNullOrWhiteSpace(projectFilePath) || _projects == null || _projects.Count == 0)
                return;

            string normalized = Path.GetFullPath(projectFilePath);
            string projName = Path.GetFileNameWithoutExtension(projectFilePath);

            var match = _projects.FirstOrDefault(p =>
                string.Equals(p.FilePath, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name, projName, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                CmbProject.SelectedItem = match;
            }
        }

        public async Task RefreshSolutionAsync()
        {
            try
            {
                TxtFooterStatus.Text = "Scanning solution for .NET Core EF projects...";

                string? slnPath = null;
                string? vsStartupPath = null;
                if (_solutionService != null)
                {
                    slnPath = await _solutionService.GetActiveSolutionPathAsync();
                    vsStartupPath = await _solutionService.GetStartupProjectFilePathAsync();
                }

                if (string.IsNullOrEmpty(slnPath))
                {
                    TxtFooterStatus.Text = "No active solution opened.";
                    _projects = new List<ProjectInfo>();
                    PopulateProjectsDropdown();
                    return;
                }

                _projects = await _scanner.ScanSolutionAsync(slnPath).ConfigureAwait(true);

                PopulateProjectsDropdown(vsStartupPath);
                TxtFooterStatus.Text = $"Discovered {_projects.Count} .NET Core project(s).";
            }
            catch (Exception ex)
            {
                TxtFooterStatus.Text = $"Error scanning solution: {ex.Message}";
            }
        }

        private void PopulateProjectsDropdown(string? vsStartupPath = null)
        {
            CmbProject.ItemsSource = _projects;
            CmbStartupProject.ItemsSource = _projects;

            // 1. Prefer active Visual Studio startup project, else Web/Exe project as startup project
            ProjectInfo? startup = null;
            if (!string.IsNullOrEmpty(vsStartupPath))
            {
                startup = _projects.FirstOrDefault(p => string.Equals(p.FilePath, vsStartupPath, StringComparison.OrdinalIgnoreCase) ||
                                                       string.Equals(p.Name, Path.GetFileNameWithoutExtension(vsStartupPath), StringComparison.OrdinalIgnoreCase));
            }

            startup = startup ?? _projects.FirstOrDefault(p => p.IsStartupProject) ?? _projects.FirstOrDefault();
            if (startup != null)
            {
                CmbStartupProject.SelectedItem = startup;
            }

            // 2. Prefer project with DbContexts or Migrations as Target Project
            var target = _projects.FirstOrDefault(p => p.DbContexts.Count > 0)
                      ?? _projects.FirstOrDefault(p => p.Migrations.Count > 0)
                      ?? _projects.FirstOrDefault(p => p.HasEfCoreReference && !p.IsStartupProject)
                      ?? _projects.FirstOrDefault(p => p.HasEfCoreReference)
                      ?? startup
                      ?? _projects.FirstOrDefault();

            if (target != null)
            {
                CmbProject.SelectedItem = target;
            }
            else if (_projects.Count > 0)
            {
                CmbProject.SelectedIndex = 0;
            }

            UpdateProjectContextsAndMigrations();
        }

        private void UpdateProjectContextsAndMigrations()
        {
            var project = CmbProject.SelectedItem as ProjectInfo;
            if (project == null)
            {
                CmbContext.ItemsSource = null;
                LstMigrations.ItemsSource = null;
                TxtMigrationCount.Text = "0 migrations";
                PnlNoMigrations.Visibility = Visibility.Visible;
                return;
            }

            // Verify Microsoft.EntityFrameworkCore.Design package
            // Design package can be referenced in EITHER target project or startup project
            var startupProject = CmbStartupProject.SelectedItem as ProjectInfo;
            bool hasDesignPackage = project.HasEfDesignReference || (startupProject != null && startupProject.HasEfDesignReference);

            if (!hasDesignPackage && project.HasEfCoreReference)
            {
                ShowBanner($"Notice: Project '{project.Name}' does not reference 'Microsoft.EntityFrameworkCore.Design'. EF Core commands require design tools.", null, null);
            }
            else if (BannerWarning.Visibility == Visibility.Visible && _bannerAction == null)
            {
                BannerWarning.Visibility = Visibility.Collapsed;
            }

            // If selected project has DbContexts, show them.
            // If selected project has NO DbContexts, fallback to all DbContexts in the solution so the user can easily select one!
            IReadOnlyList<DbContextInfo> contexts = project.DbContexts;
            if (contexts.Count == 0 && _projects.Count > 0)
            {
                contexts = _projects.SelectMany(p => p.DbContexts).ToList();
            }

            CmbContext.ItemsSource = contexts;
            if (contexts.Count > 0)
            {
                CmbContext.SelectedIndex = 0;
            }

            LstMigrations.ItemsSource = project.Migrations;
            TxtMigrationCount.Text = $"{project.Migrations.Count} migration(s)";
            PnlNoMigrations.Visibility = project.Migrations.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnProjectChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateProjectContextsAndMigrations();
        }

        private void OnStartupProjectChanged(object sender, SelectionChangedEventArgs e) { }

        private void OnContextChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedContext = CmbContext.SelectedItem as DbContextInfo;
            if (selectedContext != null && !string.IsNullOrEmpty(selectedContext.ProjectPath))
            {
                var matchingProject = _projects.FirstOrDefault(p =>
                    string.Equals(p.FilePath, selectedContext.ProjectPath, StringComparison.OrdinalIgnoreCase));

                if (matchingProject != null && CmbProject.SelectedItem != matchingProject)
                {
                    CmbProject.SelectedItem = matchingProject;
                }
            }
        }

        private async void OnRefreshClicked(object sender, RoutedEventArgs e)
        {
            await RefreshSolutionAsync();
        }

        #region Actions

        public void OpenAddMigrationDialog()
        {
            var project = CmbProject.SelectedItem as ProjectInfo;
            var startup = CmbStartupProject.SelectedItem as ProjectInfo;
            var context = CmbContext.SelectedItem as DbContextInfo;

            var dlg = new AddMigrationDialog(_projects, project, startup, context, _commandBuilder);
            if (dlg.ShowDialog() == true && dlg.Options != null)
            {
                EnsureStartupProject(dlg.Options, startup);
                string cmd = _commandBuilder.BuildAddMigrationCommand(dlg.Options);
                ExecuteCommand(cmd, dlg.Options.Project, async () =>
                {
                    await RefreshSolutionAsync();
                });
            }
        }

        private void OnAddMigrationClicked(object sender, RoutedEventArgs e)
        {
            OpenAddMigrationDialog();
        }

        public void OpenUpdateDatabaseDialog()
        {
            var project = CmbProject.SelectedItem as ProjectInfo;
            var startup = CmbStartupProject.SelectedItem as ProjectInfo;
            var context = CmbContext.SelectedItem as DbContextInfo;

            var dlg = new UpdateDatabaseDialog(_projects, project, startup, context, null, _commandBuilder);
            if (dlg.ShowDialog() == true && dlg.Options != null)
            {
                EnsureStartupProject(dlg.Options, startup);
                string cmd = _commandBuilder.BuildUpdateDatabaseCommand(dlg.Options);
                ExecuteCommand(cmd, dlg.Options.Project, null);
            }
        }

        private void OnUpdateDatabaseClicked(object sender, RoutedEventArgs e)
        {
            OpenUpdateDatabaseDialog();
        }

        public void OpenRemoveMigrationDialog()
        {
            var project = CmbProject.SelectedItem as ProjectInfo;
            var startup = CmbStartupProject.SelectedItem as ProjectInfo;
            var context = CmbContext.SelectedItem as DbContextInfo;

            var dlg = new RemoveMigrationDialog(_projects, project, startup, context, _commandBuilder);
            if (dlg.ShowDialog() == true && dlg.Options != null)
            {
                EnsureStartupProject(dlg.Options, startup);
                string cmd = _commandBuilder.BuildRemoveMigrationCommand(dlg.Options);
                ExecuteCommand(cmd, dlg.Options.Project, async () =>
                {
                    await RefreshSolutionAsync();
                });
            }
        }

        private void OnRemoveMigrationClicked(object sender, RoutedEventArgs e)
        {
            OpenRemoveMigrationDialog();
        }

        public void OpenScriptMigrationDialog()
        {
            var project = CmbProject.SelectedItem as ProjectInfo;
            var startup = CmbStartupProject.SelectedItem as ProjectInfo;
            var context = CmbContext.SelectedItem as DbContextInfo;

            var dlg = new ScriptMigrationDialog(_projects, project, startup, context, _commandBuilder);
            if (dlg.ShowDialog() == true && dlg.Options != null)
            {
                EnsureStartupProject(dlg.Options, startup);
                if (string.IsNullOrWhiteSpace(dlg.Options.OutputFilePath))
                {
                    dlg.Options.OutputFilePath = ScriptMigrationOptions.GetDefaultScriptPath(dlg.Options.StartupProject ?? dlg.Options.Project);
                }

                string cmd = _commandBuilder.BuildScriptMigrationCommand(dlg.Options);
                var scriptPath = dlg.Options.OutputFilePath;
                ExecuteCommand(cmd, dlg.Options.Project, () =>
                {
                    if (!string.IsNullOrEmpty(scriptPath))
                    {
                        TxtFooterStatus.Text = $"Script generated at {Path.GetFileName(scriptPath)}";
                        ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                        {
                            if (_solutionService != null)
                            {
                                await _solutionService.OpenFileAsync(scriptPath);
                            }
                        });
                    }
                });
            }
        }

        private void OnScriptMigrationClicked(object sender, RoutedEventArgs e)
        {
            OpenScriptMigrationDialog();
        }

        public void OpenBundleMigrationsDialog()
        {
            var project = CmbProject.SelectedItem as ProjectInfo;
            var startup = CmbStartupProject.SelectedItem as ProjectInfo;

            var dlg = new BundleMigrationsDialog(_projects, project, startup, _commandBuilder);
            if (dlg.ShowDialog() == true && dlg.Options != null)
            {
                EnsureStartupProject(dlg.Options, startup);
                string cmd = _commandBuilder.BuildBundleMigrationCommand(dlg.Options);
                ExecuteCommand(cmd, dlg.Options.Project, null);
            }
        }

        private void OnBundleMigrationsClicked(object sender, RoutedEventArgs e)
        {
            OpenBundleMigrationsDialog();
        }

        public void OpenScaffoldDbContextDialog()
        {
            var project = CmbProject.SelectedItem as ProjectInfo;
            var startup = CmbStartupProject.SelectedItem as ProjectInfo;

            var dlg = new ScaffoldDbContextDialog(_projects, project, startup, _commandBuilder);
            if (dlg.ShowDialog() == true && dlg.Options != null)
            {
                EnsureStartupProject(dlg.Options, startup);
                string cmd = _commandBuilder.BuildScaffoldDbContextCommand(dlg.Options);
                ExecuteCommand(cmd, dlg.Options.Project, async () =>
                {
                    await RefreshSolutionAsync();
                });
            }
        }

        private void OnScaffoldDbContextClicked(object sender, RoutedEventArgs e)
        {
            OpenScaffoldDbContextDialog();
        }

        public void OpenDropDatabaseDialog()
        {
            var project = CmbProject.SelectedItem as ProjectInfo;
            var startup = CmbStartupProject.SelectedItem as ProjectInfo;
            var context = CmbContext.SelectedItem as DbContextInfo;

            var dlg = new DropDatabaseDialog(_projects, project, startup, context, _commandBuilder);
            if (dlg.ShowDialog() == true && dlg.Options != null)
            {
                EnsureStartupProject(dlg.Options, startup);
                string cmd = _commandBuilder.BuildDropDatabaseCommand(dlg.Options);
                ExecuteCommand(cmd, dlg.Options.Project, null);
            }
        }

        private void OnDropDatabaseClicked(object sender, RoutedEventArgs e)
        {
            OpenDropDatabaseDialog();
        }

        public void OpenOptimizeDbContextDialog()
        {
            var project = CmbProject.SelectedItem as ProjectInfo;
            var startup = CmbStartupProject.SelectedItem as ProjectInfo;
            var context = CmbContext.SelectedItem as DbContextInfo;

            var dlg = new OptimizeDbContextDialog(_projects, project, startup, context, _commandBuilder);
            if (dlg.ShowDialog() == true && dlg.Options != null)
            {
                EnsureStartupProject(dlg.Options, startup);
                string cmd = _commandBuilder.BuildOptimizeDbContextCommand(dlg.Options);
                ExecuteCommand(cmd, dlg.Options.Project, null);
            }
        }

        private void OnOptimizeDbContextClicked(object sender, RoutedEventArgs e)
        {
            OpenOptimizeDbContextDialog();
        }

        private void OnContextUpdateToMigrationClicked(object sender, RoutedEventArgs e)
        {
            var selectedMigration = LstMigrations.SelectedItem as MigrationInfo;
            if (selectedMigration == null) return;

            var project = CmbProject.SelectedItem as ProjectInfo;
            var startup = CmbStartupProject.SelectedItem as ProjectInfo;
            var context = CmbContext.SelectedItem as DbContextInfo;

            var dlg = new UpdateDatabaseDialog(_projects, project, startup, context, selectedMigration.Name, _commandBuilder);
            if (dlg.ShowDialog() == true && dlg.Options != null)
            {
                EnsureStartupProject(dlg.Options, startup);
                string cmd = _commandBuilder.BuildUpdateDatabaseCommand(dlg.Options);
                ExecuteCommand(cmd, dlg.Options.Project, null);
            }
        }

        private void OnContextScriptFromMigrationClicked(object sender, RoutedEventArgs e)
        {
            var selectedMigration = LstMigrations.SelectedItem as MigrationInfo;
            if (selectedMigration == null) return;

            var project = CmbProject.SelectedItem as ProjectInfo;
            var startup = CmbStartupProject.SelectedItem as ProjectInfo;
            var context = CmbContext.SelectedItem as DbContextInfo;

            var dlg = new ScriptMigrationDialog(_projects, project, startup, context, _commandBuilder);
            dlg.CmbFromMigration.Text = selectedMigration.Name;
            if (dlg.ShowDialog() == true && dlg.Options != null)
            {
                EnsureStartupProject(dlg.Options, startup);
                if (string.IsNullOrWhiteSpace(dlg.Options.OutputFilePath))
                {
                    dlg.Options.OutputFilePath = ScriptMigrationOptions.GetDefaultScriptPath(dlg.Options.StartupProject ?? dlg.Options.Project);
                }

                string cmd = _commandBuilder.BuildScriptMigrationCommand(dlg.Options);
                var scriptPath = dlg.Options.OutputFilePath;
                ExecuteCommand(cmd, dlg.Options.Project, () =>
                {
                    if (!string.IsNullOrEmpty(scriptPath))
                    {
                        TxtFooterStatus.Text = $"Script generated at {Path.GetFileName(scriptPath)}";
                        ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                        {
                            if (_solutionService != null)
                            {
                                await _solutionService.OpenFileAsync(scriptPath);
                            }
                        });
                    }
                });
            }
        }

        private void EnsureStartupProject(BaseCommandOptions options, ProjectInfo? fallbackStartup)
        {
            if (string.IsNullOrWhiteSpace(options.StartupProject))
            {
                var startup = fallbackStartup ?? (CmbStartupProject.SelectedItem as ProjectInfo);
                if (startup != null)
                {
                    options.StartupProject = startup.FilePath;
                }
            }
        }

        #endregion

        #region Execution Console

        private void ExecuteCommand(string commandLine, string? projectFilePath, Action? onCompleted)
        {
            string? workingDir = null;
            if (!string.IsNullOrEmpty(projectFilePath) && File.Exists(projectFilePath))
            {
                workingDir = Path.GetDirectoryName(projectFilePath);
            }

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await RunCommandLineAsync(commandLine, workingDir, onCompleted);
            });
        }

        private async Task RunCommandLineAsync(string commandLine, string? workingDir = null, Action? onCompleted = null)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Switch to Execution Console tab
            MainTabs.SelectedIndex = 1;

            _currentExecutionCts?.Cancel();
            _currentExecutionCts = new CancellationTokenSource();
            var token = _currentExecutionCts.Token;

            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Yellow
            TxtConsoleStatus.Text = "Running...";
            BtnCancelCommand.IsEnabled = true;

            AppendConsoleLine($"══════════════════════════════════════════════════════════════");
            AppendConsoleLine($"[{DateTime.Now:HH:mm:ss}] Executing: {commandLine}");
            if (!string.IsNullOrEmpty(workingDir))
            {
                AppendConsoleLine($"Working Directory: {workingDir}");
            }
            AppendConsoleLine($"══════════════════════════════════════════════════════════════");

            if (_outputWindowService != null)
            {
                await _outputWindowService.WriteLineAsync($"\n[{DateTime.Now:HH:mm:ss}] > {commandLine}");
            }

            try
            {
                var result = await _cliRunner.ExecuteAsync(commandLine, workingDir, token).ConfigureAwait(true);

                if (result.WasCancelled)
                {
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Red
                    TxtConsoleStatus.Text = "Cancelled";
                    AppendConsoleLine($"\n[CANCELLED] Operation cancelled by user after {result.ElapsedTime.TotalSeconds:F2}s");
                    TxtFooterStatus.Text = "Command execution cancelled.";
                }
                else if (result.Success)
                {
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(40, 167, 69)); // Green
                    TxtConsoleStatus.Text = $"Succeeded ({result.ElapsedTime.TotalSeconds:F2}s)";
                    AppendConsoleLine($"\n[SUCCESS] Process exited with code 0 in {result.ElapsedTime.TotalSeconds:F2}s");
                    TxtFooterStatus.Text = "Command completed successfully.";
                    onCompleted?.Invoke();
                }
                else
                {
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Red
                    TxtConsoleStatus.Text = $"Failed (Exit code {result.ExitCode})";
                    AppendConsoleLine($"\n[FAILED] Process exited with code {result.ExitCode} in {result.ElapsedTime.TotalSeconds:F2}s");
                    TxtFooterStatus.Text = $"Command failed with exit code {result.ExitCode}.";
                }
            }
            catch (Exception ex)
            {
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Red
                TxtConsoleStatus.Text = "Error";
                AppendConsoleLine($"\n[ERROR] {ex.Message}");
                TxtFooterStatus.Text = $"Execution error: {ex.Message}";
            }
            finally
            {
                BtnCancelCommand.IsEnabled = false;
            }
        }

        private void OnCliOutputLineReceived(string line)
        {
            Dispatcher.InvokeAsync(() =>
            {
                AppendConsoleLine(line);
            });
        }

        private void OnCliErrorLineReceived(string line)
        {
            Dispatcher.InvokeAsync(() =>
            {
                AppendConsoleLine($"[ERROR] {line}");
            });
        }

        private void AppendConsoleLine(string line)
        {
            TxtConsoleOutput.AppendText(line + Environment.NewLine);
            TxtConsoleOutput.ScrollToEnd();

            _outputWindowService?.WriteLineAsync(line);
        }

        private void OnCancelExecutionClicked(object sender, RoutedEventArgs e)
        {
            _currentExecutionCts?.Cancel();
            BtnCancelCommand.IsEnabled = false;
            TxtConsoleStatus.Text = "Cancelling...";
        }

        private void OnCopyLogClicked(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtConsoleOutput.Text))
            {
                Clipboard.SetText(TxtConsoleOutput.Text);
            }
        }

        private void OnClearLogClicked(object sender, RoutedEventArgs e)
        {
            TxtConsoleOutput.Clear();
            StatusDot.Fill = Brushes.Gray;
            TxtConsoleStatus.Text = "Ready";
        }

        #endregion
    }
}
