using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace EFCoreHelper.Vsix.ToolWindows
{
    [Guid(PackageGuids.ToolWindowGuidString)]
    public class EfCoreToolWindow : ToolWindowPane
    {
        static EfCoreToolWindow()
        {
            EFCoreHelperPackage.EnsureAssemblyResolver();
        }

        public EfCoreToolWindow() : base(null)
        {
            Caption = "Entity Framework Core";
            Content = new EfCoreToolWindowControl();
        }
    }
}
