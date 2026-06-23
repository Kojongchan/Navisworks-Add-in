using NavisworksIfcExporter.Model;
using Autodesk.Navisworks.Api;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComBridge = Autodesk.Navisworks.Api.ComApi.ComApiBridge;

namespace NavisworksIfcExporter.Geometry
{
    /// <summary>
    /// Extracts a triangulated mesh for a single Navisworks ModelItem.
    ///
    /// The managed API does not expose vertex data, so we cross to the COM API:
    ///   ModelItem -> COM selection -> InwOaPath -> InwOaFragment3
    /// and generate "simple primitives" (triangles), captured by CallbackGeomListener.
    ///
    /// We process one item at a time. That keeps path.Fragments() scoped to just
    /// this item's fragments, avoiding the double-counting you get when iterating
    /// fragments under a parent path.
    /// </summary>
    public static class GeometryExtractor
    {
        public static MeshGeometry Extract(ModelItem item)
        {
            if (item == null || !item.HasGeometry)
                return null;

            var collection = new ModelItemCollection { item };
            ComApi.InwOpSelection comSelection = ComBridge.ToInwOpSelection(collection);

            var listener = new CallbackGeomListener();
            string materialName = null;

            foreach (ComApi.InwOaPath path in comSelection.Paths())
            {
                if (materialName == null)
                    materialName = ColorExtractor.TryGetMaterialName(path);

                foreach (ComApi.InwOaFragment3 fragment in path.Fragments())
                {
                    listener.SetMatrix(fragment.GetLocalToWorldMatrix());
                    fragment.GenerateSimplePrimitives(
                        ComApi.nwEVertexProperty.eNORMAL | ComApi.nwEVertexProperty.eCOLOR, listener);
                }
            }

            MeshGeometry mesh = listener.ToMesh();
            if (mesh != null)
                mesh.MaterialName = materialName;

            return mesh;
        }
    }
}
