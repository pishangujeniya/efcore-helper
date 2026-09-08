using System.Collections.Generic;

namespace EFCoreHelper.Core.Services
{
    public interface IConnectionStringsProvider
    {
        IReadOnlyList<KeyValuePair<string, string>> GetConnectionStrings(string projectDirectory);
    }
}
