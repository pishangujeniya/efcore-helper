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
    public partial class ScaffoldDbContextDialog : DialogWindow
    {
        private readonly IEfCliCommandBuilder _commandBuilder;
        private readonly IReadOnlyList<ProjectInfo> _projects;
        public ScaffoldDbContextOptions? Options { get; private set; }

        public ScaffoldDbContextDialog(
            IReadOnlyList<ProjectInfo> projects,
            ProjectInfo? selectedProject = null,
            IEfCliCommandBuilder? commandBuilder = null)
        {
            InitializeComponent();
            _commandBuilder = commandBuilder ?? new EfCliCommandBuilder();
            _projects = projects ?? new List<ProjectInfo>();

            Loaded += (s, e) =>
            {
                PopulateProviders();
                PopulateProjects(selectedProject);
                UpdatePreview();
            };
        }

        private void PopulateProviders()
        {
            CmbProvider.ItemsSource = DatabaseProvider.WellKnownProviders;
            CmbProvider.DisplayMemberPath = "Name";
            if (DatabaseProvider.WellKnownProviders.Count > 0)
            {
                CmbProvider.SelectedIndex = 0;
            }
        }

        private void PopulateProjects(ProjectInfo? selectedProject)
        {
            CmbProject.ItemsSource = _projects;
            CmbProject.DisplayMemberPath = "Name";

            if (selectedProject != null)
            {
                CmbProject.SelectedItem = selectedProject;
            }
            else if (_projects.Count > 0)
            {
                CmbProject.SelectedIndex = 0;
            }

            UpdateConnectionStrings();
        }

        private void UpdateConnectionStrings()
        {
            var project = CmbProject.SelectedItem as ProjectInfo;
            var connStrings = new List<string>();

            if (project?.ConnectionStrings != null)
            {
                foreach (var conn in project.ConnectionStrings)
                {
                    connStrings.Add(conn.Value);
                }
            }

            CmbConnectionString.ItemsSource = connStrings;
            if (connStrings.Count > 0)
            {
                CmbConnectionString.SelectedIndex = 0;
            }
            else
            {
                var provider = CmbProvider.SelectedItem as DatabaseProvider;
                if (provider != null)
                {
                    CmbConnectionString.Text = provider.DefaultConnectionStringTemplate;
                }
            }
        }

        private void OnProviderChanged(object sender, SelectionChangedEventArgs e)
        {
            var provider = CmbProvider.SelectedItem as DatabaseProvider;
            if (provider != null && string.IsNullOrEmpty(CmbConnectionString.Text))
            {
                CmbConnectionString.Text = provider.DefaultConnectionStringTemplate;
            }
            UpdatePreview();
        }

        private void OnFieldChanged(object sender, RoutedEventArgs e)
        {
            if (sender == CmbProject)
            {
                UpdateConnectionStrings();
            }
            UpdatePreview();
        }

        private void OnConnectionTextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

        private void UpdatePreview()
        {
            if (TxtCommandPreview == null || CmbProvider == null || CmbConnectionString == null)
                return;

            try
            {
                var options = BuildOptions();
                if (string.IsNullOrWhiteSpace(options.ConnectionString) || string.IsNullOrWhiteSpace(options.Provider))
                {
                    TxtCommandPreview.Text = "dotnet ef dbcontext scaffold <connection> <provider>";
                    BtnScaffold.IsEnabled = false;
                    return;
                }

                TxtCommandPreview.Text = _commandBuilder.BuildScaffoldDbContextCommand(options);
                BtnScaffold.IsEnabled = true;
            }
            catch
            {
                TxtCommandPreview.Text = "dotnet ef dbcontext scaffold <connection> <provider>";
                BtnScaffold.IsEnabled = false;
            }
        }

        private ScaffoldDbContextOptions BuildOptions()
        {
            var project = CmbProject.SelectedItem as ProjectInfo;
            var providerObj = CmbProvider.SelectedItem as DatabaseProvider;
            string providerName = providerObj?.PackageName ?? CmbProvider.Text.Trim();

            var tables = new List<string>();
            if (!string.IsNullOrWhiteSpace(TxtTables.Text))
            {
                tables.AddRange(TxtTables.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()));
            }

            var schemas = new List<string>();
            if (!string.IsNullOrWhiteSpace(TxtSchemas.Text))
            {
                schemas.AddRange(TxtSchemas.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
            }

            return new ScaffoldDbContextOptions
            {
                ConnectionString = CmbConnectionString.Text.Trim(),
                Provider = providerName,
                Project = project?.FilePath,
                ContextName = string.IsNullOrWhiteSpace(TxtContextName.Text) ? null : TxtContextName.Text.Trim(),
                OutputDir = string.IsNullOrWhiteSpace(TxtOutputDir.Text) ? null : TxtOutputDir.Text.Trim(),
                ContextDir = string.IsNullOrWhiteSpace(TxtContextDir.Text) ? null : TxtContextDir.Text.Trim(),
                Tables = tables,
                Schemas = schemas,
                DataAnnotations = ChkDataAnnotations.IsChecked == true,
                UseDatabaseNames = ChkDatabaseNames.IsChecked == true,
                NoPluralize = ChkNoPluralize.IsChecked == true,
                Force = ChkForce.IsChecked == true
            };
        }

        private void OnScaffoldClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CmbConnectionString.Text))
            {
                MessageBox.Show("Please enter a connection string.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
