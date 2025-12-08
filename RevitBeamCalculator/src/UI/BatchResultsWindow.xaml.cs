using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using RevitBeamCalculator.Commands;
using RevitBeamCalculator.Services;

namespace RevitBeamCalculator.UI
{
    /// <summary>
    /// Window displaying batch analysis results for multiple beams.
    /// </summary>
    public partial class BatchResultsWindow : Window
    {
        private readonly List<BatchAnalysisResult> _results;

        public bool WriteToModel => WriteToModelCheckbox?.IsChecked ?? false;

        public BatchResultsWindow(List<BatchAnalysisResult> results)
        {
            _results = results;
            InitializeComponent();
            LoadResults();
        }

        private void LoadResults()
        {
            // Calculate summary statistics
            int total = _results.Count;
            int passed = _results.Count(r => r.Success && r.Results?.PassesAllChecks == true);
            int failed = _results.Count(r => r.Success && r.Results?.PassesAllChecks == false);
            int errors = _results.Count(r => !r.Success);

            TotalBeamsText.Text = total.ToString();
            PassedText.Text = passed.ToString();
            FailedText.Text = failed.ToString();
            ErrorsText.Text = errors.ToString();

            // Create display models
            var displayResults = _results.Select(r => new BatchResultDisplay
            {
                BeamMark = r.BeamMark,
                SectionName = r.BeamData?.TypeName ?? "N/A",
                Length = r.BeamData != null ? $"{r.BeamData.Length:F1}" : "N/A",
                MaxMoment = r.Results != null ? $"{r.Results.MaxMoment:F1} k-ft" : "N/A",
                MaxShear = r.Results != null ? $"{r.Results.MaxShear:F1} k" : "N/A",
                DCRatio = r.Results != null ? $"{r.Results.DemandCapacityRatio:F2}" : "N/A",
                Status = GetStatus(r),
                StatusColor = GetStatusBrush(r),
                GoverningCheck = r.Success && r.Results != null ? r.Results.GoverningCheck : (r.ErrorMessage ?? "Analysis Error")
            }).ToList();

            ResultsGrid.ItemsSource = displayResults;
        }

        private string GetStatus(BatchAnalysisResult result)
        {
            if (!result.Success) return "ERROR";
            if (result.Results == null) return "N/A";
            return result.Results.PassesAllChecks ? "PASS" : "FAIL";
        }

        private Brush GetStatusBrush(BatchAnalysisResult result)
        {
            if (!result.Success) return new SolidColorBrush(Color.FromRgb(255, 152, 0));
            if (result.Results == null) return new SolidColorBrush(Colors.Gray);
            return result.Results.PassesAllChecks
                ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
                : new SolidColorBrush(Color.FromRgb(244, 67, 54));
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ReportGenerator generator = new ReportGenerator();
                string filePath = generator.GenerateBatchExcelReport(_results);
                MessageBox.Show($"Report saved to:\n{filePath}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ReportGenerator generator = new ReportGenerator();
                string filePath = generator.GenerateBatchPdfReport(_results);
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
    }

    /// <summary>
    /// Display model for batch results data grid.
    /// </summary>
    public class BatchResultDisplay
    {
        public string BeamMark { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string Length { get; set; } = string.Empty;
        public string MaxMoment { get; set; } = string.Empty;
        public string MaxShear { get; set; } = string.Empty;
        public string DCRatio { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public Brush StatusColor { get; set; } = Brushes.Gray;
        public string GoverningCheck { get; set; } = string.Empty;
    }
}
