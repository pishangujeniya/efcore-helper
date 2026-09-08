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
    public partial class BundleMigrationsDialog : DialogWindow
    {
        private readonly IEfCliCommandBuilder _commandBuilder;
        private readonly IReadOnlyList<ProjectInfo> _projects;
        public BundleMigrationOptions? Options { get; private set; }

        public BundleMigrationsDialog(
            IReadOnlyList<ProjectInfo> projects,
            ProjectInfo? selectedProject = null,
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

                CmbRuntime.ItemsSource = new List<string> { "win-x64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64" };
                CmbRuntime.SelectedIndex = 0;

                UpdateContexts();
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

        private void OnOutputTextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();
        private void OnRuntimeTextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

        private void UpdatePreview()
        {
            if (TxtCommandPreview == null) return;
            try
            {
                var options = BuildOptions();
                TxtCommandPreview.Text = _commandBuilder.BuildBundleMigrationCommand(options);
            }
            catch
            {
                TxtCommandPreview.Text = "dotnet ef migrations bundle";
            }
        }

        private BundleMigrationOptions BuildOptions()
        {
            var project = CmbProject.SelectedItem as ProjectInfo;
            var startup = CmbStartupProject.SelectedItem as ProjectInfo;
            var context = CmbContext.SelectedItem as DbContextInfo;

            return new BundleMigrationOptions
            {
                Project = project?.FilePath,
                StartupProject = startup?.FilePath,
                Context = context?.Name,
                OutputFilePath = string.IsNullOrWhiteSpace(TxtOutputFileName.Text) ? null : TxtOutputFileName.Text.Trim(),
                TargetRuntime = string.IsNullOrWhiteSpace(CmbRuntime.Text) ? null : CmbRuntime.Text.Trim(),
                SelfContained = ChkSelfContained.IsChecked == true,
                Force = ChkForce.IsChecked == true
            };
        }

        private void OnBundleClicked(object sender, RoutedEventArgs e)
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
