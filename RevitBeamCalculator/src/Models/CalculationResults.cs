using System;
using System.Collections.Generic;

namespace RevitBeamCalculator.Models
{
    /// <summary>
    /// Complete results from a beam structural analysis.
    /// Includes load calculations, structural analysis, and design checks.
    /// </summary>
    public class CalculationResults
    {
        // Input Summary
        public BeamData BeamData { get; set; } = new BeamData();
        public LoadSummary Loads { get; set; } = new LoadSummary();
        public TributaryAreaData TributaryData { get; set; } = new TributaryAreaData();

        // Analysis Results
        public double MaxMoment { get; set; }           // kip-ft
        public double MaxMomentLocation { get; set; }   // feet from start
        public double MaxShear { get; set; }            // kips
        public double MaxShearLocation { get; set; }    // feet from start
        public double MaxDeflection { get; set; }       // inches
        public double MaxDeflectionLocation { get; set; }// feet from start

        // Reactions
        public double StartReaction { get; set; }       // kips
        public double EndReaction { get; set; }         // kips

        // Diagram Data (for plotting)
        public List<DiagramPoint> MomentDiagram { get; set; } = new List<DiagramPoint>();
        public List<DiagramPoint> ShearDiagram { get; set; } = new List<DiagramPoint>();
        public List<DiagramPoint> DeflectionDiagram { get; set; } = new List<DiagramPoint>();

        // Design Checks
        public List<DesignCheck> DesignChecks { get; set; } = new List<DesignCheck>();

        // Overall Results
        public double DemandCapacityRatio { get; set; } // Maximum D/C ratio
        public string GoverningCheck { get; set; } = string.Empty;
        public string GoverningLoadCase { get; set; } = string.Empty;
        public bool PassesAllChecks { get; set; }

        // Deflection Limits
        public double AllowableDeflectionLL { get; set; }   // inches
        public double AllowableDeflectionTotal { get; set; }// inches
        public double ActualDeflectionLL { get; set; }      // inches
        public double ActualDeflectionTotal { get; set; }   // inches

        // Code Reference
        public string DesignCode { get; set; } = string.Empty; // e.g., "AISC 360-22"
        public string LoadCode { get; set; } = string.Empty;   // e.g., "ASCE 7-22"
    }

    /// <summary>
    /// Summary of loads applied to the beam.
    /// </summary>
    public class LoadSummary
    {
        // Line Loads (distributed, k/ft)
        public double DeadLoad { get; set; }            // k/ft
        public double SuperimposedDeadLoad { get; set; }// k/ft
        public double LiveLoad { get; set; }            // k/ft
        public double TotalServiceLoad { get; set; }    // k/ft
        public double SelfWeight { get; set; }          // k/ft (beam self-weight)

        // Factored Loads (k/ft)
        public double FactoredDeadLoad { get; set; }
        public double FactoredLiveLoad { get; set; }

        // Load Combinations Results
        public List<LoadCombination> LoadCombinations { get; set; } = new List<LoadCombination>();
        public LoadCombination GoverningCombination { get; set; } = new LoadCombination();

        // Area Loads (for reference, psf)
        public double DeadLoadPsf { get; set; }
        public double LiveLoadPsf { get; set; }

        // Tributary width used
        public double TributaryWidth { get; set; }      // feet
    }

    /// <summary>
    /// Load combination per ASCE 7.
    /// </summary>
    public class LoadCombination
    {
        public string Name { get; set; } = string.Empty;
        public string Equation { get; set; } = string.Empty;
        public double DeadFactor { get; set; }
        public double LiveFactor { get; set; }
        public double TotalLoad { get; set; }           // k/ft
        public double MaxMoment { get; set; }           // kip-ft
        public double MaxShear { get; set; }            // kips
        public bool IsGoverning { get; set; }

        // LRFD Load Combinations per ASCE 7
        public static List<LoadCombination> GetLRFDCombinations()
        {
            return new List<LoadCombination>
            {
                new LoadCombination { Name = "1.4D", Equation = "1.4D", DeadFactor = 1.4, LiveFactor = 0 },
                new LoadCombination { Name = "1.2D + 1.6L", Equation = "1.2D + 1.6L", DeadFactor = 1.2, LiveFactor = 1.6 },
                new LoadCombination { Name = "1.2D + 1.0L", Equation = "1.2D + 1.0L", DeadFactor = 1.2, LiveFactor = 1.0 },
            };
        }

        // ASD Load Combinations per ASCE 7
        public static List<LoadCombination> GetASDCombinations()
        {
            return new List<LoadCombination>
            {
                new LoadCombination { Name = "D", Equation = "D", DeadFactor = 1.0, LiveFactor = 0 },
                new LoadCombination { Name = "D + L", Equation = "D + L", DeadFactor = 1.0, LiveFactor = 1.0 },
            };
        }
    }

    /// <summary>
    /// Individual design check result.
    /// </summary>
    public class DesignCheck
    {
        public string Name { get; set; } = string.Empty;
        public DesignCheckType Type { get; set; }
        public double Demand { get; set; }              // Required strength
        public double Capacity { get; set; }            // Available strength
        public double Ratio { get; set; }               // D/C ratio
        public bool Passes { get; set; }
        public string Units { get; set; } = string.Empty;
        public string CodeReference { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Types of design checks performed.
    /// </summary>
    public enum DesignCheckType
    {
        // Flexure
        FlexuralYielding,
        LateralTorsionalBuckling,
        FlangeLocalBuckling,
        WebLocalBuckling,

        // Shear
        ShearStrength,
        ShearBuckling,

        // Deflection
        DeflectionLiveLoad,
        DeflectionTotalLoad,
        DeflectionLongTerm,

        // Combined
        CombinedStress,

        // Connections
        BearingStrength,
        WebCrippling,

        // Serviceability
        Vibration
    }

    /// <summary>
    /// Point on a structural diagram (moment, shear, or deflection).
    /// </summary>
    public class DiagramPoint
    {
        public double Location { get; set; }    // feet from start
        public double Value { get; set; }       // kip-ft, kips, or inches
    }
}
