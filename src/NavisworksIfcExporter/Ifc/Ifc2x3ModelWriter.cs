using System.Collections.Generic;
using NavisworksIfcExporter.Model;

using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.IO;

using Xbim.Ifc2x3.Kernel;
using Xbim.Ifc2x3.ProductExtension;
using Xbim.Ifc2x3.SharedBldgElements;
using Xbim.Ifc2x3.GeometricModelResource;
using Xbim.Ifc2x3.TopologyResource;
using Xbim.Ifc2x3.GeometryResource;
using Xbim.Ifc2x3.GeometricConstraintResource;
using Xbim.Ifc2x3.RepresentationResource;
using Xbim.Ifc2x3.PropertyResource;
using Xbim.Ifc2x3.MeasureResource;
using Xbim.Ifc2x3.MaterialResource;
using Xbim.Ifc2x3.PresentationAppearanceResource;
using Xbim.Ifc2x3.PresentationResource; // IfcColourRgb

namespace NavisworksIfcExporter.Ifc
{
    /// <summary>
    /// Writes IFC2x3. IFC2x3 has no tessellation type, so geometry is emitted as an
    /// IfcFaceBasedSurfaceModel (one triangular IfcFace per mesh triangle). This does
    /// not require a closed manifold, which suits arbitrary Navisworks meshes.
    /// Coordinates are written in millimetres.
    /// </summary>
    public class Ifc2x3ModelWriter : IIfcModelWriter
    {
        public void Write(IReadOnlyList<NavisElement> elements, ExportOptions options)
        {
            double scale = options.UnitScaleToMetre * 1000.0;
            var credentials = IfcEditing.Credentials();

            using (var model = IfcStore.Create(credentials, XbimSchemaVersion.Ifc2X3, XbimStoreType.InMemoryModel))
            {
                using (var txn = model.BeginTransaction("Create IFC2x3 model"))
                {
                    var project = model.Instances.New<IfcProject>(p => p.Name = options.ProjectName);
                    project.Initialize(ProjectUnits.SIUnitsUK);

                    var context = GetModelContext(model);

                    var site = model.Instances.New<IfcSite>(s => s.Name = "Default Site");
                    var building = model.Instances.New<IfcBuilding>(b => b.Name = "Default Building");
                    var storey = model.Instances.New<IfcBuildingStorey>(s => s.Name = "Default Storey");

                    Aggregate(model, project, site);
                    Aggregate(model, site, building);
                    Aggregate(model, building, storey);

                    var contained = model.Instances.New<IfcRelContainedInSpatialStructure>(r =>
                    {
                        r.Name = "Building elements";
                        r.RelatingStructure = storey;
                    });

                    var materials = new Dictionary<string, IfcMaterial>();

                    foreach (var element in elements)
                    {
                        if (!element.HasGeometry)
                            continue;

                        IfcElement product = CreateElement(model, element.IfcType);
                        product.Name = element.Name;
                        product.ObjectPlacement = OriginPlacement(model);
                        product.Representation = BuildShape(model, context, element.Mesh, scale, element.Color);

                        contained.RelatedElements.Add(product);

                        if (options.ExportProperties)
                            AddProperties(model, product, element);

                        if (!string.IsNullOrWhiteSpace(element.MaterialName))
                            AssociateMaterial(model, product, element.MaterialName, materials);
                    }

                    txn.Commit();
                }

                model.SaveAs(options.OutputPath);
            }
        }

        private static IfcGeometricRepresentationContext GetModelContext(IfcStore model)
        {
            foreach (var ctx in model.Instances.OfType<IfcGeometricRepresentationContext>())
            {
                if (ctx.ContextType == "Model")
                    return ctx;
            }

            return model.Instances.New<IfcGeometricRepresentationContext>(c =>
            {
                c.ContextType = "Model";
                c.CoordinateSpaceDimension = 3;
                c.Precision = 1e-5;
                c.WorldCoordinateSystem = model.Instances.New<IfcAxis2Placement3D>(a =>
                    a.Location = model.Instances.New<IfcCartesianPoint>(p => p.SetXYZ(0, 0, 0)));
            });
        }

        private static IfcProductDefinitionShape BuildShape(
            IfcStore model, IfcGeometricRepresentationContext context, MeshGeometry mesh, double scale, double[] color)
        {
            int vertexCount = mesh.VertexCount;

            // Shared cartesian points reused across faces.
            var points = new IfcCartesianPoint[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                int idx = i;
                points[i] = model.Instances.New<IfcCartesianPoint>(p => p.SetXYZ(
                    mesh.Coordinates[idx * 3]     * scale,
                    mesh.Coordinates[idx * 3 + 1] * scale,
                    mesh.Coordinates[idx * 3 + 2] * scale));
            }

            var connected = model.Instances.New<IfcConnectedFaceSet>();

            int triCount = mesh.TriangleCount;
            for (int t = 0; t < triCount; t++)
            {
                int a = mesh.TriangleIndices[t * 3];
                int b = mesh.TriangleIndices[t * 3 + 1];
                int c = mesh.TriangleIndices[t * 3 + 2];

                var loop = model.Instances.New<IfcPolyLoop>(l =>
                {
                    l.Polygon.Add(points[a]);
                    l.Polygon.Add(points[b]);
                    l.Polygon.Add(points[c]);
                });

                var bound = model.Instances.New<IfcFaceOuterBound>(fb =>
                {
                    fb.Bound = loop;
                    fb.Orientation = true;
                });

                var face = model.Instances.New<IfcFace>(f => f.Bounds.Add(bound));
                connected.CfsFaces.Add(face);
            }

            var surfaceModel = model.Instances.New<IfcFaceBasedSurfaceModel>(m => m.FbsmFaces.Add(connected));

            if (color != null)
                ApplyColor(model, surfaceModel, color);

            var shapeRep = model.Instances.New<IfcShapeRepresentation>(r =>
            {
                r.ContextOfItems = context;
                r.RepresentationIdentifier = "Body";
                r.RepresentationType = "SurfaceModel";
                r.Items.Add(surfaceModel);
            });

            return model.Instances.New<IfcProductDefinitionShape>(s => s.Representations.Add(shapeRep));
        }

        // IFC2x3 wraps the surface style in an IfcPresentationStyleAssignment.
        private static void ApplyColor(IfcStore model, IfcGeometricRepresentationItem item, double[] rgba)
        {
            model.Instances.New<IfcStyledItem>(styled =>
            {
                styled.Item = item;
                styled.Styles.Add(model.Instances.New<IfcPresentationStyleAssignment>(assignment =>
                {
                    assignment.Styles.Add(model.Instances.New<IfcSurfaceStyle>(style =>
                    {
                        style.Side = IfcSurfaceSide.BOTH;
                        style.Styles.Add(model.Instances.New<IfcSurfaceStyleRendering>(rendering =>
                        {
                            rendering.SurfaceColour = model.Instances.New<IfcColourRgb>(c =>
                            {
                                c.Red = Clamp01(rgba[0]);
                                c.Green = Clamp01(rgba[1]);
                                c.Blue = Clamp01(rgba[2]);
                            });
                            // Transparency intentionally left unset (opaque). Navisworks
                            // often reports alpha = 0, which would make everything invisible.
                        }));
                    }));
                }));
            });
        }

        private static double Clamp01(double v)
        {
            if (v < 0.0) return 0.0;
            if (v > 1.0) return 1.0;
            return v;
        }

        private static void AssociateMaterial(
            IfcStore model, IfcObjectDefinition target, string name, Dictionary<string, IfcMaterial> cache)
        {
            if (!cache.TryGetValue(name, out var material))
            {
                material = model.Instances.New<IfcMaterial>(m => m.Name = name);
                cache[name] = material;
            }

            model.Instances.New<IfcRelAssociatesMaterial>(r =>
            {
                r.RelatingMaterial = material;
                r.RelatedObjects.Add(target);
            });
        }

        // Create the mapped IFC element. Footing/Pile live in a different IFC2x3
        // module, so they fall back to a proxy here.
        private static IfcElement CreateElement(IfcStore model, IfcMappedType type)
        {
            switch (type)
            {
                case IfcMappedType.Wall:     return model.Instances.New<IfcWall>();
                case IfcMappedType.Slab:     return model.Instances.New<IfcSlab>();
                case IfcMappedType.Beam:     return model.Instances.New<IfcBeam>();
                case IfcMappedType.Column:   return model.Instances.New<IfcColumn>();
                case IfcMappedType.Member:   return model.Instances.New<IfcMember>();
                case IfcMappedType.Plate:    return model.Instances.New<IfcPlate>();
                case IfcMappedType.Railing:  return model.Instances.New<IfcRailing>();
                case IfcMappedType.Covering: return model.Instances.New<IfcCovering>();
                case IfcMappedType.Roof:     return model.Instances.New<IfcRoof>();
                case IfcMappedType.Stair:    return model.Instances.New<IfcStair>();
                case IfcMappedType.Door:     return model.Instances.New<IfcDoor>();
                case IfcMappedType.Window:   return model.Instances.New<IfcWindow>();
                default:                     return model.Instances.New<IfcBuildingElementProxy>();
            }
        }

        private static IfcLocalPlacement OriginPlacement(IfcStore model)
        {
            return model.Instances.New<IfcLocalPlacement>(lp =>
                lp.RelativePlacement = model.Instances.New<IfcAxis2Placement3D>(a =>
                    a.Location = model.Instances.New<IfcCartesianPoint>(p => p.SetXYZ(0, 0, 0))));
        }

        private static void Aggregate(IfcStore model, IfcObjectDefinition parent, IfcObjectDefinition child)
        {
            model.Instances.New<IfcRelAggregates>(r =>
            {
                r.RelatingObject = parent;
                r.RelatedObjects.Add(child);
            });
        }

        private static void AddProperties(IfcStore model, IfcObject target, NavisElement element)
        {
            foreach (var category in element.Properties)
            {
                if (category.Value.Count == 0)
                    continue;

                var pset = model.Instances.New<IfcPropertySet>(ps =>
                {
                    ps.Name = category.Key;
                    foreach (var prop in category.Value)
                    {
                        ps.HasProperties.Add(model.Instances.New<IfcPropertySingleValue>(p =>
                        {
                            p.Name = prop.Key;
                            p.NominalValue = new IfcText(prop.Value ?? string.Empty);
                        }));
                    }
                });

                model.Instances.New<IfcRelDefinesByProperties>(r =>
                {
                    r.RelatingPropertyDefinition = pset;
                    r.RelatedObjects.Add(target);
                });
            }
        }
    }
}
