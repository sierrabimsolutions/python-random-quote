using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitBeamCalculator.Models;

namespace RevitBeamCalculator.Services
{
    /// <summary>
    /// Extracts structural and geometric data from Revit beam elements.
    /// </summary>
    public class BeamDataExtractor
    {
        private readonly Document _doc;

        public BeamDataExtractor(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Extracts all relevant data from a beam element.
        /// </summary>
        public BeamData ExtractBeamData(FamilyInstance beam)
        {
            BeamData data = new BeamData
            {
                ElementId = beam.Id,
                Mark = beam.LookupParameter("Mark")?.AsString() ?? string.Empty,
                FamilyName = beam.Symbol.FamilyName,
                TypeName = beam.Symbol.Name
            };

            // Extract geometry
            ExtractGeometry(beam, data);

            // Extract section properties
            ExtractSectionProperties(beam, data);

            // Extract material properties
            ExtractMaterialProperties(beam, data);

            // Extract support conditions from analytical model
            ExtractSupportConditions(beam, data);

            return data;
        }

        private void ExtractGeometry(FamilyInstance beam, BeamData data)
        {
            // Get the location curve
            if (beam.Location is LocationCurve locationCurve)
            {
                Curve curve = locationCurve.Curve;

                if (curve is Line line)
                {
                    data.StartPoint = line.GetEndPoint(0);
                    data.EndPoint = line.GetEndPoint(1);
                    data.Length = line.Length; // feet (internal Revit units)
                }
                else
                {
                    // For curved beams, use approximate length
                    data.StartPoint = curve.GetEndPoint(0);
                    data.EndPoint = curve.GetEndPoint(1);
                    data.Length = curve.Length;
                }
            }

            // Get level
            Level? level = _doc.GetElement(beam.LevelId) as Level;
            if (level != null)
            {
                data.LevelName = level.Name;
                data.Elevation = level.Elevation;
            }

            // Get rotation
            data.Rotation = beam.LookupParameter("Cross-Section Rotation")?.AsDouble() ?? 0;

            // Check for analytical model
            AnalyticalElement? analyticalModel = GetAnalyticalModel(beam);
            if (analyticalModel != null)
            {
                data.HasAnalyticalModel = true;
                Curve? analyticalCurve = analyticalModel.GetCurve();
                if (analyticalCurve != null)
                {
                    data.AnalyticalStart = analyticalCurve.GetEndPoint(0);
                    data.AnalyticalEnd = analyticalCurve.GetEndPoint(1);
                }
            }
        }

        private AnalyticalElement? GetAnalyticalModel(FamilyInstance beam)
        {
            // In Revit 2023+, use AnalyticalToPhysicalAssociationManager
            try
            {
                AnalyticalToPhysicalAssociationManager manager =
                    AnalyticalToPhysicalAssociationManager.GetAnalyticalToPhysicalAssociationManager(_doc);

                if (manager != null)
                {
                    ElementId analyticalId = manager.GetAssociatedElementId(beam.Id);
                    if (analyticalId != ElementId.InvalidElementId)
                    {
                        return _doc.GetElement(analyticalId) as AnalyticalElement;
                    }
                }
            }
            catch
            {
                // Fallback for older Revit versions or if analytical model doesn't exist
            }

            return null;
        }

        private void ExtractSectionProperties(FamilyInstance beam, BeamData data)
        {
            FamilySymbol symbol = beam.Symbol;
            SectionProperties section = new SectionProperties
            {
                ShapeName = symbol.Name
            };

            // Determine section shape from family name
            section.Shape = DetermineSectionShape(symbol.FamilyName, symbol.Name);

            // Extract dimensions from type parameters
            // Common parameter names in steel families
            section.Depth = GetParameterValue(symbol, "d", "Depth", "bf") ?? 0;
            section.Width = GetParameterValue(symbol, "bf", "Width", "Flange Width") ?? 0;
            section.WebThickness = GetParameterValue(symbol, "tw", "Web Thickness") ?? 0;
            section.FlangeThickness = GetParameterValue(symbol, "tf", "Flange Thickness") ?? 0;

            // Section properties (from structural asset or type parameters)
            section.Area = GetParameterValue(symbol, "A", "Area", "Cross-Sectional Area") ?? 0;
            section.MomentOfInertiaX = GetParameterValue(symbol, "Ix", "Moment of Inertia X", "Ixx") ?? 0;
            section.MomentOfInertiaY = GetParameterValue(symbol, "Iy", "Moment of Inertia Y", "Iyy") ?? 0;
            section.SectionModulusX = GetParameterValue(symbol, "Sx", "Section Modulus X", "Sxx") ?? 0;
            section.SectionModulusY = GetParameterValue(symbol, "Sy", "Section Modulus Y", "Syy") ?? 0;
            section.PlasticModulusX = GetParameterValue(symbol, "Zx", "Plastic Modulus X", "Zxx") ?? 0;
            section.PlasticModulusY = GetParameterValue(symbol, "Zy", "Plastic Modulus Y", "Zyy") ?? 0;
            section.RadiusOfGyrationX = GetParameterValue(symbol, "rx", "Radius of Gyration X") ?? 0;
            section.RadiusOfGyrationY = GetParameterValue(symbol, "ry", "Radius of Gyration Y") ?? 0;
            section.TorsionalConstant = GetParameterValue(symbol, "J", "Torsional Constant") ?? 0;
            section.WarpingConstant = GetParameterValue(symbol, "Cw", "Warping Constant") ?? 0;

            // Weight per foot
            section.WeightPerFoot = GetParameterValue(symbol, "W", "Weight", "Nominal Weight") ?? 0;

            // If key properties are missing, try to get from structural asset
            if (section.MomentOfInertiaX == 0)
            {
                ExtractFromStructuralAsset(symbol, section);
            }

            data.Section = section;
        }

        private double? GetParameterValue(Element element, params string[] paramNames)
        {
            foreach (string name in paramNames)
            {
                Parameter? param = element.LookupParameter(name);
                if (param != null && param.HasValue)
                {
                    double value = param.AsDouble();
                    // Convert from internal units if needed (Revit stores in feet/radians)
                    if (param.Definition.GetDataType() == SpecTypeId.Area)
                        return value * 144; // ft² to in²
                    if (param.Definition.GetDataType() == SpecTypeId.MomentOfInertia)
                        return value * 20736; // ft⁴ to in⁴
                    if (param.Definition.GetDataType() == SpecTypeId.SectionModulus)
                        return value * 1728; // ft³ to in³
                    if (param.Definition.GetDataType() == SpecTypeId.Length)
                        return value * 12; // ft to in

                    return value;
                }
            }
            return null;
        }

        private SectionShape DetermineSectionShape(string familyName, string typeName)
        {
            string combined = $"{familyName} {typeName}".ToUpperInvariant();

            if (combined.Contains("W ") || combined.StartsWith("W"))
                return SectionShape.WideFlange;
            if (combined.Contains("HSS") && (combined.Contains("X") && !combined.Contains("RND")))
                return SectionShape.Tube;
            if (combined.Contains("HSS") && combined.Contains("RND"))
                return SectionShape.Pipe;
            if (combined.Contains("PIPE"))
                return SectionShape.Pipe;
            if (combined.Contains("C ") || combined.StartsWith("C") || combined.Contains("CHANNEL"))
                return SectionShape.Channel;
            if (combined.Contains("L ") || combined.StartsWith("L") || combined.Contains("ANGLE"))
                return SectionShape.Angle;
            if (combined.Contains("WT ") || combined.StartsWith("WT"))
                return SectionShape.Tee;
            if (combined.Contains("2L"))
                return SectionShape.DoubleAngle;

            return SectionShape.Custom;
        }

        private void ExtractFromStructuralAsset(FamilySymbol symbol, SectionProperties section)
        {
            // Try to get the structural asset from the material
            try
            {
                ElementId materialId = symbol.LookupParameter("Structural Material")?.AsElementId()
                    ?? symbol.LookupParameter("Material")?.AsElementId()
                    ?? ElementId.InvalidElementId;

                if (materialId != ElementId.InvalidElementId)
                {
                    Material? material = _doc.GetElement(materialId) as Material;
                    if (material != null)
                    {
                        PropertySetElement? structuralAsset = _doc.GetElement(material.StructuralAssetId) as PropertySetElement;
                        // Additional extraction from structural asset can be done here
                    }
                }
            }
            catch
            {
                // Structural asset not available
            }
        }

        private void ExtractMaterialProperties(FamilyInstance beam, BeamData data)
        {
            MaterialData material = new MaterialData();

            // Get material from beam or symbol
            ElementId materialId = beam.StructuralMaterialId;
            if (materialId == ElementId.InvalidElementId)
            {
                // Try getting from symbol
                materialId = beam.Symbol.LookupParameter("Structural Material")?.AsElementId()
                    ?? beam.Symbol.LookupParameter("Material")?.AsElementId()
                    ?? ElementId.InvalidElementId;
            }

            if (materialId != ElementId.InvalidElementId)
            {
                Material? revitMaterial = _doc.GetElement(materialId) as Material;
                if (revitMaterial != null)
                {
                    material.Name = revitMaterial.Name;

                    // Determine material type from name or structural asset
                    material.Type = DetermineMaterialType(revitMaterial.Name);

                    // Get structural properties from asset
                    PropertySetElement? structuralAsset = _doc.GetElement(revitMaterial.StructuralAssetId) as PropertySetElement;
                    if (structuralAsset != null)
                    {
                        StructuralAsset asset = structuralAsset.GetStructuralAsset();
                        ExtractFromAsset(asset, material);
                    }
                    else
                    {
                        // Use defaults based on material type
                        SetDefaultMaterialProperties(material);
                    }
                }
            }
            else
            {
                // Default to steel if no material found
                material.Type = MaterialType.Steel;
                material.Name = "A992 (Default)";
                SetDefaultMaterialProperties(material);
            }

            data.Material = material;
        }

        private MaterialType DetermineMaterialType(string materialName)
        {
            string upper = materialName.ToUpperInvariant();

            if (upper.Contains("STEEL") || upper.Contains("A992") || upper.Contains("A36") || upper.Contains("A572"))
                return MaterialType.Steel;
            if (upper.Contains("CONCRETE"))
                return MaterialType.Concrete;
            if (upper.Contains("WOOD") || upper.Contains("TIMBER") || upper.Contains("GLULAM") || upper.Contains("LVL"))
                return MaterialType.Wood;
            if (upper.Contains("ALUMINUM"))
                return MaterialType.Aluminum;

            return MaterialType.Steel; // Default
        }

        private void ExtractFromAsset(StructuralAsset asset, MaterialData material)
        {
            // Young's Modulus (convert from Pa to ksi)
            material.ModulusOfElasticity = asset.YoungModulus.X / 6894.76; // Pa to ksi

            // Shear Modulus
            material.ShearModulus = asset.ShearModulus.X / 6894.76;

            // Poisson's Ratio
            material.PoissonsRatio = asset.PoissonRatio.X;

            // Density (convert from kg/m³ to pcf)
            material.Density = asset.Density * 0.062428;

            // Material-specific properties
            switch (material.Type)
            {
                case MaterialType.Steel:
                    material.YieldStrength = asset.MinimumYieldStress / 6894.76; // Pa to ksi
                    material.TensileStrength = asset.MinimumTensileStrength / 6894.76;
                    break;
                case MaterialType.Concrete:
                    material.CompressiveStrength = asset.ConcreteCompression / 6894.76;
                    break;
                case MaterialType.Wood:
                    material.BendingStrength = asset.WoodBendingStrength / 6.89476; // Pa to psi
                    break;
            }
        }

        private void SetDefaultMaterialProperties(MaterialData material)
        {
            switch (material.Type)
            {
                case MaterialType.Steel:
                    material.YieldStrength = 50;     // ksi (A992)
                    material.TensileStrength = 65;   // ksi
                    material.ModulusOfElasticity = 29000; // ksi
                    material.ShearModulus = 11200;   // ksi
                    material.PoissonsRatio = 0.3;
                    material.Density = 490;          // pcf
                    break;

                case MaterialType.Concrete:
                    material.CompressiveStrength = 4; // ksi
                    material.ModulusOfElasticity = 3644; // 57√f'c ksi
                    material.PoissonsRatio = 0.2;
                    material.Density = 150;          // pcf
                    break;

                case MaterialType.Wood:
                    material.BendingStrength = 2400;  // psi (typical glulam)
                    material.ModulusOfElasticity = 1800; // ksi
                    material.Density = 35;           // pcf
                    break;

                default:
                    material.ModulusOfElasticity = 29000;
                    material.Density = 490;
                    break;
            }
        }

        private void ExtractSupportConditions(FamilyInstance beam, BeamData data)
        {
            // Get analytical model to determine support conditions
            AnalyticalElement? analyticalModel = GetAnalyticalModel(beam);

            if (analyticalModel != null)
            {
                // Check for releases at ends
                try
                {
                    // This would require checking analytical member releases
                    // Simplified assumption: if connected to columns, assume pinned
                    data.StartSupport = SupportCondition.Pinned;
                    data.EndSupport = SupportCondition.Pinned;

                    // Check for continuity
                    data.IsContinuousStart = CheckContinuity(beam, true);
                    data.IsContinuousEnd = CheckContinuity(beam, false);

                    if (data.IsContinuousStart)
                        data.StartSupport = SupportCondition.Continuous;
                    if (data.IsContinuousEnd)
                        data.EndSupport = SupportCondition.Continuous;
                }
                catch
                {
                    // Default to simply supported
                    data.StartSupport = SupportCondition.Pinned;
                    data.EndSupport = SupportCondition.Pinned;
                }
            }
            else
            {
                // Default to simply supported
                data.StartSupport = SupportCondition.Pinned;
                data.EndSupport = SupportCondition.Pinned;
            }
        }

        private bool CheckContinuity(FamilyInstance beam, bool atStart)
        {
            // Check if there's another beam connected at this end
            XYZ point = atStart ? beam.GetLocationCurve()?.Curve.GetEndPoint(0) ?? XYZ.Zero
                                : beam.GetLocationCurve()?.Curve.GetEndPoint(1) ?? XYZ.Zero;

            // Search for other beams at this point
            double tolerance = 0.5 / 12; // 0.5 inches in feet
            BoundingBoxXYZ searchBox = new BoundingBoxXYZ
            {
                Min = point - new XYZ(tolerance, tolerance, tolerance),
                Max = point + new XYZ(tolerance, tolerance, tolerance)
            };

            Outline outline = new Outline(searchBox.Min, searchBox.Max);
            BoundingBoxIntersectsFilter filter = new BoundingBoxIntersectsFilter(outline);

            FilteredElementCollector collector = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WherePasses(filter);

            int beamCount = collector
                .OfType<FamilyInstance>()
                .Count(fi => fi.Id != beam.Id && fi.StructuralType == StructuralType.Beam);

            return beamCount > 0;
        }
    }

    /// <summary>
    /// Extension methods for FamilyInstance.
    /// </summary>
    public static class FamilyInstanceExtensions
    {
        public static LocationCurve? GetLocationCurve(this FamilyInstance instance)
        {
            return instance.Location as LocationCurve;
        }
    }
}
