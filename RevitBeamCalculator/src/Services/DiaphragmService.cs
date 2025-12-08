using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitBeamCalculator.Models;

namespace RevitBeamCalculator.Services
{
    /// <summary>
    /// Service for finding diaphragms associated with beams and calculating tributary areas.
    /// </summary>
    public class DiaphragmService
    {
        private readonly Document _doc;
        private const double Tolerance = 0.01; // feet

        public DiaphragmService(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Finds the diaphragm (Area element) associated with a beam.
        /// </summary>
        public DiaphragmData? FindAssociatedDiaphragm(FamilyInstance beam)
        {
            // Get beam location
            LocationCurve? location = beam.Location as LocationCurve;
            if (location == null) return null;

            XYZ midpoint = location.Curve.Evaluate(0.5, true);
            double beamElevation = midpoint.Z;

            // Find area schemes (diaphragm definitions)
            FilteredElementCollector areaSchemes = new FilteredElementCollector(_doc)
                .OfClass(typeof(AreaScheme));

            // Find areas at the beam's level
            foreach (AreaScheme scheme in areaSchemes.Cast<AreaScheme>())
            {
                // Look for areas in this scheme
                FilteredElementCollector areas = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_Areas)
                    .OfClass(typeof(Area));

                foreach (Area area in areas.Cast<Area>())
                {
                    if (area.AreaScheme.Id != scheme.Id) continue;
                    if (area.Area <= 0) continue; // Skip unbounded areas

                    // Check if beam is within this area's level
                    Level? areaLevel = _doc.GetElement(area.LevelId) as Level;
                    if (areaLevel == null) continue;

                    // Check elevation match (within reasonable tolerance)
                    if (Math.Abs(areaLevel.Elevation - beamElevation) > 10) continue; // 10 feet tolerance

                    // Check if beam midpoint is inside area boundary
                    if (IsPointInArea(midpoint, area))
                    {
                        return ExtractDiaphragmData(area);
                    }
                }
            }

            // If no area found, try to find floor elements
            return FindDiaphragmFromFloors(beam);
        }

        /// <summary>
        /// Calculates the tributary width for a beam based on surrounding geometry.
        /// </summary>
        public double CalculateTributaryWidth(FamilyInstance beam, BeamData beamData)
        {
            // Get beam centerline
            LocationCurve? location = beam.Location as LocationCurve;
            if (location == null) return 0;

            Curve beamCurve = location.Curve;
            XYZ beamStart = beamCurve.GetEndPoint(0);
            XYZ beamEnd = beamCurve.GetEndPoint(1);
            XYZ beamDirection = (beamEnd - beamStart).Normalize();

            // Direction perpendicular to beam (in plan)
            XYZ perpDirection = new XYZ(-beamDirection.Y, beamDirection.X, 0);

            // Find parallel beams on each side
            double distanceLeft = FindDistanceToParallelBeam(beam, beamCurve, perpDirection.Negate());
            double distanceRight = FindDistanceToParallelBeam(beam, beamCurve, perpDirection);

            // Tributary width is half the distance to each adjacent beam
            double tributaryLeft = distanceLeft > 0 ? distanceLeft / 2 : GetDefaultTributaryWidth();
            double tributaryRight = distanceRight > 0 ? distanceRight / 2 : GetDefaultTributaryWidth();

            return tributaryLeft + tributaryRight;
        }

        /// <summary>
        /// Calculates detailed tributary area data.
        /// </summary>
        public TributaryAreaData CalculateTributaryAreaData(FamilyInstance beam, BeamData beamData, DiaphragmData? diaphragm)
        {
            double tributaryWidth = CalculateTributaryWidth(beam, beamData);

            TributaryAreaData data = new TributaryAreaData
            {
                TributaryWidth = tributaryWidth,
                TributaryLength = beamData.Length,
                TributaryArea = tributaryWidth * beamData.Length,
                Diaphragm = diaphragm
            };

            // Calculate individual widths
            LocationCurve? location = beam.Location as LocationCurve;
            if (location != null)
            {
                Curve beamCurve = location.Curve;
                XYZ beamDirection = (beamCurve.GetEndPoint(1) - beamCurve.GetEndPoint(0)).Normalize();
                XYZ perpDirection = new XYZ(-beamDirection.Y, beamDirection.X, 0);

                double distanceLeft = FindDistanceToParallelBeam(beam, beamCurve, perpDirection.Negate());
                double distanceRight = FindDistanceToParallelBeam(beam, beamCurve, perpDirection);

                data.TributaryWidthLeft = distanceLeft > 0 ? distanceLeft / 2 : GetDefaultTributaryWidth() / 2;
                data.TributaryWidthRight = distanceRight > 0 ? distanceRight / 2 : GetDefaultTributaryWidth() / 2;
            }

            return data;
        }

        private DiaphragmData ExtractDiaphragmData(Area area)
        {
            DiaphragmData data = new DiaphragmData
            {
                AreaId = area.Id,
                AreaName = area.Name,
                GrossArea = area.Area, // Already in sq ft
                NetArea = area.Area
            };

            // Get area scheme name
            AreaScheme? scheme = _doc.GetElement(area.AreaScheme.Id) as AreaScheme;
            if (scheme != null)
            {
                data.AreaSchemeName = scheme.Name;
            }

            // Get level
            Level? level = _doc.GetElement(area.LevelId) as Level;
            if (level != null)
            {
                data.LevelName = level.Name;
                data.LevelElevation = level.Elevation;
            }

            // Try to determine occupancy from area name
            data.Occupancy = DetermineOccupancy(area.Name);
            data.LiveLoad = (int)data.Occupancy;

            // Get boundary points
            IList<IList<BoundarySegment>> boundaries = area.GetBoundarySegments(new SpatialElementBoundaryOptions());
            if (boundaries != null && boundaries.Count > 0)
            {
                foreach (BoundarySegment segment in boundaries[0])
                {
                    Curve curve = segment.GetCurve();
                    data.BoundaryPoints.Add(curve.GetEndPoint(0));
                }
            }

            // Default slab properties (could be enhanced to read from floor elements)
            LoadSettings settings = SettingsManager.GetLoadSettings();
            data.SlabThickness = settings.DefaultSlabThickness;
            data.SlabDensity = settings.DefaultConcreteDensity;
            data.SlabSelfWeight = (data.SlabThickness / 12) * data.SlabDensity; // psf
            data.SuperimposedDeadLoad = settings.DefaultSuperimposedDeadLoad;

            return data;
        }

        private DiaphragmData? FindDiaphragmFromFloors(FamilyInstance beam)
        {
            // Find floor elements near the beam
            LocationCurve? location = beam.Location as LocationCurve;
            if (location == null) return null;

            XYZ midpoint = location.Curve.Evaluate(0.5, true);

            // Create a bounding box to search for floors
            BoundingBoxXYZ searchBox = beam.get_BoundingBox(null);
            if (searchBox == null) return null;

            // Expand search box vertically
            XYZ min = new XYZ(searchBox.Min.X - 5, searchBox.Min.Y - 5, searchBox.Min.Z - 2);
            XYZ max = new XYZ(searchBox.Max.X + 5, searchBox.Max.Y + 5, searchBox.Max.Z + 2);
            Outline outline = new Outline(min, max);

            BoundingBoxIntersectsFilter filter = new BoundingBoxIntersectsFilter(outline);

            FilteredElementCollector floors = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WherePasses(filter);

            Floor? nearestFloor = floors.Cast<Floor>().FirstOrDefault();

            if (nearestFloor != null)
            {
                return CreateDiaphragmFromFloor(nearestFloor);
            }

            // Return default if no floor found
            return CreateDefaultDiaphragm(beam);
        }

        private DiaphragmData CreateDiaphragmFromFloor(Floor floor)
        {
            DiaphragmData data = new DiaphragmData
            {
                AreaId = floor.Id,
                AreaName = floor.Name,
                AreaSchemeName = "Floor Element"
            };

            // Get floor type for thickness
            FloorType? floorType = _doc.GetElement(floor.GetTypeId()) as FloorType;
            if (floorType != null)
            {
                // Get compound structure thickness
                CompoundStructure? structure = floorType.GetCompoundStructure();
                if (structure != null)
                {
                    data.SlabThickness = structure.GetWidth() * 12; // Convert ft to inches
                }
            }

            // Get level
            Level? level = _doc.GetElement(floor.LevelId) as Level;
            if (level != null)
            {
                data.LevelName = level.Name;
                data.LevelElevation = level.Elevation;
            }

            // Default load values
            LoadSettings settings = SettingsManager.GetLoadSettings();
            data.SlabDensity = settings.DefaultConcreteDensity;
            data.SlabSelfWeight = (data.SlabThickness / 12) * data.SlabDensity;
            data.SuperimposedDeadLoad = settings.DefaultSuperimposedDeadLoad;
            data.LiveLoad = settings.DefaultLiveLoad;
            data.Occupancy = OccupancyType.Office;

            // Calculate area from floor geometry
            GeometryElement geometry = floor.get_Geometry(new Options());
            if (geometry != null)
            {
                foreach (GeometryObject obj in geometry)
                {
                    if (obj is Solid solid && solid.Volume > 0)
                    {
                        // Approximate area from faces
                        foreach (Face face in solid.Faces)
                        {
                            XYZ normal = face.ComputeNormal(new UV(0.5, 0.5));
                            if (Math.Abs(normal.Z) > 0.9) // Horizontal face
                            {
                                data.GrossArea = face.Area; // sq ft
                                break;
                            }
                        }
                    }
                }
            }

            data.NetArea = data.GrossArea;

            return data;
        }

        private DiaphragmData CreateDefaultDiaphragm(FamilyInstance beam)
        {
            LoadSettings settings = SettingsManager.GetLoadSettings();

            return new DiaphragmData
            {
                AreaName = "Default (No diaphragm found)",
                AreaSchemeName = "Default",
                Occupancy = OccupancyType.Office,
                LiveLoad = settings.DefaultLiveLoad,
                SuperimposedDeadLoad = settings.DefaultSuperimposedDeadLoad,
                SlabThickness = settings.DefaultSlabThickness,
                SlabDensity = settings.DefaultConcreteDensity,
                SlabSelfWeight = (settings.DefaultSlabThickness / 12) * settings.DefaultConcreteDensity
            };
        }

        private double FindDistanceToParallelBeam(FamilyInstance beam, Curve beamCurve, XYZ searchDirection)
        {
            XYZ beamMid = beamCurve.Evaluate(0.5, true);
            XYZ beamDirection = (beamCurve.GetEndPoint(1) - beamCurve.GetEndPoint(0)).Normalize();

            // Search up to 50 feet in the perpendicular direction
            double maxSearchDistance = 50;

            // Get all structural framing on the same level
            FilteredElementCollector collector = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilyInstance));

            double minDistance = double.MaxValue;

            foreach (FamilyInstance otherBeam in collector.Cast<FamilyInstance>())
            {
                if (otherBeam.Id == beam.Id) continue;
                if (otherBeam.StructuralType != StructuralType.Beam) continue;

                LocationCurve? otherLocation = otherBeam.Location as LocationCurve;
                if (otherLocation == null) continue;

                Curve otherCurve = otherLocation.Curve;
                XYZ otherStart = otherCurve.GetEndPoint(0);
                XYZ otherEnd = otherCurve.GetEndPoint(1);
                XYZ otherDirection = (otherEnd - otherStart).Normalize();

                // Check if beams are roughly parallel (within 10 degrees)
                double dotProduct = Math.Abs(beamDirection.DotProduct(otherDirection));
                if (dotProduct < 0.985) continue; // cos(10°) ≈ 0.985

                // Check elevation (should be on same level)
                XYZ otherMid = otherCurve.Evaluate(0.5, true);
                if (Math.Abs(beamMid.Z - otherMid.Z) > 2) continue; // 2 feet tolerance

                // Calculate perpendicular distance
                XYZ toOther = otherMid - beamMid;
                double perpDistance = toOther.DotProduct(searchDirection);

                // Only consider beams in the search direction
                if (perpDistance <= 0.1) continue; // Must be at least 0.1 ft in search direction
                if (perpDistance > maxSearchDistance) continue;

                // Check that beams overlap in plan
                double overlapStart = Math.Max(
                    Math.Min(beamCurve.GetEndPoint(0).DotProduct(beamDirection),
                             beamCurve.GetEndPoint(1).DotProduct(beamDirection)),
                    Math.Min(otherCurve.GetEndPoint(0).DotProduct(beamDirection),
                             otherCurve.GetEndPoint(1).DotProduct(beamDirection))
                );
                double overlapEnd = Math.Min(
                    Math.Max(beamCurve.GetEndPoint(0).DotProduct(beamDirection),
                             beamCurve.GetEndPoint(1).DotProduct(beamDirection)),
                    Math.Max(otherCurve.GetEndPoint(0).DotProduct(beamDirection),
                             otherCurve.GetEndPoint(1).DotProduct(beamDirection))
                );

                if (overlapEnd <= overlapStart) continue; // No overlap

                if (perpDistance < minDistance)
                {
                    minDistance = perpDistance;
                }
            }

            return minDistance < double.MaxValue ? minDistance : 0;
        }

        private bool IsPointInArea(XYZ point, Area area)
        {
            // Get area boundary
            IList<IList<BoundarySegment>> boundaries = area.GetBoundarySegments(new SpatialElementBoundaryOptions());

            if (boundaries == null || boundaries.Count == 0) return false;

            // Get boundary polygon points
            List<XYZ> polygon = new List<XYZ>();
            foreach (BoundarySegment segment in boundaries[0])
            {
                polygon.Add(segment.GetCurve().GetEndPoint(0));
            }

            // Point-in-polygon test
            return IsPointInPolygon(point, polygon);
        }

        private bool IsPointInPolygon(XYZ point, List<XYZ> polygon)
        {
            int n = polygon.Count;
            bool inside = false;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = polygon[i].X, yi = polygon[i].Y;
                double xj = polygon[j].X, yj = polygon[j].Y;

                bool intersect = ((yi > point.Y) != (yj > point.Y)) &&
                    (point.X < (xj - xi) * (point.Y - yi) / (yj - yi) + xi);

                if (intersect) inside = !inside;
            }

            return inside;
        }

        private OccupancyType DetermineOccupancy(string areaName)
        {
            string upper = areaName.ToUpperInvariant();

            if (upper.Contains("OFFICE")) return OccupancyType.Office;
            if (upper.Contains("RESIDENTIAL") || upper.Contains("APARTMENT")) return OccupancyType.Residential;
            if (upper.Contains("RETAIL") || upper.Contains("STORE")) return OccupancyType.Retail;
            if (upper.Contains("STORAGE")) return OccupancyType.StorageLight;
            if (upper.Contains("CORRIDOR")) return OccupancyType.Corridor;
            if (upper.Contains("ASSEMBLY") || upper.Contains("LOBBY")) return OccupancyType.AssemblyMovable;
            if (upper.Contains("MECHANICAL") || upper.Contains("MECH")) return OccupancyType.Mechanical;
            if (upper.Contains("PARKING")) return OccupancyType.Parking;
            if (upper.Contains("ROOF")) return OccupancyType.RoofOrdinary;
            if (upper.Contains("LIBRARY")) return OccupancyType.Library;

            return OccupancyType.Office; // Default
        }

        private double GetDefaultTributaryWidth()
        {
            // Default tributary width when no adjacent beams found
            return 10.0; // 10 feet
        }
    }
}
