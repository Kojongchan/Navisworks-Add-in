using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;

using NavisworksIfcExporter.Geometry;
using NavisworksIfcExporter.Ifc;
using NavisworksIfcExporter.Model;
using NavisworksIfcExporter.Props;
using NavisworksIfcExporter.Units;

namespace NavisworksIfcExporter
{
    /// <summary>
    /// Add-in entry point. Appears under Tool Add-ins. Exports the current
    /// selection (or the whole scene if nothing is selected) to an IFC file.
    /// </summary>
    [Plugin(
        "NavisworksIfcExporter.ExportIfc",   // unique plugin id
        "NWIFC",                              // developer id (4 chars)
        DisplayName = "Export IFC",
        ToolTip = "Export the current model to an IFC file")]
    public class ExportIfcPlugin : AddInPlugin
    {
        // Navisworks loads the plugin DLL from its folder, but does NOT probe that
        // folder for the plugin's dependencies (xBIM, etc.). Without this, the
        // plugin loads (button appears) but throws FileNotFoundException for
        // 'Xbim.Ifc' at export time. Resolve dependencies from our own folder.
        static ExportIfcPlugin()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                string folder = Path.GetDirectoryName(typeof(ExportIfcPlugin).Assembly.Location);
                if (string.IsNullOrEmpty(folder))
                    return null;

                string file = new AssemblyName(args.Name).Name + ".dll";
                string fullPath = Path.Combine(folder, file);
                return File.Exists(fullPath) ? Assembly.LoadFrom(fullPath) : null;
            }
            catch
            {
                return null;
            }
        }

        public override int Execute(params string[] parameters)
        {
            Document doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc == null || doc.IsClear)
            {
                MessageBox.Show("No model is open.", "Export IFC",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            ExportOptions options = AskForOptions(doc);
            if (options == null)
                return 0; // cancelled

            try
            {
                List<NavisElement> elements = CollectElements(doc, options);
                if (elements.Count == 0)
                {
                    MessageBox.Show("No geometry was found to export.", "Export IFC",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                IIfcModelWriter writer = IfcWriterFactory.Create(options.Schema);
                writer.Write(elements, options);

                MessageBox.Show(
                    $"Exported {elements.Count} element(s) to:\n{options.OutputPath}",
                    "Export IFC", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Export failed:\n" + ex, "Export IFC",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return 0;
        }

        private static ExportOptions AskForOptions(Document doc)
        {
            using (var dialog = new SaveFileDialog
            {
                Title = "Export IFC",
                Filter = "IFC4 (*.ifc)|*.ifc|IFC2x3 (*.ifc)|*.ifc",
                FilterIndex = 1,
                AddExtension = true,
                DefaultExt = "ifc",
                FileName = "export.ifc"
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                    return null;

                return new ExportOptions
                {
                    OutputPath = dialog.FileName,
                    // FilterIndex is 1-based: 1 => IFC4, 2 => IFC2x3
                    Schema = dialog.FilterIndex == 2 ? IfcSchemaTarget.Ifc2x3 : IfcSchemaTarget.Ifc4,
                    UnitScaleToMetre = UnitsHelper.MetresPerUnit(doc.Units),
                    ProjectName = string.IsNullOrEmpty(doc.FileName)
                        ? "Navisworks Export"
                        : System.IO.Path.GetFileNameWithoutExtension(doc.FileName)
                };
            }
        }

        private static List<NavisElement> CollectElements(Document doc, ExportOptions options)
        {
            var roots = new ModelItemCollection();

            ModelItemCollection selected = doc.CurrentSelection.SelectedItems;
            if (selected != null && selected.Count > 0)
            {
                roots.AddRange(selected);
            }
            else
            {
                foreach (Autodesk.Navisworks.Api.Model model in doc.Models)
                    roots.Add(model.RootItem);
            }

            var elements = new List<NavisElement>();
            foreach (ModelItem root in roots)
            {
                foreach (ModelItem item in root.DescendantsAndSelf)
                {
                    if (!item.HasGeometry)
                        continue;

                    MeshGeometry mesh = GeometryExtractor.Extract(item);
                    if (mesh == null || mesh.TriangleCount == 0)
                        continue;

                    var element = new NavisElement
                    {
                        Name = string.IsNullOrEmpty(item.DisplayName) ? "Item" : item.DisplayName,
                        ClassName = item.ClassName,
                        Mesh = mesh
                    };

                    if (options.ExportProperties)
                    {
                        foreach (var category in PropertyExtractor.Extract(item))
                            element.Properties[category.Key] = category.Value;
                    }

                    // Prefer the Revit material attribute; fall back to a property guess.
                    element.MaterialName = mesh.MaterialName ?? FindMaterialName(element.Properties);

                    // Auto colour by material: same material name -> same colour.
                    element.Color = element.MaterialName != null
                        ? ColorForMaterial(element.MaterialName)
                        : null;

                    // Auto-map the Revit category to an IFC element type.
                    element.IfcType = MapCategoryToIfcType(mesh.Category);

                    elements.Add(element);
                }
            }

            return elements;
        }

        // Best-effort: find a material name among the item's properties (e.g. a
        // property named "Material" / "재료"). Returns null if none found.
        private static string FindMaterialName(Dictionary<string, Dictionary<string, string>> properties)
        {
            foreach (var category in properties)
            {
                foreach (var prop in category.Value)
                {
                    bool looksLikeMaterial =
                        prop.Key.IndexOf("material", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        prop.Key.IndexOf("재료", StringComparison.Ordinal) >= 0;

                    if (looksLikeMaterial && !string.IsNullOrWhiteSpace(prop.Value))
                        return prop.Value.Trim();
                }
            }
            return null;
        }

        // Map a Revit category name (Korean or English) to an IFC element type,
        // emulating Codemill's object mapping but automatically.
        private static IfcMappedType MapCategoryToIfcType(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return IfcMappedType.Proxy;

            string c = category.ToLowerInvariant();

            bool Has(params string[] keys)
            {
                foreach (var k in keys)
                    if (c.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                return false;
            }

            // Order matters: check more specific terms first.
            if (Has("구조 기초", "기초", "footing", "foundation")) return IfcMappedType.Footing;
            if (Has("말뚝", "파일", "pile")) return IfcMappedType.Pile;
            if (Has("구조 기둥", "기둥", "column")) return IfcMappedType.Column;
            if (Has("구조 프레임", "프레임", "거더", "beam", "framing", "girder")) return IfcMappedType.Beam;
            if (Has("벽", "wall")) return IfcMappedType.Wall;
            if (Has("바닥", "슬래브", "floor", "slab")) return IfcMappedType.Slab;
            if (Has("지붕", "roof")) return IfcMappedType.Roof;
            if (Has("계단", "stair")) return IfcMappedType.Stair;
            if (Has("난간", "railing")) return IfcMappedType.Railing;
            if (Has("천장", "ceiling", "covering")) return IfcMappedType.Covering;
            if (Has("문", "door")) return IfcMappedType.Door;
            if (Has("창", "window")) return IfcMappedType.Window;
            if (Has("부재", "member")) return IfcMappedType.Member;
            if (Has("plate")) return IfcMappedType.Plate;

            return IfcMappedType.Proxy;
        }

        // Deterministic colour from a material name (same name -> same colour).
        // FNV-1a hash -> hue, then HSV with pleasant saturation/value.
        private static double[] ColorForMaterial(string name)
        {
            uint hash = 2166136261;
            foreach (char ch in name)
            {
                hash ^= ch;
                hash *= 16777619;
            }
            double hue = hash % 360u;
            return HsvToRgb(hue, 0.55, 0.85);
        }

        private static double[] HsvToRgb(double h, double s, double v)
        {
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = v - c;
            double r = 0, g = 0, b = 0;

            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }

            return new[] { r + m, g + m, b + m, 1.0 };
        }
    }
}
