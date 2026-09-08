using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using EFCoreHelper.Core.Models;
using EFCoreHelper.Core.Services;
using Microsoft.VisualStudio.PlatformUI;

namespace EFCoreHelper.Vsix.Dialogs
{
    public partial class OptimizeDbContextDialog : DialogWindow
    {
        private readonly IEfCliCommandBuilder _commandBuilder;
        private readonly IReadOnlyList<ProjectInfo> _projects;
        public OptimizeDbContextOptions? Options { get; private set; }

        public OptimizeDbContextDialog(
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
                TxtCommandPreview.Text = _commandBuilder.BuildOptimizeDbContextCommand(options);
            }
            catch
            {
                TxtCommandPreview.Text = "dotnet ef dbcontext optimize";
            }
        }

        private OptimizeDbContextOptions BuildOptions()
        {
            var project = CmbProject.SelectedItem as ProjectInfo;
            var context = CmbContext.SelectedItem as DbContextInfo;

            return new OptimizeDbContextOptions
            {
                Project = project?.FilePath,
                Context = context?.Name,
                OutputDir = string.IsNullOrWhiteSpace(TxtOutputDir.Text) ? null : TxtOutputDir.Text.Trim(),
                Namespace = string.IsNullOrWhiteSpace(TxtNamespace.Text) ? null : TxtNamespace.Text.Trim(),
                ScaffoldModel = ChkScaffoldModel.IsChecked == true
            };
        }

        private void OnOptimizeClicked(object sender, RoutedEventArgs e)
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
