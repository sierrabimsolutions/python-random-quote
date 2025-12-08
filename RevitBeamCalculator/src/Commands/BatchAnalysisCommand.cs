using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using RevitBeamCalculator.Models;
using RevitBeamCalculator.Services;
using RevitBeamCalculator.UI;

namespace RevitBeamCalculator.Commands
{
    /// <summary>
    /// Command to perform batch analysis on multiple beams.
    /// Can analyze all beams in the current view or a user selection.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class BatchAnalysisCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // Ask user for batch scope
                TaskDialog scopeDialog = new TaskDialog("Batch Analysis Scope");
                scopeDialog.MainInstruction = "Select analysis scope";
                scopeDialog.MainContent = "Choose which beams to analyze:";
                scopeDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Analyze beams in current view");
                scopeDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Analyze selected beams");
                scopeDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Analyze all beams in model");
                scopeDialog.CommonButtons = TaskDialogCommonButtons.Cancel;

                TaskDialogResult result = scopeDialog.Show();

                List<FamilyInstance> beamsToAnalyze = result switch
                {
                    TaskDialogResult.CommandLink1 => GetBeamsInView(doc, uiDoc.ActiveView),
                    TaskDialogResult.CommandLink2 => GetSelectedBeams(doc, uiDoc),
                    TaskDialogResult.CommandLink3 => GetAllBeams(doc),
                    _ => new List<FamilyInstance>()
                };

                if (beamsToAnalyze.Count == 0)
                {
                    TaskDialog.Show("Batch Analysis", "No beams found for analysis.");
                    return Result.Cancelled;
                }

                // Perform batch analysis
                BeamDataExtractor extractor = new BeamDataExtractor(doc);
                DiaphragmService diaphragmService = new DiaphragmService(doc);
                StructuralCalculator calculator = new StructuralCalculator();
                LoadSettings loadSettings = SettingsManager.GetLoadSettings();

                List<BatchAnalysisResult> batchResults = new List<BatchAnalysisResult>();
                int successCount = 0;
                int failCount = 0;

                foreach (FamilyInstance beam in beamsToAnalyze)
                {
                    try
                    {
                        BeamData beamData = extractor.ExtractBeamData(beam);
                        DiaphragmData? diaphragmData = diaphragmService.FindAssociatedDiaphragm(beam);
                        double tributaryWidth = diaphragmService.CalculateTributaryWidth(beam, beamData);

                        CalculationResults calcResults = calculator.AnalyzeBeam(
                            beamData, tributaryWidth, diaphragmData, loadSettings);

                        batchResults.Add(new BatchAnalysisResult
                        {
                            BeamId = beam.Id,
                            BeamMark = beam.LookupParameter("Mark")?.AsString() ?? beam.Id.ToString(),
                            BeamData = beamData,
                            Results = calcResults,
                            Success = true
                        });

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        batchResults.Add(new BatchAnalysisResult
                        {
                            BeamId = beam.Id,
                            BeamMark = beam.LookupParameter("Mark")?.AsString() ?? beam.Id.ToString(),
                            Success = false,
                            ErrorMessage = ex.Message
                        });
                        failCount++;
                    }
                }

                // Show batch results window
                BatchResultsWindow batchWindow = new BatchResultsWindow(batchResults);
                batchWindow.ShowDialog();

                // Optionally write results to model
                if (batchWindow.WriteToModel)
                {
                    WriteAllResultsToModel(doc, batchResults);
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Error", $"Batch analysis failed: {ex.Message}");
                return Result.Failed;
            }
        }

        private List<FamilyInstance> GetBeamsInView(Document doc, View view)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc, view.Id);
            return collector
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.StructuralType == StructuralType.Beam)
                .ToList();
        }

        private List<FamilyInstance> GetSelectedBeams(Document doc, UIDocument uiDoc)
        {
            return uiDoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .OfType<FamilyInstance>()
                .Where(fi => fi.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralFraming)
                .Where(fi => fi.StructuralType == StructuralType.Beam)
                .ToList();
        }

        private List<FamilyInstance> GetAllBeams(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            return collector
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.StructuralType == StructuralType.Beam)
                .ToList();
        }

        private void WriteAllResultsToModel(Document doc, List<BatchAnalysisResult> results)
        {
            using (Transaction trans = new Transaction(doc, "Write Batch Calculation Results"))
            {
                trans.Start();

                foreach (var result in results.Where(r => r.Success && r.Results != null))
                {
                    Element? beam = doc.GetElement(result.BeamId);
                    if (beam != null)
                    {
                        SetParameterIfExists(beam, "BC_DemandCapacityRatio", result.Results!.DemandCapacityRatio);
                        SetParameterIfExists(beam, "BC_DesignStatus", result.Results.PassesAllChecks ? "PASS" : "FAIL");
                    }
                }

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
                }
            }
        }
    }

    /// <summary>
    /// Result container for batch analysis of a single beam.
    /// </summary>
    public class BatchAnalysisResult
    {
        public ElementId BeamId { get; set; } = ElementId.InvalidElementId;
        public string BeamMark { get; set; } = string.Empty;
        public BeamData? BeamData { get; set; }
        public CalculationResults? Results { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
