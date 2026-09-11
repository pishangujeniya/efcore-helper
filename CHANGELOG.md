# Changelog

All notable changes to **EF Core Helper for Visual Studio** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.7] - 2026-09-12

### Added
- **Generate SQL Script Default Export to Startup Project Root**:
  - `ScriptMigrationDialog` automatically defaults the output script path to `script.sql` in the root of the selected startup project (`<StartupProjectRoot>\script.sql`).
  - Real-time command preview instantly reflects the `--output` argument with the default or custom script path.
  - Dynamically updates the target directory when changing startup projects while preserving custom filenames.
  - The "Browse..." save dialog pre-initializes to the startup project root folder with filename `script.sql`.
  - Automatically opens the generated `script.sql` document directly in the Visual Studio code editor on completion.

### Fixed
- **Startup Project Selection Across All Dialogs**:
  - Added Startup Project dropdown selector to dialogs that previously lacked it (`RemoveMigrationDialog`, `ScriptMigrationDialog`, `ScaffoldDbContextDialog`, `OptimizeDbContextDialog`).
  - Preselects the startup project chosen in the EF Core Tool Window toolbar across all 8 modal dialogs.
  - Automatically queries and defaults to the solution's active startup project configured in Visual Studio via `VsSolutionService`.
  - Ensures `--startup-project` is reliably populated in generated command previews and command executions across all actions.

### Changed
- **Visual Studio Menu Rebranding & Icon Integration**:
  - Rebranded top-level Tools menu to **Tools > EF Core Helper > EF Core Helper Tool Window**.
  - Rebranded Solution Explorer project context menu to **EF Core Helper > Open EF Core Helper**.
  - Updated tool window dock caption to **EF Core Helper**.
  - Integrated dedicated 16x16 icon (`Icon16.png`) for VSCT command tables so the extension icon displays cleanly in Visual Studio menus.

## [1.0.6] - 2026-09-11

### Added
- **Global `dotnet-ef` Tool Installation & Auto-Update**:
  - Automatically detects whether the `dotnet-ef` CLI global tool is installed and checks NuGet for newer tool releases (non-blocking with quick timeout).
  - One-click **Install dotnet-ef** banner and status bar action if the tool is missing from the developer's environment.
  - One-click **Update dotnet-ef** action toolbar button and status bar indicator when an update is available, streaming live CLI execution directly into the Visual Studio Execution Console.
- **Latest-First Migration Ordering**: Migrations are now sorted descending by creation timestamp by default across the migrations list and all target migration dropdowns (newest migrations at the top, oldest at the bottom).
- **Native Vector Migration Glyphs**: Added crisp database migration vector icons to migration list items that cleanly adapt color and opacity to selection and theme states.
- **Empty-State Placeholder**: Added an informative, theme-aware empty state overlay in the tool window when no migrations exist in the selected project.
- **Themed Tool Window Tabs**: Created native Visual Studio `TabControl` and `TabItem` control templates utilizing `ToolWindowTabSelectedTabKey`, `ToolWindowTabSelectedTextKey`, and active bottom border indicators, replacing default Windows Aero tabs.
- **Themed Context Menus**: Created native `ContextMenu` and `MenuItem` styles using `CommandBarMenuBackgroundGradientKey` and `CommandBarHoverOverSelectedKey`, ensuring right-click menus match Dark and Light themes.

### Fixed
- **Dark Mode Migration List Contrast**: Replaced default WPF Aero `ListBoxItem` container template with a dedicated VS shell template. Resolved issue where inactive selected items displayed blinding `#D9D9D9` light-gray backgrounds and illegible text in Visual Studio Dark Mode.
- **Dynamic Selection & Hover Brushes**: All list items now dynamically bind to `HighlightKey` / `HighlightTextKey` when active and `CommandBarHoverOverSelectedKey` / `CommandBarBorderKey` when hovered or inactive.
- **Themed ScrollViewers Across All Dialogs**: Applied Visual Studio's `VsResourceKeys.ScrollViewerStyleKey` to all modal dialog scroll viewers and text areas, ensuring scrollbars render in dark mode when the Dark theme is active.
- **Execution Console Selection Theming**: Configured explicit dynamic `CaretBrush` and `SelectionBrush` for the streaming execution console TextBox.

## [1.0.5] - 2026-09-08

### Fixed
- **Marketplace Package Compliance**: Fixed Visual Studio Marketplace upload validation failures where container parts (`.dll`, `.pkgdef`, `LICENSE.txt`, `Icon.png`) were rejected as not listed in the package manifest.
- **Explicit Manifest Asset Declarations**: Fully declared all package components, MEF components, and referenced runtime assemblies (`EFCoreHelper.Core.dll`, `Community.VisualStudio.Toolkit.dll`, `System.Text.Json.dll`, `System.Text.Encodings.Web.dll`, `Microsoft.Bcl.AsyncInterfaces.dll`) directly in `extension.vsixmanifest` `<Assets>`.
- **VSIX v3 Manifest Generation**: Modernized the packaging pipeline in `build.ps1` to supply a structured files manifest to `VsixUtil`, producing fully compliant `manifest.json` and `catalog.json` descriptors registering all container parts.
- **Automated Package Verification**: Added post-packaging Open Packaging Conventions (OPC) and `manifest.json` integrity validation to `build.ps1` to prevent incomplete VSIX uploads.

## [1.0.4] - 2026-09-08

### Added
- **Custom Native DbContext Dropdown Template**: Items display the DbContext name in bold accompanied by a subtle `(ProjectName)` badge for easy identification across multi-project solutions.
- **Dynamic Project Synchronization**: Selecting a DbContext belonging to a different project automatically synchronizes the Target Project dropdown to that DbContext's owner project.
- **Solution-Wide Context Fallback**: If the currently selected project defines no DbContexts, the context selector automatically displays all DbContexts available across the entire solution.

### Changed
- **Intelligent Target Project Selection**: When opening any dialog or the tool window, EF Core Helper now automatically detects and selects the project containing DbContexts/migrations instead of defaulting blindly to the first project in the solution.

### Fixed
- **Expanded DbContext Detection Scanner**: Regex scanner now reliably recognizes C# 12 primary constructors (`class AppDbContext(...) : DbContext(...)`), generic identity base classes (`IdentityDbContext<TUser>`), multiple interface inheritance, and classes spanning multiple lines.
- **Multi-Context Detection**: Scanner now detects multiple DbContext definitions within a single source file.
- **WPF ComboBox Binding Conflict**: Removed conflicting programmatic `DisplayMemberPath` assignments that caused blank item rendering or `InvalidOperationException` in WPF dialogs.

## [1.0.3] - 2026-09-07

### Added
- **Full Native Visual Studio Styling**: Replaced all custom color themes and emoji icons with standard Visual Studio iconography, typography, and dynamic shell brush theming (`VsBrushes`, `VsResourceKeys`).
- **`Microsoft.VisualStudio.PlatformUI.DialogWindow` Integration**: All 8 modal dialogs now inherit native Visual Studio dialog chrome, centered ownership, standard 23px input heights, and standard Enter/Esc button bindings.
- **Native Dialog Footers**: Added unified footers with a "Copy Command" button for fast CLI debugging alongside primary action and Cancel buttons.
- **Theme Adaptability**: 100% dynamic adaptation to Visual Studio Light, Dark, Blue, and High Contrast color themes.

### Fixed
- Removed amateur phone emojis from tool window toolbars and menus for a clean, built-in Visual Studio aesthetic.

## [1.0.2] - 2026-09-07

### Added
- **Visual Studio 2026 & 2022 Support**: Updated installation target range in `source.extension.vsixmanifest` to `[17.0, 19.0)` supporting both VS 2022 and VS 2026.
- **Dynamic Solution Context Autoloading**: Automatically activates extension package upon opening solutions via `UICONTEXT_SolutionExists` and `UICONTEXT_SolutionHasMultipleProjects`.

### Fixed
- Fixed command routing and context menu activation when right-clicking projects in Solution Explorer.
- Resolved "Exception has been thrown by the target of an invocation" when initializing tool windows.

## [1.0.1] - 2026-09-06

### Fixed
- Fixed VSIX installer error ("the package does not contain software license agreement that is mentioned in the manifest") by embedding `LICENSE.txt` into the root of the VSIX package.

## [1.0.0] - 2026-09-06

### Added
- **Interactive EF Core Tool Window**:
  - Solution explorer-style project, startup project, and DbContext selectors.
  - Interactive toolbar with 1-click access to all migration and database operations.
  - Dedicated Migrations explorer tab showing migration history with right-click context actions.
  - Real-time Execution Console with live streaming output, color-coded status, elapsed time, copy, and cancellation controls.
- **Dedicated Interactive Dialogs with Live CLI Preview**:
  - **Add Migration**: Migration name validation, output directory, custom namespace, `--no-build`, `--verbose`, and live command preview.
  - **Update Database**: Target migration selector (`<Latest>`, `0`, or specific migration), connection string override dropdown, and live command preview.
  - **Remove Migration**: Confirmation dialog with force option (`--force`).
  - **Generate SQL Script**: From/To migration selectors, idempotent mode (`--idempotent`), and file browser.
  - **Bundle Migrations**: Executable generator with target runtime (RID), self-contained flag, and force overwrite.
  - **Scaffold DbContext (Database First)**: Multi-provider support (SQL Server, PostgreSQL, MySQL, SQLite, Oracle, Cosmos), table & schema filters, annotations, and reverse engineering wizard.
  - **Drop Database**: Critical safety prompt and dry-run mode.
  - **Optimize DbContext**: EF Core compiled model generation for high-performance cold startup.
- **Tooling Diagnostics**:
  - Automatically checks `dotnet` CLI and `dotnet-ef` global tool installation with 1-click install action.
  - Warns if `Microsoft.EntityFrameworkCore.Design` is missing from target projects.
- **Visual Studio Integration**:
  - Tools Menu: `Tools > Entity Framework Core`.
  - Solution Explorer Project Context Menu: Right-click project > `EF Core`.
  - Output Window integration with dedicated `Entity Framework Core` pane.
  - Full theme compliance with Visual Studio Dark, Light, and Blue themes.
  - Non-blocking async execution using `AsyncPackage` and `JoinableTaskFactory`.
