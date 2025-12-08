# Revit Beam Calculator Add-in

A Revit add-in for performing gravity beam calculations based on diaphragm definitions and material properties extracted directly from the Revit model.

## Features

- **Single Beam Analysis**: Click on any structural beam to perform complete gravity load analysis
- **Batch Analysis**: Analyze multiple beams at once (selected, in view, or entire model)
- **Automatic Tributary Width Calculation**: Calculates tributary area from surrounding beams
- **Diaphragm Recognition**: Extracts load information from Revit Area elements
- **Multi-Material Support**:
  - Steel beams (AISC 360)
  - Concrete beams (ACI 318)
  - Wood beams (NDS)
- **Code-Compliant Design Checks**:
  - Flexural strength (yielding, lateral-torsional buckling)
  - Shear strength
  - Deflection limits
- **LRFD and ASD** design methods
- **Report Generation**: Export to PDF and Excel

## Installation

1. Build the solution in Visual Studio
2. Copy `RevitBeamCalculator.dll` to your Revit add-ins folder
3. Copy `RevitBeamCalculator.addin` to `%APPDATA%\Autodesk\Revit\Addins\20XX\`
4. Restart Revit

## Usage

### Analyze Single Beam
1. Click "Analyze Beam" on the Structural Calcs ribbon tab
2. Select a structural beam in the model
3. Review results in the dialog
4. Optionally export to PDF/Excel or write results to model parameters

### Batch Analysis
1. Click "Batch Analysis"
2. Choose scope (selected beams, current view, or all beams)
3. Review summary and individual results
4. Export full report

### Settings
Configure default loads, design codes, deflection criteria, and calculation options via the Settings button.

## Project Structure

```
RevitBeamCalculator/
├── src/
│   ├── Application.cs           # Main Revit application entry point
│   ├── Commands/
│   │   ├── AnalyzeBeamCommand.cs    # Single beam analysis
│   │   ├── BatchAnalysisCommand.cs  # Multi-beam analysis
│   │   └── SettingsCommand.cs       # Settings dialog
│   ├── Models/
│   │   ├── BeamData.cs              # Beam geometry and properties
│   │   ├── DiaphragmData.cs         # Floor/diaphragm data
│   │   ├── CalculationResults.cs    # Analysis output
│   │   └── LoadSettings.cs          # User settings
│   ├── Services/
│   │   ├── BeamDataExtractor.cs     # Extract data from Revit
│   │   ├── DiaphragmService.cs      # Tributary area calculations
│   │   ├── StructuralCalculator.cs  # Engineering calculations
│   │   └── ReportGenerator.cs       # PDF/Excel export
│   └── UI/
│       ├── BeamResultsWindow.xaml   # Results display
│       ├── SettingsWindow.xaml      # Settings UI
│       └── BatchResultsWindow.xaml  # Batch results UI
├── resources/                    # Icons and templates
├── RevitBeamCalculator.addin    # Revit manifest
└── RevitBeamCalculator.sln      # Solution file
```

## Requirements

- Revit 2022 or later
- .NET Framework 4.8
- Visual Studio 2019 or later

## Dependencies

- **OxyPlot**: Moment/shear diagram visualization
- **EPPlus**: Excel report generation
- **QuestPDF**: PDF report generation

## Design Codes Supported

| Material | Design Code | Editions |
|----------|-------------|----------|
| Steel | AISC 360 | 2016, 2022 |
| Concrete | ACI 318 | 2014, 2019 |
| Wood | NDS | 2018, 2024 |
| Loads | ASCE 7 | 2016, 2022 |

## Calculations Performed

### Load Calculation
- Tributary width from beam spacing
- Dead load from slab self-weight + superimposed
- Live load from occupancy type
- Self-weight from beam section

### Structural Analysis
- Simply supported beam analysis
- Moment and shear diagrams
- Deflection calculations

### Design Checks
- **Steel**: Flexural yielding, LTB, shear, deflection
- **Concrete**: Flexural capacity, shear, deflection
- **Wood**: Bending stress, shear stress, deflection

## License

Internal use only. Contact the development team for licensing information.

## Contributing

Please submit issues and feature requests through the internal tracking system.
