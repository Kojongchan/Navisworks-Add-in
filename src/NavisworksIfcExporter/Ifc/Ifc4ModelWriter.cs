using System.Collections.Generic;
using NavisworksIfcExporter.Model;

using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.IO;

using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.ProductExtension;
using Xbim.Ifc4.SharedBldgElements;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.GeometricConstraintResource;
using Xbim.Ifc4.RepresentationResource;
using Xbim.Ifc4.PropertyResource;
using Xbim.Ifc4.MeasureResource;

namespace NavisworksIfcExporter.Ifc
{
    /// <summary>
    /// Writes IFC4. Geometry is emitted as IfcTriangulatedFaceSet (the efficient,
    /// modern tessellation type). Coordinates are written in millimetres.
    /// </summary>
    public class Ifc4ModelWriter : IIfcModelWriter
    {
        public void Write(IReadOnlyList<NavisElement> elements, ExportOptions options)
        {
            // Navisworks units -> metres (option) -> millimetres (IFC declared unit).
            double scale = options.UnitScaleToMetre * 1000.0;

            var credentials = IfcEditing.Credentials();

            using (var model = IfcStore.Create(credentials, XbimSchemaVersion.Ifc4, XbimStoreType.InMemoryModel))
            {
                using (var txn = model.BeginTransaction("Create IFC4 model"))
                {
                    var project = model.Instances.New<IfcProject>(p => p.Name = options.ProjectName);
                    // Sets up owner history, units (mm) and a "Model" representation context.
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

                    foreach (var element in elements)
                    {
                        if (!element.HasGeometry)
                            continue;

                        var proxy = model.Instances.New<IfcBuildingElementProxy>(e =>
                        {
                            e.Name = element.Name;
                            e.ObjectPlacement = OriginPlacement(model);
                            e.Representation = BuildShape(model, context, element.Mesh, scale);
                        });

                        contained.RelatedObjects.Add(proxy);

                        if (options.ExportProperties)
                            AddProperties(model, proxy, element);
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

            // Fallback: create one if Initialize didn't.
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
            IfcStore model, IfcGeometricRepresentationContext context, MeshGeometry mesh, double scale)
        {
            int vertexCount = mesh.VertexCount;
            int triCount = mesh.TriangleCount;

            var coordList = model.Instances.New<IfcCartesianPointList3D>(list =>
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    list.CoordList.GetAt(i).AddRange(new List<IfcLengthMeasure>
                    {
                        mesh.Coordinates[i * 3]     * scale,
                        mesh.Coordinates[i * 3 + 1] * scale,
                        mesh.Coordinates[i * 3 + 2] * scale
                    });
                }
            });

            var faceSet = model.Instances.New<IfcTriangulatedFaceSet>(fs =>
            {
                fs.Closed = false;
                fs.Coordinates = coordList;
                for (int i = 0; i < triCount; i++)
                {
                    // IFC indices are 1-based.
                    fs.CoordIndex.GetAt(i).AddRange(new List<IfcPositiveInteger>
                    {
                        mesh.TriangleIndices[i * 3]     + 1,
                        mesh.TriangleIndices[i * 3 + 1] + 1,
                        mesh.TriangleIndices[i * 3 + 2] + 1
                    });
                }
            });

            var shapeRep = model.Instances.New<IfcShapeRepresentation>(r =>
            {
                r.ContextOfItems = context;
                r.RepresentationIdentifier = "Body";
                r.RepresentationType = "Tessellation";
                r.Items.Add(faceSet);
            });

            return model.Instances.New<IfcProductDefinitionShape>(s => s.Representations.Add(shapeRep));
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

        private static void AddProperties(IfcStore model, IfcBuildingElementProxy proxy, NavisElement element)
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
                    r.RelatedObjects.Add(proxy);
                });
            }
        }
    }
}
