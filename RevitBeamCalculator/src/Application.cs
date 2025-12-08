using System;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;

namespace RevitBeamCalculator
{
    /// <summary>
    /// Main application class that implements IExternalApplication.
    /// Creates the ribbon tab and buttons for the Beam Calculator add-in.
    /// </summary>
    public class Application : IExternalApplication
    {
        public static string? AssemblyPath => Assembly.GetExecutingAssembly().Location;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                CreateRibbonTab(application);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Failed to load Revit Beam Calculator: {ex.Message}");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        private void CreateRibbonTab(UIControlledApplication application)
        {
            string tabName = "Structural Calcs";

            // Create the ribbon tab
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Tab already exists, continue
            }

            // Create the ribbon panel
            RibbonPanel panel = application.CreateRibbonPanel(tabName, "Beam Analysis");

            // Add the Analyze Beam button
            PushButtonData analyzeBeamData = new PushButtonData(
                "AnalyzeBeam",
                "Analyze\nBeam",
                AssemblyPath,
                "RevitBeamCalculator.Commands.AnalyzeBeamCommand"
            );
            analyzeBeamData.ToolTip = "Select a beam to perform gravity load analysis based on tributary area from diaphragms.";
            analyzeBeamData.LongDescription = "Click on a structural beam to extract its material properties, " +
                "calculate tributary loads from defined diaphragms, and perform code-compliant design checks.";

            PushButton analyzeButton = panel.AddItem(analyzeBeamData) as PushButton;

            // Add Settings button
            PushButtonData settingsData = new PushButtonData(
                "BeamCalcSettings",
                "Settings",
                AssemblyPath,
                "RevitBeamCalculator.Commands.SettingsCommand"
            );
            settingsData.ToolTip = "Configure calculation settings, default loads, and code preferences.";

            panel.AddItem(settingsData);

            // Add separator and Batch Analysis button
            panel.AddSeparator();

            PushButtonData batchAnalysisData = new PushButtonData(
                "BatchAnalysis",
                "Batch\nAnalysis",
                AssemblyPath,
                "RevitBeamCalculator.Commands.BatchAnalysisCommand"
            );
            batchAnalysisData.ToolTip = "Analyze all beams in the current view or selected beams.";

            panel.AddItem(batchAnalysisData);
        }
    }
}
