using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using EFCoreHelper.Core.Models;
using EFCoreHelper.Core.Services;

using Microsoft.VisualStudio.PlatformUI;

namespace EFCoreHelper.Vsix.Dialogs
{
    public partial class AddMigrationDialog : DialogWindow
    {
        private readonly IEfCliCommandBuilder _commandBuilder;
        private readonly IReadOnlyList<ProjectInfo> _projects;
        public AddMigrationOptions? Options { get; private set; }

        public AddMigrationDialog(
            IReadOnlyList<ProjectInfo> projects,
            ProjectInfo? selectedProject = null,
            ProjectInfo? selectedStartupProject = null,
            DbContextInfo? selectedContext = null,
            IEfCliCommandBuilder? commandBuilder = null)
        {
            InitializeComponent();
            _commandBuilder = commandBuilder ?? new EfCliCommandBuilder();
            _projects = projects ?? new List<ProjectInfo>();

            Loaded += (s, e) =>
            {
                PopulateDropdowns(selectedProject, selectedStartupProject, selectedContext);
                TxtMigrationName.Focus();
                UpdatePreview();
            };
        }

        private void PopulateDropdowns(ProjectInfo? selectedProject, ProjectInfo? selectedStartupProject, DbContextInfo? selectedContext)
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

            var startup = selectedStartupProject
                ?? _projects.FirstOrDefault(p => p.IsStartupProject)
                ?? _projects.FirstOrDefault();
            if (startup != null)
            {
                CmbStartupProject.SelectedItem = startup;
            }

            UpdateContexts(selectedContext);
        }

        private void UpdateContexts(DbContextInfo? preselect = null)
        {
            var currentProject = CmbProject.SelectedItem as ProjectInfo;
            var contexts = currentProject?.DbContexts?.ToList() ?? new List<DbContextInfo>();

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
            if (TxtCommandPreview == null || TxtMigrationName == null)
                return;

            try
            {
                var options = BuildOptions();
                if (string.IsNullOrWhiteSpace(options.MigrationName))
                {
                    TxtCommandPreview.Text = "dotnet ef migrations add <Name>";
                    BtnAdd.IsEnabled = false;
                    return;
                }

                TxtCommandPreview.Text = _commandBuilder.BuildAddMigrationCommand(options);
                BtnAdd.IsEnabled = true;
            }
            catch
            {
                TxtCommandPreview.Text = "dotnet ef migrations add <Name>";
                BtnAdd.IsEnabled = false;
            }
        }

        private AddMigrationOptions BuildOptions()
        {
            var project = CmbProject.SelectedItem as ProjectInfo;
            var startup = CmbStartupProject.SelectedItem as ProjectInfo;
            var context = CmbContext.SelectedItem as DbContextInfo;

            return new AddMigrationOptions
            {
                MigrationName = TxtMigrationName.Text.Trim(),
                Project = project?.FilePath,
                StartupProject = startup?.FilePath,
                Context = context?.Name ?? CmbContext.Text.Trim(),
                OutputDir = string.IsNullOrWhiteSpace(TxtOutputDir.Text) ? null : TxtOutputDir.Text.Trim(),
                Namespace = string.IsNullOrWhiteSpace(TxtNamespace.Text) ? null : TxtNamespace.Text.Trim(),
                NoBuild = ChkNoBuild.IsChecked == true,
                Verbose = ChkVerbose.IsChecked == true
            };
        }

        private void OnAddClicked(object sender, RoutedEventArgs e)
        {
            var name = TxtMigrationName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please provide a name for the migration.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtMigrationName.Focus();
                return;
            }

            Options = BuildOptions();
            DialogResult = true;
            Close();
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
