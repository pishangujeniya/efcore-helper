# Release & Marketplace Publishing Guide

This guide outlines how releases are prepared using **GitHub Actions** and manually uploaded to the [Visual Studio Marketplace](https://marketplace.visualstudio.com/).

---

## 🎯 Release Flow Summary

```mermaid
flowchart LR
    A["1. Bump Version & Update CHANGELOG"] --> B["2. Push Tag (e.g. v1.0.6)"]
    B --> C["3. GitHub Actions Builds VSIX & Creates Release"]
    D --> E["5. Manually Upload to VS Marketplace Portal"]
    C --> D["4. Download VSIX from GitHub Release"]
```

1. **GitHub Actions**: Builds the extension, runs all unit tests, packages the VSIX container, extracts version release notes from `CHANGELOG.md`, and creates a GitHub Release with downloadable `.vsix` assets.
2. **Visual Studio Marketplace**: You deliberately upload the verified `.vsix` package through the Marketplace Management Portal.
3. **No Marketplace Secrets Required**: Because publishing is manual, no Azure DevOps Personal Access Tokens (`VS_MARKETPLACE_PAT`) are needed in repository secrets.

---

## 📋 Pre-Release Checklist

Before creating a new release:

1. **Bump Version Number**:
   - In [`Directory.Build.props`](Directory.Build.props):
     ```xml
     <VersionPrefix>1.0.6</VersionPrefix>
     ```
   - In [`src/EFCoreHelper.Vsix/source.extension.vsixmanifest`](src/EFCoreHelper.Vsix/source.extension.vsixmanifest):
     ```xml
     <Identity Id="EFCoreHelper.f3f6c8d7-7d9a-4e2b-9e4a-9b1b7a2d4e8f" Version="1.0.6" Language="en-US" Publisher="Pishang Ujeniya" />
     ```

2. **Update CHANGELOG.md**:
   - Ensure a section exists for the new version in [`CHANGELOG.md`](CHANGELOG.md) following Keep a Changelog formatting:
     ```markdown
     ## [1.0.6] - 2026-09-11

     ### Added
     - New feature description...

     ### Fixed
     - Bug fix description...
     ```
   > [!TIP]
   > The GitHub Actions workflow will automatically parse this exact section from `CHANGELOG.md` and populate the GitHub Release notes body!

3. **Commit & Push Changes**:
   ```bash
   git add .
   git commit -m "chore: prepare release v1.0.6"
   git push origin main
   ```

---

## 🚀 Step 1: Trigger GitHub Release & Asset Generation

You can trigger release preparation via either **Git Tag** or the **GitHub Actions UI**.

### Option A: Via Git Tag (Recommended)
Tag your release commit and push the tag to GitHub:
```bash
git tag v1.0.6
git push origin v1.0.6
```
*The `release.yml` workflow will start automatically.*

### Option B: Via GitHub Actions UI (`workflow_dispatch`)
1. Open your repository on GitHub: `https://github.com/pishangujeniya/efcore-helper`.
2. Go to the **Actions** tab.
3. In the left workflow list, select **Release & Prepare Assets**.
4. Click **Run workflow** dropdown on the right:
   - **Branch**: `main`
   - **Version string to release**: e.g. `1.0.6` (leave empty to infer from manifest)
   - Click **Run workflow**.

---

## 📦 Step 2: Download the VSIX Package

Once the workflow finishes (typically ~2 minutes):

1. Go to **Releases** (`https://github.com/pishangujeniya/efcore-helper/releases`).
2. Open the newly published release (e.g. `EF Core Helper v1.0.6`).
3. Under **Assets**, download:
   - `EFCoreHelper.vsix` (or `EFCoreHelper-v1.0.6.vsix`).

> [!NOTE]
> Alternatively, if you built the package locally using `.\build.ps1 -Configuration Release -Version 1.0.6`, the ready-to-upload VSIX is located at `artifacts/EFCoreHelper.vsix`.

---

## 🌐 Step 3: Manually Upload to Visual Studio Marketplace

1. Visit the [Visual Studio Marketplace Management Portal](https://marketplace.visualstudio.com/manage).
2. Sign in with your Microsoft Account.
3. Under **Publishers**, click your publisher name (e.g. `pishangujeniya`).

### Updating an Existing Extension:
1. Locate **EF Core Helper** in the extensions table.
2. Click the **More actions** button (`...`) next to the extension.
3. Select **Edit** (or **Update**).
4. Click or drag-and-drop your downloaded `EFCoreHelper.vsix` file into the upload zone.
5. Review the extension metadata, description, and version number.
6. Click **Save & Publish** (or **Upload**).

### First-Time Upload (if not yet created on Marketplace):
1. Click **New extension** in the upper-right corner.
2. Select **Visual Studio**.
3. Drag-and-drop `EFCoreHelper.vsix` into the dialog.
4. Verify the extension ID and publisher ID match the manifest.
5. Click **Upload**.

---

## ⏳ Step 4: Verification & Public Availability

- Immediately after uploading, the extension status will display as **Verifying**.
- Visual Studio Marketplace runs automated malware, digital signature, and manifest checks.
- Verification typically takes **2 to 5 minutes**.
- Once verified, the status changes to **Public** &mdash; your new version is now live and available directly from Visual Studio's **Extensions > Manage Extensions** dialog worldwide!

---

## 💻 Local Build & Packaging (Optional)

If you prefer building and packaging the VSIX locally on your machine without running GitHub Actions:

```powershell
# Restore, run tests, compile, and package VSIX
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release -Version 1.0.5
```

The output package will be produced at:
```text
artifacts\EFCoreHelper.vsix
```

You can test-install it locally with:
```powershell
Start-Process "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\VSIXInstaller.exe" -ArgumentList "artifacts\EFCoreHelper.vsix"
```
