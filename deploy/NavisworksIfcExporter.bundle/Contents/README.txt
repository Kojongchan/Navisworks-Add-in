This folder holds the add-in binaries at runtime. It is intentionally empty in
source control — the contents are produced by a build.

After building, the following must end up here (next to PackageContents.xml's
"./Contents/" reference):

    NavisworksIfcExporter.dll      <- the add-in
    Xbim.*.dll                     <- xBIM Toolkit dependencies
    (other NuGet dependency DLLs)

Do NOT put the Autodesk.Navisworks.*.dll files here; those ship with Navisworks.

The easiest way to populate this folder + zip the bundle for distribution is:

    pwsh build/pack-bundle.ps1

For local testing you can instead deploy straight to your user plugins folder:

    msbuild src/NavisworksIfcExporter.sln /p:Configuration=Release /p:Platform=x64 /p:DeployToNavisworks=true
