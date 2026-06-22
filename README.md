# Navisworks IFC Exporter (Add-in)

Navisworks Manage/Simulate has built-in export to DWF/DWFx, FBX and Google
Earth KML — but **no IFC export**. This project is a custom .NET add-in that
adds **Export to IFC** by reading the model's geometry and properties through
the Navisworks API and writing them out with the [xBIM Toolkit](https://github.com/xBimTeam).

> This is exactly how the commercial "IFC Exporter" add-ins on the Autodesk App
> Store work under the hood. There's no hidden IFC engine inside Navisworks; you
> extract the tessellated geometry + properties yourself and assemble the IFC.

## ⚡ Quick start (Windows)

> **There is no prebuilt `setup.exe` in this repo.** The installer must be
> compiled on a Windows PC that has Navisworks installed (the add-in links
> against Navisworks' own DLLs, which only exist on that machine). It's a
> build *output*, so it isn't committed to git. Two double-click batch files
> make this painless — clone/download the repo, then:

| I want to… | Double-click | Needs |
|---|---|---|
| **Just use it on my PC** | `install-for-me.bat` | Navisworks + VS 2022 / .NET SDK |
| **Make a `setup.exe` to share** | `make-installer.bat` | the above **+ [Inno Setup 6](https://jrsoftware.org/isdl.php)** |

`make-installer.bat` writes the shareable installer to
`artifacts\NavisworksIfcExporter-Setup-0.1.0.exe`.

Different Navisworks version? Pass its folder, e.g.
`make-installer.bat -NavisworksDir "C:\Program Files\Autodesk\Navisworks Manage 2024"`.

## What it does

- Adds an **Export IFC** command under *Tool Add-ins*.
- Exports the current selection, or the whole scene if nothing is selected.
- Writes **IFC4** (`IfcTriangulatedFaceSet`) or **IFC2x3** (`IfcFaceBasedSurfaceModel`),
  chosen via the Save dialog's file-type dropdown.
- Maps every geometry item to an `IfcBuildingElementProxy` under a default
  Project ▸ Site ▸ Building ▸ Storey spatial structure.
- Copies Navisworks property categories into `IfcPropertySet`s.

## Honest limitations (read this)

Navisworks only stores **tessellated (triangle-mesh) geometry** internally — the
original parametric/BREP solids from the source CAD are already gone by the time
the model is in Navisworks. So:

- Exported shapes are **triangle meshes**, not clean solids/sweeps. File sizes
  can be large for detailed models (especially IFC2x3).
- Everything is an `IfcBuildingElementProxy`. Semantic classification
  (wall → `IfcWall`, etc.) would need a separate mapping layer — a good next step.
- Geometry comes through the COM API; the local-to-world matrix handling and
  vertex VARIANT-array reads in `Geometry/CallbackGeomListener.cs` are the parts
  most worth verifying first on your real models.

## Project layout

```
src/
  NavisworksIfcExporter.sln
  NavisworksIfcExporter/
    NavisworksIfcExporter.csproj      # net48, x64, references Navisworks + xBIM
    ExportIfcPlugin.cs                # AddInPlugin entry point + UI
    Model/ExportModels.cs            # ExportOptions, NavisElement, MeshGeometry
    Geometry/
      CallbackGeomListener.cs        # COM primitive callback -> dedup mesh (world coords)
      GeometryExtractor.cs           # ModelItem -> COM fragments -> mesh
    Props/PropertyExtractor.cs       # ModelItem properties -> dictionaries
    Units/UnitsHelper.cs             # Navisworks units -> metres factor
    Ifc/
      IIfcModelWriter.cs             # writer interface + factory
      IfcEditing.cs                  # xBIM editor credentials
      Ifc4ModelWriter.cs             # IFC4 (Tessellation)
      Ifc2x3ModelWriter.cs           # IFC2x3 (SurfaceModel)
```

## Build (Windows + Visual Studio 2022)

Navisworks is Windows-only and references its installed DLLs, so this **cannot be
built on Linux/CI without the Navisworks DLLs present.**

1. Install **Navisworks Manage/Simulate 2022+** (you need the `.NET API` feature).
2. Open `src/NavisworksIfcExporter.sln` in Visual Studio 2022.
3. If your install path differs from the default, set `NavisworksDir` — either
   edit it in the `.csproj` or build with:
   ```
   msbuild src/NavisworksIfcExporter.sln /p:Configuration=Release /p:NavisworksDir="C:\Program Files\Autodesk\Navisworks Manage 2022"
   ```
4. Restore NuGet packages (xBIM) and build (x64).

### Version notes

- Target framework is **.NET Framework 4.8** (matches Navisworks 2022+).
- The three Navisworks references use `CopyLocal = false` — they ship with
  Navisworks; don't deploy them.
- `Xbim.Essentials` is pinned to `5.1.341`. If you bump the major version, a few
  xBIM API calls in the writers (point-list / face-set construction) may need
  small adjustments.

## Packaging & distribution (Autodesk Application Bundle)

This add-in ships as an **Autodesk Application Bundle** — the same install format
Autodesk App Store add-ins use. A bundle is a folder named `*.bundle` containing
a `PackageContents.xml` manifest plus a `Contents/` folder with the binaries.
Drop the bundle into an `ApplicationPlugins` directory and Navisworks
auto-discovers it (no per-version Plugins folder needed).

```
NavisworksIfcExporter.bundle/
  PackageContents.xml          # manifest (deploy/NavisworksIfcExporter.bundle/)
  Contents/
    NavisworksIfcExporter.dll  # the add-in
    Xbim.*.dll                 # xBIM dependencies (and other NuGet deps)
```

The manifest declares the supported products/versions:
`Platform="NAVMAN|NAVSIM"` (Manage/Simulate), `SeriesMin="Nw19"` (= 2022),
`SeriesMax="Nw99"`. Version codes: **Nw19=2022, Nw20=2023, Nw21=2024, Nw22=2025**.
Pin `SeriesMax` to the newest release you've actually tested.

### Build the distributable bundle

```powershell
pwsh build/pack-bundle.ps1
```

Produces:
- `artifacts/NavisworksIfcExporter.bundle/` — the installable bundle
- `artifacts/NavisworksIfcExporter-bundle.zip` — for distribution / App Store

> The Navisworks API DLLs are intentionally **excluded** from the bundle — they
> ship with Navisworks and must not be redistributed.

### Build a double-click installer (recommended for end users)

Most users find a `.exe` installer far easier than a zip. An [Inno Setup](https://jrsoftware.org/isdl.php)
script builds one that installs the bundle for all users and adds a Windows
uninstall entry.

```powershell
# Requires Inno Setup 6 installed
pwsh build/build-installer.ps1
```

Produces `artifacts/NavisworksIfcExporter-Setup-<version>.exe`. Users just
double-click it (it prompts for admin, warns if Navisworks is running, and
installs to `%PROGRAMDATA%\Autodesk\ApplicationPlugins\`). Uninstall from
**Settings → Apps**.

The installer layout lives in `installer/NavisworksIfcExporter.iss`. Publisher is
set to **CHAN**; the icon and wizard banner come from `installer/assets/`.

### Branding assets

The icon and installer banner are generated from a script so they're
reproducible (no binary editing):

```
python3 assets/generate_assets.py
```

This (re)writes:
- `installer/assets/appicon.ico` — setup + uninstall icon
- `installer/assets/wizard_large.bmp`, `wizard_small.bmp` — installer banners
- `deploy/NavisworksIfcExporter.bundle/Contents/Resources/` — the icon shown in
  the Autodesk App Store / plugin manager (referenced by `PackageContents.xml`)

Edit colors/text near the top of `assets/generate_assets.py` to restyle.

### Code signing (publisher: CHAN)

Signing is automated and optional — pass a certificate to the build and both the
add-in DLL and the `setup.exe` get signed (SHA-256 + timestamp).

```powershell
# One-time: create a self-signed "CHAN" certificate (for testing / managed networks)
pwsh build/new-signing-cert.ps1 -Password "<strong-password>"

# Build a signed installer
pwsh build/build-installer.ps1 -SignPfx installer\CHAN-codesign.pfx -SignPassword "<strong-password>"
```

You can also sign by certificate store thumbprint: `-SignThumbprint <hash>`.

> **Self-signed vs. real certificate:** a self-signed CHAN certificate makes the
> signature consistent and tamper-evident, but Windows still shows "Unknown
> publisher" on *other* machines unless the public `CHAN-codesign.cer` is trusted
> there (e.g. deployed via Group Policy on a managed network). For public
> distribution (App Store / internet), buy an **OV or EV code-signing
> certificate** and point `-SignPfx` at it — no script changes needed. The
> `.pfx` is git-ignored; never commit it.

### Install from the bundle/zip manually (alternative)

If you'd rather not use the installer, copy `NavisworksIfcExporter.bundle` into
one of:
- All users:    `%PROGRAMDATA%\Autodesk\ApplicationPlugins\`
- Current user: `%APPDATA%\Autodesk\ApplicationPlugins\`

Then launch Navisworks → ribbon **Tool Add-ins** → **Export IFC**.

### Install locally while developing

```
msbuild src/NavisworksIfcExporter.sln /p:Configuration=Release /p:Platform=x64 /p:DeployToNavisworks=true
```

This copies the bundle straight into your user `ApplicationPlugins` folder.

### Publishing to the Autodesk App Store (so anyone can install)

1. Register as a publisher at the [Autodesk App Store](https://apps.autodesk.com/).
2. **Code-sign** with a real CA certificate (see "Code signing" above) — App
   Store review expects signing, and a self-signed cert won't be trusted.
3. Upload the bundle zip from `artifacts/`, confirm `PackageContents.xml`
   metadata (Name, Author=CHAN, CompanyDetails, ProductCode/UpgradeCode),
   add screenshots and a description, then submit for review.
4. Keep `UpgradeCode` stable across releases; bump `ProductCode` and
   `AppVersion` each release.

## Roadmap ideas

- Semantic mapping (proxy → `IfcWall`/`IfcSlab`/… by Navisworks category/layer).
- Reuse identical meshes via `IfcMappedItem` to shrink output.
- Progress bar + cancellation for large models.
- Options dialog (units, which property categories to include, georeferencing).
