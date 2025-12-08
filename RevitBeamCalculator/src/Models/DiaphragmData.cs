using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitBeamCalculator.Models
{
    /// <summary>
    /// Represents diaphragm data extracted from Revit Area elements.
    /// Contains information about floor areas and their associated loads.
    /// </summary>
    public class DiaphragmData
    {
        public ElementId AreaId { get; set; } = ElementId.InvalidElementId;
        public string AreaName { get; set; } = string.Empty;
        public string AreaSchemeName { get; set; } = string.Empty;

        // Geometry
        public double GrossArea { get; set; }        // Square feet
        public double NetArea { get; set; }          // After openings, sf
        public List<XYZ> BoundaryPoints { get; set; } = new List<XYZ>();
        public double Perimeter { get; set; }        // feet

        // Level Information
        public string LevelName { get; set; } = string.Empty;
        public double LevelElevation { get; set; }   // feet

        // Load Information
        public OccupancyType Occupancy { get; set; } = OccupancyType.Office;
        public double LiveLoad { get; set; }         // psf
        public double SuperimposedDeadLoad { get; set; } // psf
        public double SlabSelfWeight { get; set; }   // psf

        // Slab Properties
        public double SlabThickness { get; set; }    // inches
        public double SlabDensity { get; set; }      // pcf

        // For composite calculations
        public double ConcreteStrength { get; set; } // f'c (ksi)

        /// <summary>
        /// Total dead load (self-weight + superimposed).
        /// </summary>
        public double TotalDeadLoad => SlabSelfWeight + SuperimposedDeadLoad;

        /// <summary>
        /// Total gravity load for load combinations.
        /// </summary>
        public double TotalGravityLoad => TotalDeadLoad + LiveLoad;
    }

    /// <summary>
    /// Tributary area information for a specific beam.
    /// </summary>
    public class TributaryAreaData
    {
        public double TributaryWidth { get; set; }   // feet (perpendicular to beam)
        public double TributaryLength { get; set; }  // feet (along beam)
        public double TributaryArea { get; set; }    // square feet

        // For irregular tributary areas
        public double TributaryWidthLeft { get; set; }
        public double TributaryWidthRight { get; set; }

        // Span ratios for load distribution
        public double SpanRatio { get; set; }        // For two-way slab behavior

        // Associated diaphragm
        public DiaphragmData? Diaphragm { get; set; }
    }

    /// <summary>
    /// Building occupancy types per ASCE 7 Table 4-1.
    /// </summary>
    public enum OccupancyType
    {
        // Residential
        Residential = 40,           // 40 psf
        ResidentialPrivate = 40,

        // Office
        Office = 50,                // 50 psf lobbies/first floor
        OfficeTypical = 50,

        // Assembly
        AssemblyFixed = 60,         // Fixed seats
        AssemblyMovable = 100,      // Movable seats

        // Storage
        StorageLight = 125,         // Light storage
        StorageHeavy = 250,         // Heavy storage

        // Retail
        Retail = 75,                // First floor
        RetailUpper = 75,           // Upper floors

        // Industrial
        IndustrialLight = 125,
        IndustrialHeavy = 250,

        // Institutional
        Hospital = 60,
        Schools = 40,

        // Parking
        Parking = 40,               // Passenger vehicles
        ParkingTruck = 250,         // Truck/bus

        // Roof
        RoofOrdinary = 20,          // Ordinary flat roof
        RoofOccupied = 100,         // Roof gardens

        // Special
        Library = 150,              // Stack rooms
        Corridor = 100,             // First floor corridors
        Mechanical = 150,           // Mechanical rooms

        // Default
        Unknown = 50
    }
}
