using System;
using System.Text.RegularExpressions;

namespace EFCoreHelper.Core.Models
{
    public class MigrationInfo
    {
        private static readonly Regex MigrationIdRegex = new Regex(@"^(\d{14})_(.+)$", RegexOptions.Compiled);

        public string MigrationId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime? CreatedDate { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string ProjectPath { get; set; } = string.Empty;
        public bool IsApplied { get; set; }

        public static MigrationInfo FromFileName(string fileName, string filePath, string projectPath)
        {
            var cleanName = System.IO.Path.GetFileNameWithoutExtension(fileName);
            // Ignore Designer.cs files
            if (cleanName.EndsWith(".Designer", StringComparison.OrdinalIgnoreCase))
            {
                cleanName = cleanName.Substring(0, cleanName.Length - ".Designer".Length);
            }

            var match = MigrationIdRegex.Match(cleanName);
            if (match.Success)
            {
                var timestamp = match.Groups[1].Value;
                var migrationName = match.Groups[2].Value;

                DateTime? created = null;
                if (DateTime.TryParseExact(timestamp, "yyyyMMddHHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt))
                {
                    created = dt;
                }

                return new MigrationInfo
                {
                    MigrationId = cleanName,
                    Name = migrationName,
                    CreatedDate = created,
                    FilePath = filePath,
                    ProjectPath = projectPath
                };
            }

            return new MigrationInfo
            {
                MigrationId = cleanName,
                Name = cleanName,
                FilePath = filePath,
                ProjectPath = projectPath
            };
        }

        public override string ToString() => Name;
    }
}
