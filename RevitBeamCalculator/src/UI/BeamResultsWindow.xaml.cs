using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using RevitBeamCalculator.Models;
using RevitBeamCalculator.Services;

namespace RevitBeamCalculator.UI
{
    /// <summary>
    /// Window displaying beam analysis results.
    /// </summary>
    public partial class BeamResultsWindow : Window, INotifyPropertyChanged
    {
        private readonly BeamData _beamData;
        private readonly CalculationResults _results;
        private readonly DiaphragmData? _diaphragm;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool WriteToModel => WriteToModelCheckbox?.IsChecked ?? false;

        // Bindable Properties
        public string BeamDisplayName => _beamData.DisplayName;
        public string BeamTypeName => $"{_beamData.FamilyName}: {_beamData.TypeName}";
        public string StatusText => _results.PassesAllChecks ? "PASS" : "FAIL";
        public Brush StatusBackground => _results.PassesAllChecks
            ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
            : new SolidColorBrush(Color.FromRgb(244, 67, 54));

        public string BeamLength => $"{_beamData.Length:F2} ft";
        public string SectionName => _beamData.Section.ShapeName;
        public string MaterialName => $"{_beamData.Material.Name} ({_beamData.Material.Type})";
        public string LevelName => _beamData.LevelName;
        public string SupportCondition => $"{_beamData.StartSupport} - {_beamData.EndSupport}";

        public string TributaryWidth => $"{_results.TributaryData.TributaryWidth:F2} ft";
        public string DeadLoad => $"{_results.Loads.DeadLoad + _results.Loads.SuperimposedDeadLoad:F3} k/ft";
        public string LiveLoad => $"{_results.Loads.LiveLoad:F3} k/ft";
        public string TotalServiceLoad => $"{_results.Loads.TotalServiceLoad:F3} k/ft";
        public string GoverningCombo => _results.Loads.GoverningCombination.Name;

        public string MaxMoment => $"{_results.MaxMoment:F2} kip-ft @ {_results.MaxMomentLocation:F1} ft";
        public string MaxShear => $"{_results.MaxShear:F2} kips @ support";
        public string MaxDeflection => $"{_results.MaxDeflection:F3} in (L/{(int)(_beamData.Length * 12 / _results.MaxDeflection)})";
        public string Reactions => $"R1 = R2 = {_results.StartReaction:F2} kips";

        public string MaxDCRatio => $"{_results.DemandCapacityRatio:F2}";
        public Brush DCRatioColor => _results.DemandCapacityRatio <= 1.0
            ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
            : new SolidColorBrush(Color.FromRgb(244, 67, 54));
        public string GoverningCheck => $"Governing: {_results.GoverningCheck}";
        public string DesignCode => $"Per {_results.DesignCode}";

        public BeamResultsWindow(BeamData beamData, CalculationResults results, DiaphragmData? diaphragm)
        {
            _beamData = beamData;
            _results = results;
            _diaphragm = diaphragm;

            InitializeComponent();
            DataContext = this;

            PopulateDesignChecks();
            CreateDiagrams();
            GenerateDetailedCalculations();
        }

        private void PopulateDesignChecks()
        {
            var checkDisplays = _results.DesignChecks.Select(c => new DesignCheckDisplay
            {
                Name = c.Name,
                DemandDisplay = $"{c.Demand:F2} {c.Units}",
                CapacityDisplay = $"{c.Capacity:F2} {c.Units}",
                RatioDisplay = $"{c.Ratio:F2}",
                StatusDisplay = c.Passes ? "OK" : "NG",
                CodeReference = c.CodeReference,
                Passes = c.Passes
            }).ToList();

            DesignChecksGrid.ItemsSource = checkDisplays;
        }

        private void CreateDiagrams()
        {
            // Moment Diagram
            var momentModel = new PlotModel { Title = "Moment Diagram (kip-ft)" };
            momentModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Distance (ft)" });
            momentModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Moment (kip-ft)" });

            var momentSeries = new AreaSeries
            {
                Color = OxyColor.FromRgb(33, 150, 243),
                Fill = OxyColor.FromArgb(100, 33, 150, 243)
            };

            foreach (var point in _results.MomentDiagram)
            {
                momentSeries.Points.Add(new DataPoint(point.Location, point.Value));
            }
            momentModel.Series.Add(momentSeries);
            MomentPlot.Model = momentModel;

            // Shear Diagram
            var shearModel = new PlotModel { Title = "Shear Diagram (kips)" };
            shearModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Distance (ft)" });
            shearModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Shear (kips)" });

            var shearSeries = new AreaSeries
            {
                Color = OxyColor.FromRgb(76, 175, 80),
                Fill = OxyColor.FromArgb(100, 76, 175, 80)
            };

            foreach (var point in _results.ShearDiagram)
            {
                shearSeries.Points.Add(new DataPoint(point.Location, point.Value));
            }
            shearModel.Series.Add(shearSeries);
            ShearPlot.Model = shearModel;

            // Deflection Diagram
            var deflectionModel = new PlotModel { Title = "Deflection Diagram (in)" };
            deflectionModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Distance (ft)" });
            deflectionModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Deflection (in)" });

            var deflectionSeries = new LineSeries
            {
                Color = OxyColor.FromRgb(255, 152, 0),
                StrokeThickness = 2
            };

            foreach (var point in _results.DeflectionDiagram)
            {
                deflectionSeries.Points.Add(new DataPoint(point.Location, -point.Value)); // Negative for visual
            }
            deflectionModel.Series.Add(deflectionSeries);
            DeflectionPlot.Model = deflectionModel;
        }

        private void GenerateDetailedCalculations()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("                    BEAM ANALYSIS CALCULATIONS");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();

            sb.AppendLine("BEAM DATA");
            sb.AppendLine("─────────────────────────────────────────────────────────────────");
            sb.AppendLine($"  Beam Mark:       {_beamData.Mark}");
            sb.AppendLine($"  Section:         {_beamData.TypeName}");
            sb.AppendLine($"  Length (L):      {_beamData.Length:F2} ft");
            sb.AppendLine($"  Material:        {_beamData.Material.Name}");
            sb.AppendLine($"  Fy:              {_beamData.Material.YieldStrength:F0} ksi");
            sb.AppendLine($"  E:               {_beamData.Material.ModulusOfElasticity:F0} ksi");
            sb.AppendLine();

            sb.AppendLine("SECTION PROPERTIES");
            sb.AppendLine("─────────────────────────────────────────────────────────────────");
            sb.AppendLine($"  Depth (d):       {_beamData.Section.Depth:F2} in");
            sb.AppendLine($"  Width (bf):      {_beamData.Section.Width:F2} in");
            sb.AppendLine($"  Area (A):        {_beamData.Section.Area:F2} in²");
            sb.AppendLine($"  Ix:              {_beamData.Section.MomentOfInertiaX:F1} in⁴");
            sb.AppendLine($"  Sx:              {_beamData.Section.SectionModulusX:F1} in³");
            sb.AppendLine($"  Zx:              {_beamData.Section.PlasticModulusX:F1} in³");
            sb.AppendLine($"  ry:              {_beamData.Section.RadiusOfGyrationY:F2} in");
            sb.AppendLine();

            sb.AppendLine("LOAD CALCULATION");
            sb.AppendLine("─────────────────────────────────────────────────────────────────");
            sb.AppendLine($"  Tributary Width:           {_results.TributaryData.TributaryWidth:F2} ft");
            sb.AppendLine($"  Dead Load (area):          {_results.Loads.DeadLoadPsf:F1} psf");
            sb.AppendLine($"  Live Load (area):          {_results.Loads.LiveLoadPsf:F1} psf");
            sb.AppendLine();
            sb.AppendLine("  Line Loads:");
            sb.AppendLine($"    Dead Load (wD):          {_results.Loads.DeadLoad + _results.Loads.SuperimposedDeadLoad:F4} k/ft");
            sb.AppendLine($"    Live Load (wL):          {_results.Loads.LiveLoad:F4} k/ft");
            sb.AppendLine($"    Self Weight:             {_results.Loads.SelfWeight:F4} k/ft");
            sb.AppendLine($"    Total Service:           {_results.Loads.TotalServiceLoad:F4} k/ft");
            sb.AppendLine();

            sb.AppendLine("LOAD COMBINATIONS (LRFD)");
            sb.AppendLine("─────────────────────────────────────────────────────────────────");
            foreach (var combo in _results.Loads.LoadCombinations)
            {
                string gov = combo.IsGoverning ? " ← GOVERNS" : "";
                sb.AppendLine($"  {combo.Name,-15} wu = {combo.TotalLoad:F4} k/ft{gov}");
            }
            sb.AppendLine();

            sb.AppendLine("STRUCTURAL ANALYSIS (Simply Supported)");
            sb.AppendLine("─────────────────────────────────────────────────────────────────");
            double wu = _results.Loads.GoverningCombination.TotalLoad;
            double L = _beamData.Length;
            sb.AppendLine($"  Governing Load: wu = {wu:F4} k/ft");
            sb.AppendLine();
            sb.AppendLine($"  Maximum Moment:  Mu = wu × L² / 8");
            sb.AppendLine($"                      = {wu:F4} × {L:F2}² / 8");
            sb.AppendLine($"                      = {_results.MaxMoment:F2} kip-ft");
            sb.AppendLine();
            sb.AppendLine($"  Maximum Shear:   Vu = wu × L / 2");
            sb.AppendLine($"                      = {wu:F4} × {L:F2} / 2");
            sb.AppendLine($"                      = {_results.MaxShear:F2} kips");
            sb.AppendLine();

            sb.AppendLine("DESIGN CHECKS");
            sb.AppendLine("─────────────────────────────────────────────────────────────────");
            foreach (var check in _results.DesignChecks)
            {
                string status = check.Passes ? "OK" : "NG";
                sb.AppendLine($"  {check.Name}");
                sb.AppendLine($"    Demand:   {check.Demand:F2} {check.Units}");
                sb.AppendLine($"    Capacity: {check.Capacity:F2} {check.Units}");
                sb.AppendLine($"    D/C:      {check.Ratio:F3} [{status}]");
                sb.AppendLine($"    Ref:      {check.CodeReference}");
                if (!string.IsNullOrEmpty(check.Notes))
                    sb.AppendLine($"    Notes:    {check.Notes}");
                sb.AppendLine();
            }

            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine($"  Design Code:    {_results.DesignCode}");
            sb.AppendLine($"  Load Code:      {_results.LoadCode}");
            sb.AppendLine($"  Overall Status: {((_results.PassesAllChecks ? "PASS" : "FAIL"))}");
            sb.AppendLine($"  Max D/C Ratio:  {_results.DemandCapacityRatio:F2}");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");

            DetailedCalcsText.Text = sb.ToString();
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ReportGenerator generator = new ReportGenerator();
                string filePath = generator.GeneratePdfReport(_beamData, _results, _diaphragm);
                MessageBox.Show($"Report saved to:\n{filePath}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ReportGenerator generator = new ReportGenerator();
                string filePath = generator.GenerateExcelReport(_beamData, _results, _diaphragm);
                MessageBox.Show($"Report saved to:\n{filePath}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Display model for design check data grid.
    /// </summary>
    public class DesignCheckDisplay
    {
        public string Name { get; set; } = string.Empty;
        public string DemandDisplay { get; set; } = string.Empty;
        public string CapacityDisplay { get; set; } = string.Empty;
        public string RatioDisplay { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
        public string CodeReference { get; set; } = string.Empty;
        public bool Passes { get; set; }
    }
}
