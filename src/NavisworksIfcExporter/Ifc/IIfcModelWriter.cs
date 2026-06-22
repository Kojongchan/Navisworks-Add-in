using System.Collections.Generic;
using NavisworksIfcExporter.Model;

namespace NavisworksIfcExporter.Ifc
{
    /// <summary>Writes a set of Navisworks elements to an IFC file on disk.</summary>
    public interface IIfcModelWriter
    {
        void Write(IReadOnlyList<NavisElement> elements, ExportOptions options);
    }

    public static class IfcWriterFactory
    {
        public static IIfcModelWriter Create(IfcSchemaTarget schema)
        {
            switch (schema)
            {
                case IfcSchemaTarget.Ifc2x3:
                    return new Ifc2x3ModelWriter();
                case IfcSchemaTarget.Ifc4:
                default:
                    return new Ifc4ModelWriter();
            }
        }
    }
}
