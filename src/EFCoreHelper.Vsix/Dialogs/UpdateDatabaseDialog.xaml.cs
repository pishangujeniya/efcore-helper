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
    public partial class UpdateDatabaseDialog : DialogWindow
    {
        private readonly IEfCliCommandBuilder _commandBuilder;
        private readonly IReadOnlyList<ProjectInfo> _projects;
        public UpdateDatabaseOptions? Options { get; private set; }

        public UpdateDatabaseDialog(
            IReadOnlyList<ProjectInfo> projects,
            ProjectInfo? selectedProject = null,
            ProjectInfo? selectedStartupProject = null,
            DbContextInfo? selectedContext = null,
            string? targetMigration = null,
            IEfCliCommandBuilder? commandBuilder = null)
        {
            InitializeComponent();
            _commandBuilder = commandBuilder ?? new EfCliCommandBuilder();
            _projects = projects ?? new List<ProjectInfo>();

            Loaded += (s, e) =>
            {
                PopulateDropdowns(selectedProject, selectedStartupProject, selectedContext, targetMigration);
                UpdatePreview();
            };
        }

        private void PopulateDropdowns(ProjectInfo? selectedProject, ProjectInfo? selectedStartupProject, DbContextInfo? selectedContext, string? targetMigration)
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

            UpdateProjectDependencies(selectedContext, targetMigration);
        }

        private void UpdateProjectDependencies(DbContextInfo? preselectContext = null, string? preselectMigration = null)
        {
            var project = CmbProject.SelectedItem as ProjectInfo;

            // Update Contexts
            var contexts = project?.DbContexts?.ToList() ?? new List<DbContextInfo>();
            if (contexts.Count == 0 && _projects.Count > 0)
            {
                contexts = _projects.SelectMany(p => p.DbContexts).ToList();
            }

            CmbContext.ItemsSource = contexts;

            var matchContext = preselectContext != null
                ? contexts.FirstOrDefault(c => c.Name == preselectContext.Name)
                : null;

            if (matchContext != null)
            {
                CmbContext.SelectedItem = matchContext;
            }
            else if (contexts.Count > 0)
            {
                CmbContext.SelectedIndex = 0;
            }

            // Update Target Migrations
            var migrationItems = new List<string> { "<Latest> (Apply all pending)", "0 (Revert all migrations)" };
            if (project?.Migrations != null)
            {
                foreach (var m in project.Migrations)
                {
                    migrationItems.Add(m.Name);
                }
            }

            CmbTargetMigration.ItemsSource = migrationItems;
            if (!string.IsNullOrEmpty(preselectMigration) && migrationItems.Contains(preselectMigration))
            {
                CmbTargetMigration.SelectedItem = preselectMigration;
            }
            else
            {
                CmbTargetMigration.SelectedIndex = 0;
            }

            // Update Connection Strings
            var connStrings = new List<string>();
            if (project?.ConnectionStrings != null)
            {
                foreach (var conn in project.ConnectionStrings)
                {
                    connStrings.Add(conn.Value);
                }
            }

            var startup = CmbStartupProject.SelectedItem as ProjectInfo;
            if (startup != null && startup != project && startup.ConnectionStrings != null)
            {
                foreach (var conn in startup.ConnectionStrings)
                {
                    if (!connStrings.Contains(conn.Value))
                    {
                        connStrings.Add(conn.Value);
                    }
                }
            }

            CmbConnectionString.ItemsSource = connStrings;
        }

        private void OnFieldChanged(object sender, RoutedEventArgs e)
        {
            if (sender == CmbProject || sender == CmbStartupProject)
            {
                var keepCtx = CmbContext.SelectedItem as DbContextInfo;
                UpdateProjectDependencies(keepCtx);
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

        private void OnTargetTextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();
        private void OnConnectionTextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

        private void UpdatePreview()
        {
            if (TxtCommandPreview == null)
                return;

            try
            {
                var options = BuildOptions();
                TxtCommandPreview.Text = _commandBuilder.BuildUpdateDatabaseCommand(options);
            }
            catch
            {
                TxtCommandPreview.Text = "dotnet ef database update";
            }
        }

        private UpdateDatabaseOptions BuildOptions()
        {
            var project = CmbProject.SelectedItem as ProjectInfo;
            var startup = CmbStartupProject.SelectedItem as ProjectInfo;
            var context = CmbContext.SelectedItem as DbContextInfo;

            string? target = CmbTargetMigration.Text.Trim();
            if (string.Equals(target, "<Latest> (Apply all pending)", StringComparison.OrdinalIgnoreCase))
            {
                target = null;
            }

            string? connStr = CmbConnectionString.Text.Trim();
            if (string.IsNullOrWhiteSpace(connStr))
            {
                connStr = null;
            }

            return new UpdateDatabaseOptions
            {
                TargetMigration = target,
                ConnectionString = connStr,
                Project = project?.FilePath,
                StartupProject = startup?.FilePath,
                Context = context?.Name ?? (string.IsNullOrWhiteSpace(CmbContext.Text) ? null : CmbContext.Text.Trim()),
                NoBuild = ChkNoBuild.IsChecked == true,
                Verbose = ChkVerbose.IsChecked == true
            };
        }

        private void OnUpdateClicked(object sender, RoutedEventArgs e)
        {
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
