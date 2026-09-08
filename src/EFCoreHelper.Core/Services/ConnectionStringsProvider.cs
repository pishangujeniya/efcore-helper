using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EFCoreHelper.Core.Services
{
    public class ConnectionStringsProvider : IConnectionStringsProvider
    {
        public IReadOnlyList<KeyValuePair<string, string>> GetConnectionStrings(string projectDirectory)
        {
            var results = new List<KeyValuePair<string, string>>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(projectDirectory) || !Directory.Exists(projectDirectory))
            {
                return results;
            }

            // Look for appsettings.json, appsettings.Development.json, etc.
            string[] searchFiles = new[]
            {
                "appsettings.Development.json",
                "appsettings.json",
                "appsettings.Local.json"
            };

            foreach (var fileName in searchFiles)
            {
                var fullPath = Path.Combine(projectDirectory, fileName);
                if (File.Exists(fullPath))
                {
                    ExtractConnectionStringsFromFile(fullPath, results, seenKeys);
                }
            }

            // Also check any other appsettings.*.json
            try
            {
                var otherFiles = Directory.GetFiles(projectDirectory, "appsettings.*.json", SearchOption.TopDirectoryOnly);
                foreach (var file in otherFiles)
                {
                    ExtractConnectionStringsFromFile(file, results, seenKeys);
                }
            }
            catch
            {
                // Directory access errors
            }

            return results;
        }

        private void ExtractConnectionStringsFromFile(string filePath, List<KeyValuePair<string, string>> results, HashSet<string> seenKeys)
        {
            try
            {
                string jsonContent = File.ReadAllText(filePath);
                using (var doc = JsonDocument.Parse(jsonContent, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }))
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            if (string.Equals(prop.Name, "ConnectionStrings", StringComparison.OrdinalIgnoreCase) &&
                                prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var connProp in prop.Value.EnumerateObject())
                                {
                                    if (connProp.Value.ValueKind == JsonValueKind.String)
                                    {
                                        var connValue = connProp.Value.GetString() ?? string.Empty;
                                        if (!seenKeys.Contains(connProp.Name))
                                        {
                                            seenKeys.Add(connProp.Name);
                                            results.Add(new KeyValuePair<string, string>(connProp.Name, connValue));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Silently skip malformed json files
            }
        }
    }
}
