# Contributing to EF Core Helper

Thank you for your interest in contributing to **EF Core Helper for Visual Studio**! 🎉

Our mission is to provide an intuitive, first-class visual Entity Framework Core management experience natively inside Visual Studio for modern .NET Core projects.

---

## 🛠️ Prerequisites

To build and debug the extension locally, you will need:

1. **Visual Studio 2022** (17.0 or newer, Community / Professional / Enterprise)
   - Workload: *.NET desktop development*
   - Workload: *Visual Studio extension development*
2. **.NET SDK** (8.0, 9.0, or 10.0+)
3. **`dotnet-ef` Global Tool**:
   ```bash
   dotnet tool install --global dotnet-ef
   ```

---

## 🏗️ Repository Architecture

The solution is divided into modular, cleanly separated layers:

- **`src/EFCoreHelper.Core`**:
  - Pure, portable .NET class library (`netstandard2.0` / `net8.0` / `net10.0`).
  - Contains command line generators (`EfCliCommandBuilder`), asynchronous process runner (`EfCliRunner`), solution scanner (`SolutionScanner`), and configuration/connection string parsers.
  - Independent of Visual Studio APIs for maximum testability.
- **`src/EFCoreHelper.Vsix`**:
  - The Visual Studio Extension package (`net48`).
  - Contains the `AsyncPackage`, `ToolWindowPane`, VS Command Table (`VSCommandTable.vsct`), and WPF dialogs with live CLI preview.
  - Fully compliant with Visual Studio Theming (`VsBrushes`).
- **`tests/EFCoreHelper.Core.Tests`**:
  - xUnit test suite for command argument generation, quoting, solution scanning, and JSON parsing.
- **`samples/ContosoUniversity`**:
  - Sample ASP.NET Core project with EF Core SQLite models and migrations used for integration testing and demo purposes.

---

## 🚀 Building & Testing Locally

You can run the automated build and test pipeline with a single command:

```powershell
# Run unit tests, build all projects, and package the VSIX:
.\build.ps1
```

To run just the unit tests:
```bash
dotnet test
```

The resulting `.vsix` installer is generated in:
```
artifacts/EFCoreHelper.vsix
```

---

## 🐞 Debugging in Visual Studio

1. Open `EFCoreHelper.sln` in Visual Studio 2022.
2. Set `EFCoreHelper.Vsix` as the **Startup Project**.
3. In the project debug properties, verify that:
   - **Start external program**: points to your `devenv.exe` (e.g. `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe`).
   - **Command line arguments**: `/rootsuffix Exp` (runs an isolated Experimental instance of Visual Studio without touching your main settings).
4. Press **F5**. Visual Studio will launch an Experimental Instance with EF Core Helper loaded!
5. In the experimental instance, open `samples/ContosoUniversity/ContosoUniversity.csproj` or any EF Core project, and open the tool window via **Tools > EF Core Helper > EF Core Helper Tool Window** or press `Ctrl+Alt+E, M`.

---

## 📐 Code Guidelines

- Adhere to the `.editorconfig` settings in the root directory.
- Ensure all automated unit tests pass before opening a PR.
- Use `ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync()` when accessing Visual Studio COM/DTE objects, and perform all CLI and I/O executions asynchronously on background threads.
- When creating or modifying WPF UI elements, always use Visual Studio shell dynamic brushes (`VsBrushes`) to ensure dark and light theme consistency.

---

## 📬 Pull Request Process

1. Fork the repository and create your feature branch:
   ```bash
   git checkout -b feature/my-new-feature
   ```
2. Commit your changes with clear commit messages:
   ```bash
   git commit -am "feat: add support for compiled model optimize options"
   ```
3. Push to your fork:
   ```bash
   git push origin feature/my-new-feature
   ```
4. Open a Pull Request on GitHub against `main`. Fill out the PR template with details about what was changed and tested.

We welcome feedback, suggestions, and PRs from developers of all experience levels!
