# Navisworks IFC Exporter (Add-in)

Navisworks Manage/Simulate has built-in export to DWF/DWFx, FBX and Google
Earth KML — but **no IFC export**. This project is a custom .NET add-in that
adds **Export to IFC** by reading the model's geometry and properties through
the Navisworks API and writing them out with the [xBIM Toolkit](https://github.com/xBimTeam).

> This is exactly how the commercial "IFC Exporter" add-ins on the Autodesk App
> Store work under the hood. There's no hidden IFC engine inside Navisworks; you
> extract the tessellated geometry + properties yourself and assemble the IFC.

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

## Deploy / run

Navisworks loads add-ins from a versioned Plugins folder. Copy the built output
into a folder **named after the plugin id segment**:

```
%PROGRAMDATA%\Autodesk\Navisworks Manage 2022\Plugins\NavisworksIfcExporter\
    NavisworksIfcExporter.dll
    <all xBIM*.dll and dependencies from bin\x64\Release>
```

(Or under `%APPDATA%\Autodesk Navisworks Manage 2022\Plugins\...`.)

Then launch Navisworks → ribbon **Tool Add-ins 1** → **Export IFC**.

A quick post-build copy step you can add to the `.csproj`:

```xml
<Target Name="DeployPlugin" AfterTargets="Build">
  <PropertyGroup>
    <PluginDir>$(ProgramData)\Autodesk\Navisworks Manage 2022\Plugins\NavisworksIfcExporter</PluginDir>
  </PropertyGroup>
  <ItemGroup>
    <Deploy Include="$(TargetDir)**\*.*" />
  </ItemGroup>
  <Copy SourceFiles="@(Deploy)" DestinationFolder="$(PluginDir)\%(RecursiveDir)" />
</Target>
```

## Roadmap ideas

- Semantic mapping (proxy → `IfcWall`/`IfcSlab`/… by Navisworks category/layer).
- Reuse identical meshes via `IfcMappedItem` to shrink output.
- Progress bar + cancellation for large models.
- Options dialog (units, which property categories to include, georeferencing).
