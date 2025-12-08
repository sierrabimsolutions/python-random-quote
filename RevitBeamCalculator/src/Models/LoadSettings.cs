using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RevitBeamCalculator.Models
{
    /// <summary>
    /// User-configurable settings for beam calculations.
    /// Includes default loads, code preferences, and calculation options.
    /// </summary>
    public class LoadSettings
    {
        // Default Area Loads (psf)
        public double DefaultSuperimposedDeadLoad { get; set; } = 20.0;  // psf (MEP, finishes)
        public double DefaultLiveLoad { get; set; } = 50.0;              // psf (office)
        public double DefaultSlabThickness { get; set; } = 6.0;          // inches
        public double DefaultConcreteDensity { get; set; } = 150.0;      // pcf

        // Live Load by Occupancy (psf) - ASCE 7 Table 4-1
        public Dictionary<OccupancyType, double> LiveLoadByOccupancy { get; set; } = new Dictionary<OccupancyType, double>
        {
            { OccupancyType.Residential, 40 },
            { OccupancyType.Office, 50 },
            { OccupancyType.AssemblyFixed, 60 },
            { OccupancyType.AssemblyMovable, 100 },
            { OccupancyType.StorageLight, 125 },
            { OccupancyType.StorageHeavy, 250 },
            { OccupancyType.Retail, 75 },
            { OccupancyType.Hospital, 60 },
            { OccupancyType.Schools, 40 },
            { OccupancyType.Parking, 40 },
            { OccupancyType.Library, 150 },
            { OccupancyType.Corridor, 100 },
            { OccupancyType.RoofOrdinary, 20 }
        };

        // Design Code Settings
        public DesignMethod DesignMethod { get; set; } = DesignMethod.LRFD;
        public SteelCode SteelCode { get; set; } = SteelCode.AISC360_22;
        public ConcreteCode ConcreteCode { get; set; } = ConcreteCode.ACI318_19;
        public LoadCode LoadCode { get; set; } = LoadCode.ASCE7_22;
        public WoodCode WoodCode { get; set; } = WoodCode.NDS_2024;

        // Deflection Criteria
        public DeflectionCriteria DeflectionCriteria { get; set; } = new DeflectionCriteria();

        // Calculation Options
        public bool IncludeSelfWeight { get; set; } = true;
        public bool UseEffectiveLength { get; set; } = true;
        public bool ConsiderLateralBracing { get; set; } = true;
        public bool UseReducedLiveLoad { get; set; } = false;

        // Unbraced Length (for LTB calculations)
        public double DefaultUnbracedLengthRatio { get; set; } = 1.0; // Fraction of span
        public bool UseActualUnbracedLength { get; set; } = true;

        // Material Defaults
        public double DefaultSteelFy { get; set; } = 50.0;     // ksi (A992)
        public double DefaultSteelFu { get; set; } = 65.0;     // ksi
        public double DefaultSteelE { get; set; } = 29000.0;   // ksi
        public double DefaultConcreteFc { get; set; } = 4.0;   // ksi

        // Output Options
        public bool ShowDetailedCalculations { get; set; } = true;
        public bool ShowDiagrams { get; set; } = true;
        public UnitSystem UnitSystem { get; set; } = UnitSystem.Imperial;

        // Report Options
        public bool IncludeAssumptions { get; set; } = true;
        public bool IncludeCodeReferences { get; set; } = true;
        public string CompanyName { get; set; } = string.Empty;
        public string ProjectNumber { get; set; } = string.Empty;
    }

    /// <summary>
    /// Deflection limit criteria.
    /// </summary>
    public class DeflectionCriteria
    {
        // Live Load Deflection Limits
        public double LiveLoadLimit { get; set; } = 360;       // L/360
        public bool CheckLiveLoadDeflection { get; set; } = true;

        // Total Load Deflection Limits
        public double TotalLoadLimit { get; set; } = 240;      // L/240
        public bool CheckTotalDeflection { get; set; } = true;

        // Long-term (Creep) Deflection
        public double LongTermLimit { get; set; } = 480;       // L/480
        public bool CheckLongTermDeflection { get; set; } = false;
        public double CreepFactor { get; set; } = 2.0;         // For concrete/wood

        // Ponding Check
        public bool CheckPonding { get; set; } = false;

        // Vibration Criteria
        public bool CheckVibration { get; set; } = false;
        public double MinimumFrequency { get; set; } = 4.0;    // Hz

        /// <summary>
        /// Gets the allowable deflection for a given span length.
        /// </summary>
        public double GetAllowableLiveLoadDeflection(double spanLength)
        {
            return spanLength * 12 / LiveLoadLimit; // inches
        }

        public double GetAllowableTotalDeflection(double spanLength)
        {
            return spanLength * 12 / TotalLoadLimit; // inches
        }
    }

    /// <summary>
    /// Design philosophy: LRFD vs ASD.
    /// </summary>
    public enum DesignMethod
    {
        LRFD,   // Load and Resistance Factor Design
        ASD     // Allowable Stress Design
    }

    /// <summary>
    /// Steel design code editions.
    /// </summary>
    public enum SteelCode
    {
        AISC360_16,     // AISC 360-16
        AISC360_22      // AISC 360-22
    }

    /// <summary>
    /// Concrete design code editions.
    /// </summary>
    public enum ConcreteCode
    {
        ACI318_14,      // ACI 318-14
        ACI318_19       // ACI 318-19
    }

    /// <summary>
    /// Load code editions.
    /// </summary>
    public enum LoadCode
    {
        ASCE7_16,       // ASCE 7-16
        ASCE7_22        // ASCE 7-22
    }

    /// <summary>
    /// Wood design code editions.
    /// </summary>
    public enum WoodCode
    {
        NDS_2018,       // NDS-2018
        NDS_2024        // NDS-2024
    }

    /// <summary>
    /// Unit system for input/output.
    /// </summary>
    public enum UnitSystem
    {
        Imperial,       // US units (kips, feet, inches)
        Metric          // SI units (kN, meters, mm)
    }

    /// <summary>
    /// Manages loading and saving of settings.
    /// </summary>
    public static class SettingsManager
    {
        private static LoadSettings? _currentSettings;
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RevitBeamCalculator",
            "settings.json"
        );

        public static LoadSettings GetLoadSettings()
        {
            if (_currentSettings != null)
                return _currentSettings;

            if (File.Exists(SettingsPath))
            {
                try
                {
                    string json = File.ReadAllText(SettingsPath);
                    _currentSettings = JsonSerializer.Deserialize<LoadSettings>(json) ?? new LoadSettings();
                }
                catch
                {
                    _currentSettings = new LoadSettings();
                }
            }
            else
            {
                _currentSettings = new LoadSettings();
            }

            return _currentSettings;
        }

        public static void SaveSettings(LoadSettings settings)
        {
            _currentSettings = settings;

            try
            {
                string? directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Silently fail if we can't write settings
            }
        }

        public static void ResetToDefaults()
        {
            _currentSettings = new LoadSettings();
            SaveSettings(_currentSettings);
        }
    }
}
