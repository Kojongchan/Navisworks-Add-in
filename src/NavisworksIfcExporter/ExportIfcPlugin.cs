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
                        Mesh = mesh,
                        Color = mesh.Color
                    };

                    if (options.ExportProperties)
                    {
                        foreach (var category in PropertyExtractor.Extract(item))
                            element.Properties[category.Key] = category.Value;
                    }

                    // Prefer the Revit material attribute; fall back to a property guess.
                    element.MaterialName = mesh.MaterialName ?? FindMaterialName(element.Properties);

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
    }
}
