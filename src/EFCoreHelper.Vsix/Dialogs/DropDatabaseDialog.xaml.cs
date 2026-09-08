using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using EFCoreHelper.Core.Models;
using EFCoreHelper.Core.Services;
using Microsoft.VisualStudio.PlatformUI;

namespace EFCoreHelper.Vsix.Dialogs
{
    public partial class DropDatabaseDialog : DialogWindow
    {
        private readonly IEfCliCommandBuilder _commandBuilder;
        private readonly IReadOnlyList<ProjectInfo> _projects;
        public DropDatabaseOptions? Options { get; private set; }

        public DropDatabaseDialog(
            IReadOnlyList<ProjectInfo> projects,
            ProjectInfo? selectedProject = null,
            DbContextInfo? selectedContext = null,
            IEfCliCommandBuilder? commandBuilder = null)
        {
            InitializeComponent();
            _commandBuilder = commandBuilder ?? new EfCliCommandBuilder();
            _projects = projects ?? new List<ProjectInfo>();

            Loaded += (s, e) =>
            {
                CmbProject.ItemsSource = _projects;
                CmbProject.DisplayMemberPath = "Name";

                CmbStartupProject.ItemsSource = _projects;
                CmbStartupProject.DisplayMemberPath = "Name";

                if (selectedProject != null && (selectedProject.DbContexts.Count > 0 || _projects.All(p => p.DbContexts.Count == 0)))
                {
                    CmbProject.SelectedItem = selectedProject;
                }
                else
                {
                    var preferred = _projects.FirstOrDefault(p => p.DbContexts.Count > 0) ?? selectedProject ?? _projects.FirstOrDefault();
                    if (preferred != null)
                    {
                        CmbProject.SelectedItem = preferred;
                    }
                }

                var startup = _projects.FirstOrDefault(p => p.IsStartupProject) ?? _projects.FirstOrDefault();
                if (startup != null)
                {
                    CmbStartupProject.SelectedItem = startup;
                }

                UpdateContexts(selectedContext);
                UpdatePreview();
            };
        }

        private void UpdateContexts(DbContextInfo? preselect = null)
        {
            var project = CmbProject.SelectedItem as ProjectInfo;
            var contexts = project?.DbContexts?.ToList() ?? new List<DbContextInfo>();

            if (contexts.Count == 0 && _projects.Count > 0)
            {
                contexts = _projects.SelectMany(p => p.DbContexts).ToList();
            }

            CmbContext.ItemsSource = contexts;

            var match = preselect != null
                ? contexts.FirstOrDefault(c => c.Name == preselect.Name)
                : null;

            if (match != null)
            {
                CmbContext.SelectedItem = match;
            }
            else if (contexts.Count > 0)
            {
                CmbContext.SelectedIndex = 0;
            }
        }

        private void OnFieldChanged(object sender, RoutedEventArgs e)
        {
            if (sender == CmbProject)
            {
                var keepCtx = CmbContext.SelectedItem as DbContextInfo;
                UpdateContexts(keepCtx);
            }
            else if (sender == CmbContext && CmbContext.SelectedItem is DbContextInfo ctx)
            {
                var owner = _projects.FirstOrDefault(p => p.DbContexts.Any(c => c.Name == ctx.Name));
                if (owner != null && CmbProject.SelectedItem != owner)
                {
                    CmbProject.SelectedItem = owner;
                    return;
                }
            }
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (TxtCommandPreview == null) return;
            try
            {
                var options = BuildOptions();
                TxtCommandPreview.Text = _commandBuilder.BuildDropDatabaseCommand(options);
            }
            catch
            {
                TxtCommandPreview.Text = "dotnet ef database drop";
            }
        }

        private DropDatabaseOptions BuildOptions()
        {
            var project = CmbProject.SelectedItem as ProjectInfo;
            var startup = CmbStartupProject.SelectedItem as ProjectInfo;
            var context = CmbContext.SelectedItem as DbContextInfo;

            return new DropDatabaseOptions
            {
                Project = project?.FilePath,
                StartupProject = startup?.FilePath,
                Context = context?.Name,
                Force = ChkForce.IsChecked == true,
                DryRun = ChkDryRun.IsChecked == true
            };
        }

        private void OnDropClicked(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "WARNING: Are you ABSOLUTELY sure you want to drop the database? All tables, views, and data will be permanently destroyed!",
                "CRITICAL: Confirm Database Drop",
                MessageBoxButton.YesNo,
                MessageBoxImage.Exclamation);

            if (result == MessageBoxResult.Yes)
            {
                Options = BuildOptions();
                DialogResult = true;
                Close();
            }
        }

        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OnCopyPreviewClicked(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtCommandPreview.Text))
            {
                Clipboard.SetText(TxtCommandPreview.Text);
            }
        }
    }
}
