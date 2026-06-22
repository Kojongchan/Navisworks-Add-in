using Xbim.Ifc;

namespace NavisworksIfcExporter.Ifc
{
    /// <summary>Editor credentials stamped into the IFC owner history.</summary>
    internal static class IfcEditing
    {
        public static XbimEditorCredentials Credentials()
        {
            return new XbimEditorCredentials
            {
                ApplicationDevelopersName = "NavisworksIfcExporter",
                ApplicationFullName = "Navisworks IFC Exporter Add-in",
                ApplicationIdentifier = "NavisworksIfcExporter",
                ApplicationVersion = "0.1.0",
                EditorsFamilyName = "Navisworks",
                EditorsGivenName = "User",
                EditorsOrganisationName = "Unknown"
            };
        }
    }
}
