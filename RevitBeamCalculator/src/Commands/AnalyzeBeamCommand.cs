using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RevitBeamCalculator.Models;
using RevitBeamCalculator.Services;
using RevitBeamCalculator.UI;

namespace RevitBeamCalculator.Commands
{
    /// <summary>
    /// Command to analyze a selected beam for gravity loads.
    /// Extracts beam properties, calculates tributary loads from diaphragms,
    /// and performs structural design checks.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AnalyzeBeamCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Prompt user to select a beam
                Reference? beamRef = SelectBeam(uiDoc);
                if (beamRef == null)
                {
                    return Result.Cancelled;
                }

                // Get the beam element
                Element element = doc.GetElement(beamRef);
                if (element is not FamilyInstance beam)
                {
                    TaskDialog.Show("Error", "Selected element is not a valid beam.");
                    return Result.Failed;
                }

                // Verify it's structural framing
                if (beam.Category?.Id.IntegerValue != (int)BuiltInCategory.OST_StructuralFraming)
                {
                    TaskDialog.Show("Error", "Please select a structural framing element (beam).");
                    return Result.Failed;
                }

                // Extract beam data
                BeamDataExtractor extractor = new BeamDataExtractor(doc);
                BeamData beamData = extractor.ExtractBeamData(beam);

                // Find associated diaphragms and calculate tributary area
                DiaphragmService diaphragmService = new DiaphragmService(doc);
                DiaphragmData? diaphragmData = diaphragmService.FindAssociatedDiaphragm(beam);
                double tributaryWidth = diaphragmService.CalculateTributaryWidth(beam, beamData);

                // Get load settings
                LoadSettings loadSettings = SettingsManager.GetLoadSettings();

                // Perform structural analysis
                StructuralCalculator calculator = new StructuralCalculator();
                CalculationResults results = calculator.AnalyzeBeam(
                    beamData,
                    tributaryWidth,
                    diaphragmData,
                    loadSettings
                );

                // Show results dialog
                BeamResultsWindow resultsWindow = new BeamResultsWindow(beamData, results, diaphragmData);
                resultsWindow.ShowDialog();

                // Optionally update Revit parameters with results
                if (resultsWindow.WriteToModel)
                {
                    WriteResultsToModel(doc, beam, results);
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Error", $"Analysis failed: {ex.Message}\n\n{ex.StackTrace}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Prompts the user to select a beam element.
        /// </summary>
        private Reference? SelectBeam(UIDocument uiDoc)
        {
            try
            {
                BeamSelectionFilter filter = new BeamSelectionFilter();
                Reference beamRef = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    filter,
                    "Select a beam to analyze"
                );
                return beamRef;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Writes calculation results back to beam parameters in the Revit model.
        /// </summary>
        private void WriteResultsToModel(Document doc, FamilyInstance beam, CalculationResults results)
        {
            using (Transaction trans = new Transaction(doc, "Write Beam Calculation Results"))
            {
                trans.Start();

                // Try to find or create shared parameters for results
                SetParameterIfExists(beam, "BC_DemandCapacityRatio", results.DemandCapacityRatio);
                SetParameterIfExists(beam, "BC_MaxMoment_kipft", results.MaxMoment);
                SetParameterIfExists(beam, "BC_MaxShear_kip", results.MaxShear);
                SetParameterIfExists(beam, "BC_MaxDeflection_in", results.MaxDeflection);
                SetParameterIfExists(beam, "BC_DesignStatus", results.PassesAllChecks ? "PASS" : "FAIL");
                SetParameterIfExists(beam, "BC_GoverningCase", results.GoverningLoadCase);

                trans.Commit();
            }
        }

        private void SetParameterIfExists(Element element, string paramName, object value)
        {
            Parameter? param = element.LookupParameter(paramName);
            if (param != null && !param.IsReadOnly)
            {
                switch (value)
                {
                    case double d:
                        param.Set(d);
                        break;
                    case string s:
                        param.Set(s);
                        break;
                    case int i:
                        param.Set(i);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Selection filter that only allows structural framing (beams) to be selected.
    /// </summary>
    public class BeamSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            // Only allow structural framing elements
            if (elem.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralFraming)
            {
                // Verify it's a beam (horizontal member) not a brace
                if (elem is FamilyInstance fi)
                {
                    StructuralType structType = fi.StructuralType;
                    return structType == StructuralType.Beam;
                }
            }
            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }
}
