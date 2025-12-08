using System;
using Autodesk.Revit.DB;

namespace RevitBeamCalculator.Models
{
    /// <summary>
    /// Represents the extracted data from a Revit beam element.
    /// Contains geometry, material properties, and section properties.
    /// </summary>
    public class BeamData
    {
        // Identification
        public ElementId ElementId { get; set; } = ElementId.InvalidElementId;
        public string Mark { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;

        // Geometry
        public double Length { get; set; }           // feet
        public XYZ StartPoint { get; set; } = XYZ.Zero;
        public XYZ EndPoint { get; set; } = XYZ.Zero;
        public double Rotation { get; set; }         // radians
        public string LevelName { get; set; } = string.Empty;
        public double Elevation { get; set; }        // feet

        // Section Properties (from family type)
        public SectionProperties Section { get; set; } = new SectionProperties();

        // Material Properties
        public MaterialData Material { get; set; } = new MaterialData();

        // Support Conditions
        public SupportCondition StartSupport { get; set; } = SupportCondition.Pinned;
        public SupportCondition EndSupport { get; set; } = SupportCondition.Pinned;
        public bool IsContinuousStart { get; set; }
        public bool IsContinuousEnd { get; set; }

        // Analysis Model
        public bool HasAnalyticalModel { get; set; }
        public XYZ AnalyticalStart { get; set; } = XYZ.Zero;
        public XYZ AnalyticalEnd { get; set; } = XYZ.Zero;

        /// <summary>
        /// Gets the effective span length considering support conditions.
        /// </summary>
        public double EffectiveLength => Length; // Can be adjusted for partial fixity

        /// <summary>
        /// Gets a display string for the beam identification.
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(Mark)
            ? $"{Mark} ({TypeName})"
            : $"{TypeName} [{ElementId}]";
    }

    /// <summary>
    /// Section geometric properties for structural calculations.
    /// </summary>
    public class SectionProperties
    {
        public string ShapeName { get; set; } = string.Empty;
        public SectionShape Shape { get; set; } = SectionShape.WideFlange;

        // Dimensions (inches)
        public double Depth { get; set; }            // d
        public double Width { get; set; }            // bf (flange width) or b
        public double WebThickness { get; set; }     // tw
        public double FlangeThickness { get; set; }  // tf

        // Calculated Properties (in^2, in^4, in^3)
        public double Area { get; set; }             // A
        public double MomentOfInertiaX { get; set; } // Ix (strong axis)
        public double MomentOfInertiaY { get; set; } // Iy (weak axis)
        public double SectionModulusX { get; set; }  // Sx
        public double SectionModulusY { get; set; }  // Sy
        public double PlasticModulusX { get; set; }  // Zx
        public double PlasticModulusY { get; set; }  // Zy
        public double RadiusOfGyrationX { get; set; }// rx
        public double RadiusOfGyrationY { get; set; }// ry
        public double TorsionalConstant { get; set; }// J
        public double WarpingConstant { get; set; }  // Cw

        // For composite beams
        public double EffectiveWidth { get; set; }   // Effective slab width for composite action
        public bool IsComposite { get; set; }

        /// <summary>
        /// Weight per linear foot (plf) for steel sections.
        /// </summary>
        public double WeightPerFoot { get; set; }
    }

    /// <summary>
    /// Material properties for structural calculations.
    /// </summary>
    public class MaterialData
    {
        public string Name { get; set; } = string.Empty;
        public MaterialType Type { get; set; } = MaterialType.Steel;

        // Common Properties
        public double Density { get; set; }          // pcf (pounds per cubic foot)
        public double ModulusOfElasticity { get; set; } // E (ksi)
        public double ShearModulus { get; set; }     // G (ksi)
        public double PoissonsRatio { get; set; }    // nu

        // Steel Properties
        public double YieldStrength { get; set; }    // Fy (ksi)
        public double TensileStrength { get; set; }  // Fu (ksi)

        // Concrete Properties
        public double CompressiveStrength { get; set; } // f'c (ksi)

        // Wood Properties
        public double BendingStrength { get; set; }  // Fb (psi)
        public double ShearStrength { get; set; }    // Fv (psi)
        public double CompressionParallel { get; set; } // Fc (psi)
        public string WoodSpecies { get; set; } = string.Empty;
        public string WoodGrade { get; set; } = string.Empty;
    }

    /// <summary>
    /// Types of structural materials.
    /// </summary>
    public enum MaterialType
    {
        Steel,
        Concrete,
        Wood,
        Aluminum,
        ColdFormedSteel,
        Composite,
        Other
    }

    /// <summary>
    /// Standard structural section shapes.
    /// </summary>
    public enum SectionShape
    {
        WideFlange,      // W shapes
        Channel,         // C shapes
        Angle,           // L shapes
        Tube,            // HSS rectangular
        Pipe,            // HSS round
        Tee,             // WT shapes
        Rectangular,     // Solid rectangular (concrete/wood)
        Circular,        // Solid circular
        DoubleAngle,     // 2L shapes
        Custom
    }

    /// <summary>
    /// Beam end support conditions.
    /// </summary>
    public enum SupportCondition
    {
        Free,            // Cantilever end
        Pinned,          // Simply supported
        Fixed,           // Fully restrained
        PartiallyFixed,  // Some rotational restraint
        Continuous       // Continuous over support
    }
}
