using System;
using System.Collections.Generic;

namespace NavisworksIfcExporter.Model
{
    /// <summary>Target IFC schema for the export.</summary>
    public enum IfcSchemaTarget
    {
        Ifc4,
        Ifc2x3
    }

    /// <summary>IFC element type an item is exported as (mapped from Revit category).</summary>
    public enum IfcMappedType
    {
        Proxy,
        Wall,
        Slab,
        Beam,
        Column,
        Footing,
        Pile,
        Member,
        Plate,
        Railing,
        Covering,
        Roof,
        Stair,
        Door,
        Window
    }

    /// <summary>User-controlled options for a single export run.</summary>
    public class ExportOptions
    {
        public IfcSchemaTarget Schema { get; set; } = IfcSchemaTarget.Ifc4;

        public string OutputPath { get; set; }

        public string ProjectName { get; set; } = "Navisworks Export";

        /// <summary>
        /// Factor to convert Navisworks model units into metres (IFC works in metres).
        /// Computed from the active document units; see <see cref="Units.UnitsHelper"/>.
        /// </summary>
        public double UnitScaleToMetre { get; set; } = 1.0;

        /// <summary>Whether to copy Navisworks properties into IfcPropertySets.</summary>
        public bool ExportProperties { get; set; } = true;
    }

    /// <summary>
    /// A triangulated mesh in world coordinates (Navisworks model units, pre unit-scaling).
    /// Vertices are de-duplicated; indices are 0-based triples.
    /// </summary>
    public class MeshGeometry
    {
        /// <summary>Flat list of coordinates: x0,y0,z0, x1,y1,z1, ...</summary>
        public List<double> Coordinates { get; } = new List<double>();

        /// <summary>Triangle vertex indices (0-based), grouped in threes.</summary>
        public List<int> TriangleIndices { get; } = new List<int>();

        /// <summary>Representative display colour as RGBA (0..1), or null if unknown.</summary>
        public double[] Color { get; set; }

        /// <summary>Material name read from the Revit material attribute, or null.</summary>
        public string MaterialName { get; set; }

        /// <summary>Revit category (e.g. "구조 기초"), used to map the IFC type. Null if unknown.</summary>
        public string Category { get; set; }

        public int VertexCount => Coordinates.Count / 3;
        public int TriangleCount => TriangleIndices.Count / 3;
    }

    /// <summary>
    /// One Navisworks geometry item mapped to one IFC element.
    /// </summary>
    public class NavisElement
    {
        /// <summary>Stable IFC GUID for this element.</summary>
        public Guid Guid { get; } = Guid.NewGuid();

        public string Name { get; set; } = "Item";

        /// <summary>Navisworks class name (e.g. "Solid", "Geometry").</summary>
        public string ClassName { get; set; }

        /// <summary>Display colour as RGBA (0..1), or null if unknown.</summary>
        public double[] Color { get; set; }

        /// <summary>Material name if one could be found in the properties, else null.</summary>
        public string MaterialName { get; set; }

        /// <summary>IFC element type this item is exported as.</summary>
        public IfcMappedType IfcType { get; set; } = IfcMappedType.Proxy;

        public MeshGeometry Mesh { get; set; }

        /// <summary>category name -> (property name -> value as string).</summary>
        public Dictionary<string, Dictionary<string, string>> Properties { get; }
            = new Dictionary<string, Dictionary<string, string>>();

        public bool HasGeometry => Mesh != null && Mesh.TriangleCount > 0;
    }
}
