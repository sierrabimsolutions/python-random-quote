using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RevitBeamCalculator.Commands;
using RevitBeamCalculator.Models;

namespace RevitBeamCalculator.Services
{
    /// <summary>
    /// Generates PDF and Excel reports for beam analysis results.
    /// </summary>
    public class ReportGenerator
    {
        private readonly string _outputFolder;

        public ReportGenerator()
        {
            _outputFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "RevitBeamCalculator",
                "Reports"
            );

            if (!Directory.Exists(_outputFolder))
            {
                Directory.CreateDirectory(_outputFolder);
            }

            // Set EPPlus license context (required for EPPlus 5+)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            // Set QuestPDF license
            QuestPDF.Settings.License = LicenseType.Community;
        }

        #region PDF Report Generation

        public string GeneratePdfReport(BeamData beamData, CalculationResults results, DiaphragmData? diaphragm)
        {
            string fileName = $"BeamCalc_{beamData.Mark}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string filePath = Path.Combine(_outputFolder, fileName);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(0.75f, Unit.Inch);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(c => ComposeHeader(c, beamData, results));
                    page.Content().Element(c => ComposeContent(c, beamData, results, diaphragm));
                    page.Footer().Element(ComposeFooter);
                });
            }).GeneratePdf(filePath);

            return filePath;
        }

        private void ComposeHeader(IContainer container, BeamData beamData, CalculationResults results)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("BEAM ANALYSIS REPORT").FontSize(18).Bold();
                    col.Item().Text($"Beam: {beamData.DisplayName}").FontSize(12);
                    col.Item().Text($"Section: {beamData.TypeName}").FontSize(10).FontColor(Colors.Grey.Darken1);
                });

                row.ConstantItem(100).AlignRight().Column(col =>
                {
                    string status = results.PassesAllChecks ? "PASS" : "FAIL";
                    string color = results.PassesAllChecks ? Colors.Green.Darken1 : Colors.Red.Darken1;
                    col.Item().Text(status).FontSize(24).Bold().FontColor(color);
                    col.Item().Text($"D/C: {results.DemandCapacityRatio:F2}").FontSize(12);
                });
            });

            container.PaddingBottom(10).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);
        }

        private void ComposeContent(IContainer container, BeamData beamData, CalculationResults results, DiaphragmData? diaphragm)
        {
            container.PaddingVertical(10).Column(col =>
            {
                // Beam Properties Section
                col.Item().Text("BEAM PROPERTIES").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
                col.Item().PaddingLeft(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                    });

                    table.Cell().Text("Length:").Bold();
                    table.Cell().Text($"{beamData.Length:F2} ft");
                    table.Cell().Text("Material:").Bold();
                    table.Cell().Text($"{beamData.Material.Name}");

                    table.Cell().Text("Fy:").Bold();
                    table.Cell().Text($"{beamData.Material.YieldStrength:F0} ksi");
                    table.Cell().Text("E:").Bold();
                    table.Cell().Text($"{beamData.Material.ModulusOfElasticity:F0} ksi");

                    table.Cell().Text("Ix:").Bold();
                    table.Cell().Text($"{beamData.Section.MomentOfInertiaX:F1} in⁴");
                    table.Cell().Text("Zx:").Bold();
                    table.Cell().Text($"{beamData.Section.PlasticModulusX:F1} in³");
                });

                col.Item().PaddingTop(15);

                // Applied Loads Section
                col.Item().Text("APPLIED LOADS").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
                col.Item().PaddingLeft(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                    });

                    table.Cell().Text("Tributary Width:").Bold();
                    table.Cell().Text($"{results.TributaryData.TributaryWidth:F2} ft");
                    table.Cell().Text("Occupancy:").Bold();
                    table.Cell().Text(diaphragm?.Occupancy.ToString() ?? "Office");

                    table.Cell().Text("Dead Load:").Bold();
                    table.Cell().Text($"{results.Loads.DeadLoad + results.Loads.SuperimposedDeadLoad:F3} k/ft");
                    table.Cell().Text("Live Load:").Bold();
                    table.Cell().Text($"{results.Loads.LiveLoad:F3} k/ft");

                    table.Cell().Text("Governing:").Bold();
                    table.Cell().ColumnSpan(3).Text($"{results.Loads.GoverningCombination.Name}: wu = {results.Loads.GoverningCombination.TotalLoad:F3} k/ft");
                });

                col.Item().PaddingTop(15);

                // Analysis Results Section
                col.Item().Text("ANALYSIS RESULTS").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
                col.Item().PaddingLeft(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                    });

                    table.Cell().Text("Max Moment:").Bold();
                    table.Cell().Text($"{results.MaxMoment:F2} kip-ft");
                    table.Cell().Text("Location:").Bold();
                    table.Cell().Text($"@ {results.MaxMomentLocation:F1} ft");

                    table.Cell().Text("Max Shear:").Bold();
                    table.Cell().Text($"{results.MaxShear:F2} kips");
                    table.Cell().Text("Location:").Bold();
                    table.Cell().Text("@ supports");

                    table.Cell().Text("Max Deflection:").Bold();
                    table.Cell().Text($"{results.MaxDeflection:F3} in");
                    table.Cell().Text("Limit:").Bold();
                    table.Cell().Text($"{results.AllowableDeflectionTotal:F3} in");
                });

                col.Item().PaddingTop(15);

                // Design Checks Section
                col.Item().Text("DESIGN CHECKS").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
                col.Item().PaddingLeft(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1.5f);
                    });

                    // Header
                    table.Cell().Background(Colors.Grey.Lighten3).Text("Check").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Text("Demand").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Text("Capacity").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Text("D/C").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Text("Status").Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Text("Reference").Bold();

                    foreach (var check in results.DesignChecks)
                    {
                        table.Cell().Text(check.Name);
                        table.Cell().Text($"{check.Demand:F2}");
                        table.Cell().Text($"{check.Capacity:F2}");
                        table.Cell().Text($"{check.Ratio:F2}");
                        table.Cell().Text(check.Passes ? "OK" : "NG")
                            .FontColor(check.Passes ? Colors.Green.Darken1 : Colors.Red.Darken1)
                            .Bold();
                        table.Cell().Text(check.CodeReference).FontSize(8);
                    }
                });

                col.Item().PaddingTop(20);

                // Summary
                col.Item().Background(Colors.Grey.Lighten4).Padding(10).Row(row =>
                {
                    row.RelativeItem().Text($"Design Code: {results.DesignCode}").FontSize(9);
                    row.RelativeItem().Text($"Load Code: {results.LoadCode}").FontSize(9);
                    row.RelativeItem().AlignRight().Text($"Max D/C: {results.DemandCapacityRatio:F2}").Bold();
                });
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(8);
                row.RelativeItem().AlignCenter().Text("Revit Beam Calculator").FontSize(8);
                row.RelativeItem().AlignRight().Text(x =>
                {
                    x.Span("Page ").FontSize(8);
                    x.CurrentPageNumber().FontSize(8);
                    x.Span(" of ").FontSize(8);
                    x.TotalPages().FontSize(8);
                });
            });
        }

        public string GenerateBatchPdfReport(List<BatchAnalysisResult> results)
        {
            string fileName = $"BatchAnalysis_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string filePath = Path.Combine(_outputFolder, fileName);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter.Landscape());
                    page.Margin(0.5f, Unit.Inch);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("BATCH BEAM ANALYSIS REPORT").FontSize(16).Bold();
                        col.Item().Text($"Total Beams: {results.Count} | Pass: {results.Count(r => r.Success && r.Results?.PassesAllChecks == true)} | Fail: {results.Count(r => r.Success && r.Results?.PassesAllChecks == false)}").FontSize(10);
                        col.Item().PaddingBottom(10).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);
                    });

                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(1.5f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(0.8f);
                            cols.RelativeColumn(2);
                        });

                        // Header
                        table.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Mark").Bold();
                        table.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Section").Bold();
                        table.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Length").Bold();
                        table.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Max Moment").Bold();
                        table.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Max Shear").Bold();
                        table.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("D/C").Bold();
                        table.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Status").Bold();
                        table.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Governing").Bold();

                        foreach (var result in results)
                        {
                            table.Cell().Padding(3).Text(result.BeamMark);
                            table.Cell().Padding(3).Text(result.BeamData?.TypeName ?? "N/A");
                            table.Cell().Padding(3).Text(result.BeamData != null ? $"{result.BeamData.Length:F1} ft" : "N/A");
                            table.Cell().Padding(3).Text(result.Results != null ? $"{result.Results.MaxMoment:F1} k-ft" : "N/A");
                            table.Cell().Padding(3).Text(result.Results != null ? $"{result.Results.MaxShear:F1} k" : "N/A");
                            table.Cell().Padding(3).Text(result.Results != null ? $"{result.Results.DemandCapacityRatio:F2}" : "N/A");

                            string status = !result.Success ? "ERROR" : (result.Results?.PassesAllChecks == true ? "PASS" : "FAIL");
                            string color = !result.Success ? Colors.Orange.Darken1 : (result.Results?.PassesAllChecks == true ? Colors.Green.Darken1 : Colors.Red.Darken1);
                            table.Cell().Padding(3).Text(status).FontColor(color).Bold();

                            table.Cell().Padding(3).Text(result.Success ? result.Results?.GoverningCheck ?? "" : result.ErrorMessage ?? "Error");
                        }
                    });

                    page.Footer().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(8);
                });
            }).GeneratePdf(filePath);

            return filePath;
        }

        #endregion

        #region Excel Report Generation

        public string GenerateExcelReport(BeamData beamData, CalculationResults results, DiaphragmData? diaphragm)
        {
            string fileName = $"BeamCalc_{beamData.Mark}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            string filePath = Path.Combine(_outputFolder, fileName);

            using (ExcelPackage package = new ExcelPackage())
            {
                // Summary Sheet
                var summarySheet = package.Workbook.Worksheets.Add("Summary");
                PopulateSummarySheet(summarySheet, beamData, results, diaphragm);

                // Detailed Calculations Sheet
                var calcSheet = package.Workbook.Worksheets.Add("Calculations");
                PopulateCalculationsSheet(calcSheet, beamData, results);

                // Design Checks Sheet
                var checksSheet = package.Workbook.Worksheets.Add("Design Checks");
                PopulateDesignChecksSheet(checksSheet, results);

                package.SaveAs(new FileInfo(filePath));
            }

            return filePath;
        }

        private void PopulateSummarySheet(ExcelWorksheet sheet, BeamData beamData, CalculationResults results, DiaphragmData? diaphragm)
        {
            int row = 1;

            // Title
            sheet.Cells[row, 1].Value = "BEAM ANALYSIS SUMMARY";
            sheet.Cells[row, 1].Style.Font.Size = 16;
            sheet.Cells[row, 1].Style.Font.Bold = true;
            row += 2;

            // Status
            sheet.Cells[row, 1].Value = "Status:";
            sheet.Cells[row, 2].Value = results.PassesAllChecks ? "PASS" : "FAIL";
            sheet.Cells[row, 2].Style.Font.Bold = true;
            sheet.Cells[row, 2].Style.Font.Color.SetColor(
                results.PassesAllChecks ? System.Drawing.Color.Green : System.Drawing.Color.Red);
            row++;

            sheet.Cells[row, 1].Value = "Max D/C Ratio:";
            sheet.Cells[row, 2].Value = results.DemandCapacityRatio;
            sheet.Cells[row, 2].Style.Numberformat.Format = "0.00";
            row += 2;

            // Beam Properties
            sheet.Cells[row, 1].Value = "BEAM PROPERTIES";
            sheet.Cells[row, 1].Style.Font.Bold = true;
            row++;

            sheet.Cells[row, 1].Value = "Mark:";
            sheet.Cells[row, 2].Value = beamData.Mark;
            row++;
            sheet.Cells[row, 1].Value = "Section:";
            sheet.Cells[row, 2].Value = beamData.TypeName;
            row++;
            sheet.Cells[row, 1].Value = "Length (ft):";
            sheet.Cells[row, 2].Value = beamData.Length;
            row++;
            sheet.Cells[row, 1].Value = "Material:";
            sheet.Cells[row, 2].Value = beamData.Material.Name;
            row++;
            sheet.Cells[row, 1].Value = "Fy (ksi):";
            sheet.Cells[row, 2].Value = beamData.Material.YieldStrength;
            row += 2;

            // Loads
            sheet.Cells[row, 1].Value = "APPLIED LOADS";
            sheet.Cells[row, 1].Style.Font.Bold = true;
            row++;

            sheet.Cells[row, 1].Value = "Tributary Width (ft):";
            sheet.Cells[row, 2].Value = results.TributaryData.TributaryWidth;
            row++;
            sheet.Cells[row, 1].Value = "Dead Load (k/ft):";
            sheet.Cells[row, 2].Value = results.Loads.DeadLoad + results.Loads.SuperimposedDeadLoad;
            row++;
            sheet.Cells[row, 1].Value = "Live Load (k/ft):";
            sheet.Cells[row, 2].Value = results.Loads.LiveLoad;
            row++;
            sheet.Cells[row, 1].Value = "Factored Load (k/ft):";
            sheet.Cells[row, 2].Value = results.Loads.GoverningCombination.TotalLoad;
            row += 2;

            // Results
            sheet.Cells[row, 1].Value = "ANALYSIS RESULTS";
            sheet.Cells[row, 1].Style.Font.Bold = true;
            row++;

            sheet.Cells[row, 1].Value = "Max Moment (kip-ft):";
            sheet.Cells[row, 2].Value = results.MaxMoment;
            row++;
            sheet.Cells[row, 1].Value = "Max Shear (kips):";
            sheet.Cells[row, 2].Value = results.MaxShear;
            row++;
            sheet.Cells[row, 1].Value = "Max Deflection (in):";
            sheet.Cells[row, 2].Value = results.MaxDeflection;
            row++;

            sheet.Cells.AutoFitColumns();
        }

        private void PopulateCalculationsSheet(ExcelWorksheet sheet, BeamData beamData, CalculationResults results)
        {
            int row = 1;

            sheet.Cells[row, 1].Value = "DETAILED CALCULATIONS";
            sheet.Cells[row, 1].Style.Font.Bold = true;
            row += 2;

            // Section Properties
            sheet.Cells[row, 1].Value = "Section Properties";
            sheet.Cells[row, 1].Style.Font.Bold = true;
            row++;

            sheet.Cells[row, 1].Value = "Parameter";
            sheet.Cells[row, 2].Value = "Value";
            sheet.Cells[row, 3].Value = "Units";
            row++;

            sheet.Cells[row, 1].Value = "Depth (d)";
            sheet.Cells[row, 2].Value = beamData.Section.Depth;
            sheet.Cells[row, 3].Value = "in";
            row++;

            sheet.Cells[row, 1].Value = "Width (bf)";
            sheet.Cells[row, 2].Value = beamData.Section.Width;
            sheet.Cells[row, 3].Value = "in";
            row++;

            sheet.Cells[row, 1].Value = "Area (A)";
            sheet.Cells[row, 2].Value = beamData.Section.Area;
            sheet.Cells[row, 3].Value = "in²";
            row++;

            sheet.Cells[row, 1].Value = "Ix";
            sheet.Cells[row, 2].Value = beamData.Section.MomentOfInertiaX;
            sheet.Cells[row, 3].Value = "in⁴";
            row++;

            sheet.Cells[row, 1].Value = "Sx";
            sheet.Cells[row, 2].Value = beamData.Section.SectionModulusX;
            sheet.Cells[row, 3].Value = "in³";
            row++;

            sheet.Cells[row, 1].Value = "Zx";
            sheet.Cells[row, 2].Value = beamData.Section.PlasticModulusX;
            sheet.Cells[row, 3].Value = "in³";
            row++;

            sheet.Cells.AutoFitColumns();
        }

        private void PopulateDesignChecksSheet(ExcelWorksheet sheet, CalculationResults results)
        {
            int row = 1;

            // Headers
            sheet.Cells[row, 1].Value = "Check";
            sheet.Cells[row, 2].Value = "Demand";
            sheet.Cells[row, 3].Value = "Capacity";
            sheet.Cells[row, 4].Value = "D/C Ratio";
            sheet.Cells[row, 5].Value = "Status";
            sheet.Cells[row, 6].Value = "Reference";

            using (var range = sheet.Cells[row, 1, row, 6])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            }
            row++;

            foreach (var check in results.DesignChecks)
            {
                sheet.Cells[row, 1].Value = check.Name;
                sheet.Cells[row, 2].Value = check.Demand;
                sheet.Cells[row, 3].Value = check.Capacity;
                sheet.Cells[row, 4].Value = check.Ratio;
                sheet.Cells[row, 5].Value = check.Passes ? "OK" : "NG";
                sheet.Cells[row, 6].Value = check.CodeReference;

                sheet.Cells[row, 5].Style.Font.Color.SetColor(
                    check.Passes ? System.Drawing.Color.Green : System.Drawing.Color.Red);

                row++;
            }

            sheet.Cells.AutoFitColumns();
        }

        public string GenerateBatchExcelReport(List<BatchAnalysisResult> results)
        {
            string fileName = $"BatchAnalysis_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            string filePath = Path.Combine(_outputFolder, fileName);

            using (ExcelPackage package = new ExcelPackage())
            {
                var sheet = package.Workbook.Worksheets.Add("Batch Results");

                int row = 1;

                // Summary
                sheet.Cells[row, 1].Value = "BATCH ANALYSIS RESULTS";
                sheet.Cells[row, 1].Style.Font.Size = 14;
                sheet.Cells[row, 1].Style.Font.Bold = true;
                row++;

                sheet.Cells[row, 1].Value = $"Total: {results.Count}";
                sheet.Cells[row, 2].Value = $"Pass: {results.Count(r => r.Success && r.Results?.PassesAllChecks == true)}";
                sheet.Cells[row, 3].Value = $"Fail: {results.Count(r => r.Success && r.Results?.PassesAllChecks == false)}";
                sheet.Cells[row, 4].Value = $"Errors: {results.Count(r => !r.Success)}";
                row += 2;

                // Headers
                sheet.Cells[row, 1].Value = "Mark";
                sheet.Cells[row, 2].Value = "Section";
                sheet.Cells[row, 3].Value = "Length (ft)";
                sheet.Cells[row, 4].Value = "Moment (k-ft)";
                sheet.Cells[row, 5].Value = "Shear (k)";
                sheet.Cells[row, 6].Value = "D/C Ratio";
                sheet.Cells[row, 7].Value = "Status";
                sheet.Cells[row, 8].Value = "Governing Check";

                using (var range = sheet.Cells[row, 1, row, 8])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                }
                row++;

                foreach (var result in results)
                {
                    sheet.Cells[row, 1].Value = result.BeamMark;
                    sheet.Cells[row, 2].Value = result.BeamData?.TypeName ?? "N/A";
                    sheet.Cells[row, 3].Value = result.BeamData?.Length;
                    sheet.Cells[row, 4].Value = result.Results?.MaxMoment;
                    sheet.Cells[row, 5].Value = result.Results?.MaxShear;
                    sheet.Cells[row, 6].Value = result.Results?.DemandCapacityRatio;

                    string status = !result.Success ? "ERROR" : (result.Results?.PassesAllChecks == true ? "PASS" : "FAIL");
                    sheet.Cells[row, 7].Value = status;
                    sheet.Cells[row, 7].Style.Font.Color.SetColor(
                        !result.Success ? System.Drawing.Color.Orange :
                        (result.Results?.PassesAllChecks == true ? System.Drawing.Color.Green : System.Drawing.Color.Red));

                    sheet.Cells[row, 8].Value = result.Success ? result.Results?.GoverningCheck : result.ErrorMessage;
                    row++;
                }

                sheet.Cells.AutoFitColumns();
                package.SaveAs(new FileInfo(filePath));
            }

            return filePath;
        }

        #endregion
    }
}
