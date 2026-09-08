using System.Collections.Generic;
using System.IO;

namespace EFCoreHelper.Core.Models
{
    public class ProjectInfo
    {
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string DirectoryPath => string.IsNullOrEmpty(FilePath) ? string.Empty : (Path.GetDirectoryName(FilePath) ?? string.Empty);
        public string TargetFramework { get; set; } = string.Empty;
        public bool IsStartupProject { get; set; }
        public bool HasEfCoreReference { get; set; }
        public bool HasEfDesignReference { get; set; }

        public List<DbContextInfo> DbContexts { get; set; } = new List<DbContextInfo>();
        public List<MigrationInfo> Migrations { get; set; } = new List<MigrationInfo>();
        public List<KeyValuePair<string, string>> ConnectionStrings { get; set; } = new List<KeyValuePair<string, string>>();

        public override string ToString() => Name;
    }
}
