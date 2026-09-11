using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using EFCoreHelper.Core.Models;
using EFCoreHelper.Core.Services;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.Win32;

namespace EFCoreHelper.Vsix.Dialogs
{
    public partial class ScriptMigrationDialog : DialogWindow
    {
        private readonly IEfCliCommandBuilder _commandBuilder;
        private readonly IReadOnlyList<ProjectInfo> _projects;
        public ScriptMigrationOptions? Options { get; private set; }

        public ScriptMigrationDialog(
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
                UpdatePreview();
            };
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

            var migrations = new List<string> { "0 (Beginning of time)" };
            if (currentProject?.Migrations != null)
            {
                foreach (var m in currentProject.Migrations)
                {
                    migrations.Add(m.Name);
                }
            }

            CmbFromMigration.ItemsSource = migrations;
            CmbFromMigration.SelectedIndex = 0;

            var toMigrations = new List<string> { "<Latest>" };
            if (currentProject?.Migrations != null)
            {
                foreach (var m in currentProject.Migrations)
                {
                    toMigrations.Add(m.Name);
                }
            }
            CmbToMigration.ItemsSource = toMigrations;
            CmbToMigration.SelectedIndex = 0;
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

        private void OnFromTextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();
        private void OnToTextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();
        private void OnOutputTextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

        private void OnBrowseClicked(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "SQL Script (*.sql)|*.sql|All Files (*.*)|*.*",
                DefaultExt = ".sql",
                FileName = "migration_script.sql"
            };

            if (dlg.ShowDialog() == true)
            {
                TxtOutputPath.Text = dlg.FileName;
            }
        }

        private void UpdatePreview()
        {
            if (TxtCommandPreview == null) return;
            try
            {
                var options = BuildOptions();
                TxtCommandPreview.Text = _commandBuilder.BuildScriptMigrationCommand(options);
            }
            catch
            {
                TxtCommandPreview.Text = "dotnet ef migrations script";
            }
        }

        private ScriptMigrationOptions BuildOptions()
        {
            var project = CmbProject.SelectedItem as ProjectInfo;
            var startup = CmbStartupProject.SelectedItem as ProjectInfo;
            var context = CmbContext.SelectedItem as DbContextInfo;

            string? from = CmbFromMigration.Text.Trim();
            if (from.StartsWith("0", StringComparison.OrdinalIgnoreCase))
            {
                from = null;
            }

            string? to = CmbToMigration.Text.Trim();
            if (string.Equals(to, "<Latest>", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(to))
            {
                to = null;
            }

            return new ScriptMigrationOptions
            {
                Project = project?.FilePath,
                StartupProject = startup?.FilePath,
                Context = context?.Name,
                FromMigration = from,
                ToMigration = to,
                OutputFilePath = string.IsNullOrWhiteSpace(TxtOutputPath.Text) ? null : TxtOutputPath.Text.Trim(),
                Idempotent = ChkIdempotent.IsChecked == true,
                NoTransactions = ChkNoTransactions.IsChecked == true
            };
        }

        private void OnGenerateClicked(object sender, RoutedEventArgs e)
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
