using System;

namespace EFCoreHelper.Core.Models
{
    public class DbContextInfo
    {
        public string Name { get; set; } = string.Empty;
        public string FullTypeName { get; set; } = string.Empty;
        public string ProjectPath { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;

        public string ProjectName => !string.IsNullOrEmpty(ProjectPath) ? System.IO.Path.GetFileNameWithoutExtension(ProjectPath) : string.Empty;
        public string DisplayLabel => !string.IsNullOrEmpty(ProjectName) ? $"{Name} ({ProjectName})" : Name;

        public override string ToString() => Name;
    }
}
