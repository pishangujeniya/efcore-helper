using System;

namespace EFCoreHelper.Vsix
{
    public static class PackageGuids
    {
        public const string PackageGuidString = "e7498c19-7561-419a-9e1e-2fb0d5a37f26";
        public static readonly Guid PackageGuid = new Guid(PackageGuidString);

        public const string CommandSetGuidString = "a29f8c12-8874-4b55-bfbb-41808605c03f";
        public static readonly Guid CommandSetGuid = new Guid(CommandSetGuidString);

        public const string ToolWindowGuidString = "c56b7c93-5182-45e0-9bc8-812e9b8b0e71";
        public static readonly Guid ToolWindowGuid = new Guid(ToolWindowGuidString);
    }

    public static class PackageIds
    {
        public const int cmdidOpenToolWindow = 0x0100;
        public const int cmdidAddMigration = 0x0101;
        public const int cmdidUpdateDatabase = 0x0102;
        public const int cmdidRemoveMigration = 0x0103;
        public const int cmdidScriptMigration = 0x0104;
        public const int cmdidBundleMigrations = 0x0105;
        public const int cmdidScaffoldDbContext = 0x0106;
        public const int cmdidDropDatabase = 0x0107;
        public const int cmdidOptimizeDbContext = 0x0108;

        public const int cmdidProjectAddMigration = 0x0201;
        public const int cmdidProjectUpdateDatabase = 0x0202;
        public const int cmdidProjectScaffold = 0x0203;
        public const int cmdidProjectOpenToolWindow = 0x0204;
    }
}
