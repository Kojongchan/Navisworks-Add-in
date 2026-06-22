using Autodesk.Navisworks.Api;

namespace NavisworksIfcExporter.Units
{
    /// <summary>
    /// Converts Navisworks document units into a "metres per unit" factor, because
    /// the IFC files we write declare their length unit as METRE.
    /// </summary>
    public static class UnitsHelper
    {
        public static double MetresPerUnit(Autodesk.Navisworks.Api.Units units)
        {
            switch (units)
            {
                case Autodesk.Navisworks.Api.Units.Meters:      return 1.0;
                case Autodesk.Navisworks.Api.Units.Centimeters: return 0.01;
                case Autodesk.Navisworks.Api.Units.Millimeters: return 0.001;
                case Autodesk.Navisworks.Api.Units.Kilometers:  return 1000.0;
                case Autodesk.Navisworks.Api.Units.Feet:        return 0.3048;
                case Autodesk.Navisworks.Api.Units.Inches:      return 0.0254;
                case Autodesk.Navisworks.Api.Units.Yards:       return 0.9144;
                case Autodesk.Navisworks.Api.Units.Miles:       return 1609.344;
                case Autodesk.Navisworks.Api.Units.Micrometers: return 0.000001;
                case Autodesk.Navisworks.Api.Units.Mils:        return 0.0000254;
                case Autodesk.Navisworks.Api.Units.Microinches: return 0.0000000254;
                default:                                        return 1.0;
            }
        }
    }
}
