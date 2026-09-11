using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using EFCoreHelper.Core.Models;

namespace EFCoreHelper.Core.Services
{
    public class SolutionScanner : ISolutionScanner
    {
        private readonly IConnectionStringsProvider _connectionStringsProvider;

        private static readonly Regex DbContextClassRegex = new Regex(
            @"\b(?:public|internal)?\s*(?:sealed\s+)?(?:partial\s+)?class\s+([A-Za-z0-9_]+)(?:<[^>]+>)?(?:\([^\)]*\))?\s*:[^{;]*\b[A-Za-z0-9_]*DbContext\b",
            RegexOptions.Compiled);

        private static readonly Regex SlnProjectRegex = new Regex(
            @"Project\(""\{[A-Fa-f0-9\-]+\}""\)\s*=\s*""([^""]+)"",\s*""([^""]+\.csproj)""",
            RegexOptions.Compiled);

        public SolutionScanner(IConnectionStringsProvider? connectionStringsProvider = null)
        {
            _connectionStringsProvider = connectionStringsProvider ?? new ConnectionStringsProvider();
        }

        public async Task<IReadOnlyList<ProjectInfo>> ScanSolutionAsync(string solutionDirectoryOrPath, CancellationToken cancellationToken = default)
        {
            var projects = new List<ProjectInfo>();

            if (string.IsNullOrWhiteSpace(solutionDirectoryOrPath))
                return projects;

            var projectFiles = new List<string>();

            if (File.Exists(solutionDirectoryOrPath) && solutionDirectoryOrPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                var slnDir = Path.GetDirectoryName(solutionDirectoryOrPath) ?? string.Empty;
                try
                {
                    var doc = XDocument.Load(solutionDirectoryOrPath);
                    var projectPaths = doc.Descendants("Project")
                        .Select(p => p.Attribute("Path")?.Value)
                        .Where(p => !string.IsNullOrEmpty(p) && p!.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
                    foreach (var projRel in projectPaths)
                    {
                        var fullPath = Path.GetFullPath(Path.Combine(slnDir, projRel!.Replace('/', Path.DirectorySeparatorChar)));
                        if (File.Exists(fullPath) && !projectFiles.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
                        {
                            projectFiles.Add(fullPath);
                        }
                    }
                }
                catch
                {
                    // Fallback to directory scan
                }
            }
            else if (File.Exists(solutionDirectoryOrPath) && solutionDirectoryOrPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                var slnDir = Path.GetDirectoryName(solutionDirectoryOrPath) ?? string.Empty;
                var slnLines = File.ReadAllLines(solutionDirectoryOrPath);
                foreach (var line in slnLines)
                {
                    var match = SlnProjectRegex.Match(line);
                    if (match.Success)
                    {
                        var relPath = match.Groups[2].Value.Replace('\\', Path.DirectorySeparatorChar);
                        var fullPath = Path.GetFullPath(Path.Combine(slnDir, relPath));
                        if (File.Exists(fullPath) && !projectFiles.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
                        {
                            projectFiles.Add(fullPath);
                        }
                    }
                }
            }
            else
            {
                var dir = Directory.Exists(solutionDirectoryOrPath)
                    ? solutionDirectoryOrPath
                    : Path.GetDirectoryName(solutionDirectoryOrPath);

                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    projectFiles.AddRange(FindCsprojFiles(dir));
                }
            }

            foreach (var csproj in projectFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var projectInfo = await ScanProjectAsync(csproj, cancellationToken).ConfigureAwait(false);
                if (projectInfo != null)
                {
                    projects.Add(projectInfo);
                }
            }

            return projects;
        }

        public async Task<ProjectInfo?> ScanProjectAsync(string projectFilePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(projectFilePath) || !File.Exists(projectFilePath))
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    var doc = XDocument.Load(projectFilePath);
                    var projectInfo = new ProjectInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(projectFilePath),
                        FilePath = projectFilePath
                    };

                    // Detect Sdk attribute (e.g. Microsoft.NET.Sdk.Web)
                    var sdkAttr = doc.Root?.Attribute("Sdk")?.Value ?? string.Empty;
                    bool isWebSdk = sdkAttr.IndexOf("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) >= 0;

                    // Detect TargetFramework
                    var tfElement = doc.Descendants("TargetFramework").FirstOrDefault();
                    if (tfElement != null)
                    {
                        projectInfo.TargetFramework = tfElement.Value;
                    }
                    else
                    {
                        var tfsElement = doc.Descendants("TargetFrameworks").FirstOrDefault();
                        if (tfsElement != null)
                        {
                            projectInfo.TargetFramework = tfsElement.Value;
                        }
                    }

                    // Focus strictly on modern .NET / .NET Core projects (netcoreapp*, net5.0+, net6.0, net7.0, net8.0, net9.0, net10.0)
                    if (!IsModernDotNetProject(projectInfo.TargetFramework))
                    {
                        return null;
                    }

                    // Detect OutputType
                    var outputType = doc.Descendants("OutputType").FirstOrDefault()?.Value ?? string.Empty;
                    bool isExe = string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(outputType, "WinExe", StringComparison.OrdinalIgnoreCase);

                    projectInfo.IsStartupProject = isWebSdk || isExe;

                    // Check package references
                    var pkgRefs = doc.Descendants("PackageReference")
                        .Select(p => p.Attribute("Include")?.Value ?? p.Attribute("Update")?.Value ?? string.Empty)
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();

                    projectInfo.HasEfCoreReference = pkgRefs.Any(p => p.IndexOf("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) >= 0);
                    projectInfo.HasEfDesignReference = pkgRefs.Any(p => p.IndexOf("EntityFrameworkCore.Design", StringComparison.OrdinalIgnoreCase) >= 0);

                    // Scan DbContext classes in project directory
                    var projectDir = Path.GetDirectoryName(projectFilePath);
                    if (!string.IsNullOrEmpty(projectDir) && Directory.Exists(projectDir))
                    {
                        ScanForDbContexts(projectDir, projectInfo);
                        ScanForMigrations(projectDir, projectInfo);

                        // Load connection strings
                        var connections = _connectionStringsProvider.GetConnectionStrings(projectDir);
                        projectInfo.ConnectionStrings.AddRange(connections);
                    }

                    return projectInfo;
                }
                catch
                {
                    return null;
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        public static bool IsModernDotNetProject(string targetFramework)
        {
            if (string.IsNullOrWhiteSpace(targetFramework))
                return true; // Default to true if unstated SDK-style project

            var tfms = targetFramework.Split(';', ',');
            foreach (var tfm in tfms)
            {
                var t = tfm.Trim().ToLowerInvariant();

                // Accept modern .NET and .NET Core first
                if (t.StartsWith("netcoreapp") ||
                    t.StartsWith("net5.") || t.StartsWith("net6.") ||
                    t.StartsWith("net7.") || t.StartsWith("net8.") ||
                    t.StartsWith("net9.") || t.StartsWith("net10.") ||
                    t.StartsWith("netstandard"))
                {
                    return true;
                }

                // Explicitly reject legacy .NET Framework (net48, net472, net35, v4.8, etc.)
                if (t.StartsWith("net4") || t.StartsWith("net3") || t.StartsWith("net2") ||
                    t.StartsWith("v4.") || t.StartsWith("v3.") || t.StartsWith("v2."))
                {
                    continue;
                }
            }

            return false;
        }

        private static List<string> FindCsprojFiles(string rootDir)
        {
            var results = new List<string>();
            try
            {
                foreach (var file in Directory.GetFiles(rootDir, "*.csproj", SearchOption.TopDirectoryOnly))
                {
                    results.Add(file);
                }

                foreach (var dir in Directory.GetDirectories(rootDir))
                {
                    var dirName = Path.GetFileName(dir);
                    if (dirName.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Equals(".vs", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Equals(".idea", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    results.AddRange(FindCsprojFiles(dir));
                }
            }
            catch
            {
                // Access denied or folder deleted
            }

            return results;
        }

        private static void ScanForDbContexts(string projectDir, ProjectInfo projectInfo)
        {
            try
            {
                var csFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories);
                foreach (var file in csFiles)
                {
                    var rel = file.Substring(projectDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (rel.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                        rel.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                        rel.StartsWith("Migrations" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var content = File.ReadAllText(file);
                    foreach (Match match in DbContextClassRegex.Matches(content))
                    {
                        if (match.Success)
                        {
                            var contextName = match.Groups[1].Value;
                            if (!projectInfo.DbContexts.Any(c => c.Name == contextName))
                            {
                                projectInfo.DbContexts.Add(new DbContextInfo
                                {
                                    Name = contextName,
                                    FilePath = file,
                                    ProjectPath = projectInfo.FilePath
                                });
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore read errors
            }
        }

        private static void ScanForMigrations(string projectDir, ProjectInfo projectInfo)
        {
            try
            {
                var migrationsDir = Path.Combine(projectDir, "Migrations");
                if (Directory.Exists(migrationsDir))
                {
                    var files = Directory.GetFiles(migrationsDir, "*.cs", SearchOption.TopDirectoryOnly);
                    foreach (var file in files)
                    {
                        var fileName = Path.GetFileName(file);
                        if (fileName.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var migration = MigrationInfo.FromFileName(fileName, file, projectInfo.FilePath);
                        projectInfo.Migrations.Add(migration);
                    }

                    // Sort migrations descending: latest at the top, oldest at the bottom
                    projectInfo.Migrations.Sort((a, b) =>
                    {
                        if (a.CreatedDate.HasValue && b.CreatedDate.HasValue)
                            return b.CreatedDate.Value.CompareTo(a.CreatedDate.Value);
                        return string.Compare(b.MigrationId, a.MigrationId, StringComparison.OrdinalIgnoreCase);
                    });
                }
            }
            catch
            {
                // Ignore read errors
            }
        }
    }
}
