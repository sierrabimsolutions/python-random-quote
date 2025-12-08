using System;
using System.Collections.Generic;
using System.Linq;
using RevitBeamCalculator.Models;

namespace RevitBeamCalculator.Services
{
    /// <summary>
    /// Performs structural engineering calculations for beams.
    /// Implements AISC 360, ACI 318, and NDS design checks.
    /// </summary>
    public class StructuralCalculator
    {
        private const int DiagramPoints = 21; // Points for moment/shear/deflection diagrams

        /// <summary>
        /// Performs complete beam analysis including loads, internal forces, and design checks.
        /// </summary>
        public CalculationResults AnalyzeBeam(
            BeamData beamData,
            double tributaryWidth,
            DiaphragmData? diaphragm,
            LoadSettings settings)
        {
            CalculationResults results = new CalculationResults
            {
                BeamData = beamData,
                LoadCode = GetLoadCodeString(settings.LoadCode),
                DesignCode = GetDesignCodeString(beamData.Material.Type, settings)
            };

            // Calculate loads
            results.Loads = CalculateLoads(beamData, tributaryWidth, diaphragm, settings);
            results.TributaryData = new TributaryAreaData
            {
                TributaryWidth = tributaryWidth,
                TributaryLength = beamData.Length,
                TributaryArea = tributaryWidth * beamData.Length,
                Diaphragm = diaphragm
            };

            // Determine governing load combination
            List<LoadCombination> combinations = settings.DesignMethod == DesignMethod.LRFD
                ? LoadCombination.GetLRFDCombinations()
                : LoadCombination.GetASDCombinations();

            ApplyLoadCombinations(combinations, results.Loads);
            results.Loads.LoadCombinations = combinations;
            results.Loads.GoverningCombination = combinations.OrderByDescending(c => c.TotalLoad).First();
            results.Loads.GoverningCombination.IsGoverning = true;
            results.GoverningLoadCase = results.Loads.GoverningCombination.Name;

            // Perform structural analysis
            AnalyzeSimplySupported(beamData, results.Loads.GoverningCombination.TotalLoad, results);

            // Generate diagrams
            GenerateDiagrams(beamData, results.Loads, results);

            // Calculate deflections
            CalculateDeflections(beamData, results.Loads, settings, results);

            // Perform design checks based on material type
            switch (beamData.Material.Type)
            {
                case MaterialType.Steel:
                    PerformSteelDesignChecks(beamData, results, settings);
                    break;
                case MaterialType.Concrete:
                    PerformConcreteDesignChecks(beamData, results, settings);
                    break;
                case MaterialType.Wood:
                    PerformWoodDesignChecks(beamData, results, settings);
                    break;
                default:
                    PerformSteelDesignChecks(beamData, results, settings);
                    break;
            }

            // Determine overall pass/fail
            results.DemandCapacityRatio = results.DesignChecks.Max(c => c.Ratio);
            results.GoverningCheck = results.DesignChecks.OrderByDescending(c => c.Ratio).First().Name;
            results.PassesAllChecks = results.DesignChecks.All(c => c.Passes);

            return results;
        }

        private LoadSummary CalculateLoads(BeamData beamData, double tributaryWidth, DiaphragmData? diaphragm, LoadSettings settings)
        {
            LoadSummary loads = new LoadSummary
            {
                TributaryWidth = tributaryWidth
            };

            // Get area loads (psf)
            double deadLoadPsf = 0;
            double liveLoadPsf = 0;
            double sdlPsf = 0;

            if (diaphragm != null)
            {
                deadLoadPsf = diaphragm.SlabSelfWeight;
                sdlPsf = diaphragm.SuperimposedDeadLoad;
                liveLoadPsf = diaphragm.LiveLoad;
            }
            else
            {
                // Use defaults
                double slabWeight = (settings.DefaultSlabThickness / 12) * settings.DefaultConcreteDensity;
                deadLoadPsf = slabWeight;
                sdlPsf = settings.DefaultSuperimposedDeadLoad;
                liveLoadPsf = settings.DefaultLiveLoad;
            }

            loads.DeadLoadPsf = deadLoadPsf + sdlPsf;
            loads.LiveLoadPsf = liveLoadPsf;

            // Convert to line loads (k/ft)
            // Area load (psf) × Tributary width (ft) ÷ 1000 = k/ft
            loads.DeadLoad = (deadLoadPsf * tributaryWidth) / 1000;
            loads.SuperimposedDeadLoad = (sdlPsf * tributaryWidth) / 1000;
            loads.LiveLoad = (liveLoadPsf * tributaryWidth) / 1000;

            // Beam self-weight (if applicable)
            if (settings.IncludeSelfWeight && beamData.Section.WeightPerFoot > 0)
            {
                loads.SelfWeight = beamData.Section.WeightPerFoot / 1000; // plf to klf
            }

            // Total service load
            loads.TotalServiceLoad = loads.DeadLoad + loads.SuperimposedDeadLoad + loads.LiveLoad + loads.SelfWeight;

            return loads;
        }

        private void ApplyLoadCombinations(List<LoadCombination> combinations, LoadSummary loads)
        {
            double totalDead = loads.DeadLoad + loads.SuperimposedDeadLoad + loads.SelfWeight;
            double totalLive = loads.LiveLoad;

            foreach (var combo in combinations)
            {
                combo.TotalLoad = combo.DeadFactor * totalDead + combo.LiveFactor * totalLive;
            }
        }

        private void AnalyzeSimplySupported(BeamData beamData, double uniformLoad, CalculationResults results)
        {
            double L = beamData.Length; // feet
            double w = uniformLoad;     // k/ft

            // Simply supported beam formulas
            // Maximum moment at midspan: M = wL²/8
            results.MaxMoment = w * L * L / 8; // kip-ft
            results.MaxMomentLocation = L / 2;

            // Maximum shear at supports: V = wL/2
            results.MaxShear = w * L / 2; // kips
            results.MaxShearLocation = 0;

            // Reactions
            results.StartReaction = w * L / 2;
            results.EndReaction = w * L / 2;
        }

        private void GenerateDiagrams(BeamData beamData, LoadSummary loads, CalculationResults results)
        {
            double L = beamData.Length;
            double w = results.Loads.GoverningCombination.TotalLoad;
            double dx = L / (DiagramPoints - 1);

            results.MomentDiagram.Clear();
            results.ShearDiagram.Clear();

            for (int i = 0; i < DiagramPoints; i++)
            {
                double x = i * dx;

                // Shear: V(x) = wL/2 - wx
                double shear = w * L / 2 - w * x;
                results.ShearDiagram.Add(new DiagramPoint { Location = x, Value = shear });

                // Moment: M(x) = wx(L-x)/2
                double moment = w * x * (L - x) / 2;
                results.MomentDiagram.Add(new DiagramPoint { Location = x, Value = moment });
            }
        }

        private void CalculateDeflections(BeamData beamData, LoadSummary loads, LoadSettings settings, CalculationResults results)
        {
            double L = beamData.Length * 12;        // inches
            double E = beamData.Material.ModulusOfElasticity; // ksi
            double I = beamData.Section.MomentOfInertiaX;     // in^4

            if (E <= 0 || I <= 0)
            {
                // Cannot calculate deflection without E and I
                results.MaxDeflection = 0;
                return;
            }

            // Convert loads to k/in
            double wDL = (loads.DeadLoad + loads.SuperimposedDeadLoad + loads.SelfWeight) / 12; // k/in
            double wLL = loads.LiveLoad / 12; // k/in
            double wTotal = wDL + wLL;

            // Maximum deflection for simply supported beam: Δ = 5wL⁴/(384EI)
            double deltaLL = 5 * wLL * Math.Pow(L, 4) / (384 * E * I);
            double deltaTotal = 5 * wTotal * Math.Pow(L, 4) / (384 * E * I);

            results.ActualDeflectionLL = deltaLL;
            results.ActualDeflectionTotal = deltaTotal;
            results.MaxDeflection = deltaTotal;
            results.MaxDeflectionLocation = beamData.Length / 2;

            // Allowable deflections
            results.AllowableDeflectionLL = settings.DeflectionCriteria.GetAllowableLiveLoadDeflection(beamData.Length);
            results.AllowableDeflectionTotal = settings.DeflectionCriteria.GetAllowableTotalDeflection(beamData.Length);

            // Generate deflection diagram
            results.DeflectionDiagram.Clear();
            double dx = L / (DiagramPoints - 1);

            for (int i = 0; i < DiagramPoints; i++)
            {
                double x = i * dx; // inches from start

                // Deflection: δ(x) = wx(L³ - 2Lx² + x³) / (24EI)
                double delta = wTotal * x * (Math.Pow(L, 3) - 2 * L * x * x + Math.Pow(x, 3)) / (24 * E * I);
                results.DeflectionDiagram.Add(new DiagramPoint
                {
                    Location = x / 12, // Convert back to feet
                    Value = delta      // inches
                });
            }
        }

        #region Steel Design Checks (AISC 360)

        private void PerformSteelDesignChecks(BeamData beamData, CalculationResults results, LoadSettings settings)
        {
            double Fy = beamData.Material.YieldStrength; // ksi
            double E = beamData.Material.ModulusOfElasticity; // ksi
            double Zx = beamData.Section.PlasticModulusX; // in³
            double Sx = beamData.Section.SectionModulusX; // in³
            double Ix = beamData.Section.MomentOfInertiaX; // in⁴
            double d = beamData.Section.Depth; // in
            double tw = beamData.Section.WebThickness; // in
            double bf = beamData.Section.Width; // in
            double tf = beamData.Section.FlangeThickness; // in
            double ry = beamData.Section.RadiusOfGyrationY; // in

            double phi = settings.DesignMethod == DesignMethod.LRFD ? 0.9 : 1.0;
            double omega = settings.DesignMethod == DesignMethod.ASD ? 1.67 : 1.0;

            // Required strength
            double Mu = results.MaxMoment * 12; // kip-in
            double Vu = results.MaxShear; // kips

            // 1. Flexural Yielding Check (F2.1)
            double Mp = Fy * Zx; // kip-in
            double phiMn_yield = phi * Mp / omega;

            results.DesignChecks.Add(new DesignCheck
            {
                Name = "Flexural Yielding",
                Type = DesignCheckType.FlexuralYielding,
                Demand = Mu,
                Capacity = phiMn_yield,
                Ratio = Mu / phiMn_yield,
                Passes = Mu <= phiMn_yield,
                Units = "kip-in",
                CodeReference = "AISC 360 F2.1"
            });

            // 2. Lateral-Torsional Buckling Check (F2.2)
            double Lb = beamData.Length * 12; // Unbraced length in inches
            if (settings.UseActualUnbracedLength)
            {
                Lb = beamData.Length * 12 * settings.DefaultUnbracedLengthRatio;
            }

            double rts = Math.Sqrt(Math.Sqrt(Ix * beamData.Section.WarpingConstant) / Sx);
            if (rts <= 0) rts = ry * 1.2; // Approximate if Cw not available

            double Lp = 1.76 * ry * Math.Sqrt(E / Fy);
            double c = 1.0; // For doubly symmetric I-shapes
            double Lr = 1.95 * rts * (E / (0.7 * Fy)) * Math.Sqrt(
                beamData.Section.TorsionalConstant * c / (Sx * d) +
                Math.Sqrt(Math.Pow(beamData.Section.TorsionalConstant * c / (Sx * d), 2) +
                         6.76 * Math.Pow(0.7 * Fy / E, 2)));

            if (Lr <= 0) Lr = Lp * 3; // Fallback

            double Mn_ltb;
            if (Lb <= Lp)
            {
                Mn_ltb = Mp; // No LTB
            }
            else if (Lb <= Lr)
            {
                // Inelastic LTB
                double Cb = 1.0; // Conservative, could calculate from moment diagram
                Mn_ltb = Cb * (Mp - (Mp - 0.7 * Fy * Sx) * (Lb - Lp) / (Lr - Lp));
                Mn_ltb = Math.Min(Mn_ltb, Mp);
            }
            else
            {
                // Elastic LTB
                double Cb = 1.0;
                double Fcr = Cb * Math.PI * Math.PI * E / Math.Pow(Lb / rts, 2) *
                    Math.Sqrt(1 + 0.078 * beamData.Section.TorsionalConstant * c / (Sx * d) * Math.Pow(Lb / rts, 2));
                Mn_ltb = Fcr * Sx;
                Mn_ltb = Math.Min(Mn_ltb, Mp);
            }

            double phiMn_ltb = phi * Mn_ltb / omega;

            results.DesignChecks.Add(new DesignCheck
            {
                Name = "Lateral-Torsional Buckling",
                Type = DesignCheckType.LateralTorsionalBuckling,
                Demand = Mu,
                Capacity = phiMn_ltb,
                Ratio = Mu / phiMn_ltb,
                Passes = Mu <= phiMn_ltb,
                Units = "kip-in",
                CodeReference = "AISC 360 F2.2",
                Notes = $"Lb={Lb:F1}in, Lp={Lp:F1}in, Lr={Lr:F1}in"
            });

            // 3. Shear Check (G2.1)
            double Aw = d * tw;
            double Cv1 = 1.0; // For most rolled I-shapes
            double Vn = 0.6 * Fy * Aw * Cv1;
            double phiVn = (settings.DesignMethod == DesignMethod.LRFD ? 1.0 : 1.0 / 1.5) * Vn;

            results.DesignChecks.Add(new DesignCheck
            {
                Name = "Shear Strength",
                Type = DesignCheckType.ShearStrength,
                Demand = Vu,
                Capacity = phiVn,
                Ratio = Vu / phiVn,
                Passes = Vu <= phiVn,
                Units = "kips",
                CodeReference = "AISC 360 G2.1"
            });

            // 4. Deflection Checks
            if (settings.DeflectionCriteria.CheckLiveLoadDeflection)
            {
                results.DesignChecks.Add(new DesignCheck
                {
                    Name = "Live Load Deflection",
                    Type = DesignCheckType.DeflectionLiveLoad,
                    Demand = results.ActualDeflectionLL,
                    Capacity = results.AllowableDeflectionLL,
                    Ratio = results.ActualDeflectionLL / results.AllowableDeflectionLL,
                    Passes = results.ActualDeflectionLL <= results.AllowableDeflectionLL,
                    Units = "in",
                    CodeReference = $"L/{settings.DeflectionCriteria.LiveLoadLimit}"
                });
            }

            if (settings.DeflectionCriteria.CheckTotalDeflection)
            {
                results.DesignChecks.Add(new DesignCheck
                {
                    Name = "Total Load Deflection",
                    Type = DesignCheckType.DeflectionTotalLoad,
                    Demand = results.ActualDeflectionTotal,
                    Capacity = results.AllowableDeflectionTotal,
                    Ratio = results.ActualDeflectionTotal / results.AllowableDeflectionTotal,
                    Passes = results.ActualDeflectionTotal <= results.AllowableDeflectionTotal,
                    Units = "in",
                    CodeReference = $"L/{settings.DeflectionCriteria.TotalLoadLimit}"
                });
            }
        }

        #endregion

        #region Concrete Design Checks (ACI 318)

        private void PerformConcreteDesignChecks(BeamData beamData, CalculationResults results, LoadSettings settings)
        {
            double fc = beamData.Material.CompressiveStrength; // ksi
            double b = beamData.Section.Width;  // in
            double h = beamData.Section.Depth;  // in
            double d = h - 2.5; // Effective depth assuming 2.5" cover

            double phi = settings.DesignMethod == DesignMethod.LRFD ? 0.9 : 1.0;

            double Mu = results.MaxMoment * 12; // kip-in
            double Vu = results.MaxShear; // kips

            // 1. Flexural Capacity (simplified)
            // Assume tension-controlled section
            double rho_max = 0.0181 * fc / 60; // For fy = 60 ksi
            double As_max = rho_max * b * d;
            double a = As_max * 60 / (0.85 * fc * b);
            double Mn = As_max * 60 * (d - a / 2);
            double phiMn = phi * Mn;

            results.DesignChecks.Add(new DesignCheck
            {
                Name = "Flexural Capacity",
                Type = DesignCheckType.FlexuralYielding,
                Demand = Mu,
                Capacity = phiMn,
                Ratio = Mu / phiMn,
                Passes = Mu <= phiMn,
                Units = "kip-in",
                CodeReference = "ACI 318-19 22.2",
                Notes = $"As_max = {As_max:F2} in²"
            });

            // 2. Shear Capacity
            double Vc = 2 * Math.Sqrt(fc * 1000) * b * d / 1000; // kips (convert psi to ksi)
            double phiVc = 0.75 * Vc;

            results.DesignChecks.Add(new DesignCheck
            {
                Name = "Shear Capacity (Concrete)",
                Type = DesignCheckType.ShearStrength,
                Demand = Vu,
                Capacity = phiVc,
                Ratio = Vu / phiVc,
                Passes = Vu <= phiVc,
                Units = "kips",
                CodeReference = "ACI 318-19 22.5",
                Notes = Vu > phiVc ? "Stirrups required" : "Minimum stirrups per 9.6.3"
            });

            // 3. Deflection checks
            if (settings.DeflectionCriteria.CheckTotalDeflection)
            {
                results.DesignChecks.Add(new DesignCheck
                {
                    Name = "Total Load Deflection",
                    Type = DesignCheckType.DeflectionTotalLoad,
                    Demand = results.ActualDeflectionTotal,
                    Capacity = results.AllowableDeflectionTotal,
                    Ratio = results.ActualDeflectionTotal / results.AllowableDeflectionTotal,
                    Passes = results.ActualDeflectionTotal <= results.AllowableDeflectionTotal,
                    Units = "in",
                    CodeReference = "ACI 318-19 Table 24.2.2"
                });
            }
        }

        #endregion

        #region Wood Design Checks (NDS)

        private void PerformWoodDesignChecks(BeamData beamData, CalculationResults results, LoadSettings settings)
        {
            double Fb = beamData.Material.BendingStrength / 1000; // psi to ksi
            double Fv = beamData.Material.ShearStrength / 1000;
            double E = beamData.Material.ModulusOfElasticity;
            double b = beamData.Section.Width;
            double d = beamData.Section.Depth;
            double Sx = b * d * d / 6; // Section modulus for rectangular section

            // Adjustment factors (simplified)
            double CD = 1.0;  // Load duration factor (normal)
            double CM = 1.0;  // Wet service factor
            double Ct = 1.0;  // Temperature factor
            double CL = 1.0;  // Beam stability factor
            double CF = 1.0;  // Size factor
            double Cfu = 1.0; // Flat use factor
            double Ci = 1.0;  // Incising factor
            double Cr = 1.0;  // Repetitive member factor

            double Fb_adj = Fb * CD * CM * Ct * CL * CF * Cfu * Ci * Cr;
            double Fv_adj = Fv * CD * CM * Ct * Ci;

            double Mu = results.MaxMoment * 12; // kip-in
            double Vu = results.MaxShear; // kips

            // 1. Bending Check
            double fb = Mu / Sx; // ksi
            double Mn = Fb_adj * Sx;

            results.DesignChecks.Add(new DesignCheck
            {
                Name = "Bending Stress",
                Type = DesignCheckType.FlexuralYielding,
                Demand = fb,
                Capacity = Fb_adj,
                Ratio = fb / Fb_adj,
                Passes = fb <= Fb_adj,
                Units = "ksi",
                CodeReference = "NDS 3.3.1"
            });

            // 2. Shear Check
            double fv = 1.5 * Vu / (b * d); // ksi
            results.DesignChecks.Add(new DesignCheck
            {
                Name = "Shear Stress",
                Type = DesignCheckType.ShearStrength,
                Demand = fv,
                Capacity = Fv_adj,
                Ratio = fv / Fv_adj,
                Passes = fv <= Fv_adj,
                Units = "ksi",
                CodeReference = "NDS 3.4.2"
            });

            // 3. Deflection Check
            if (settings.DeflectionCriteria.CheckLiveLoadDeflection)
            {
                results.DesignChecks.Add(new DesignCheck
                {
                    Name = "Live Load Deflection",
                    Type = DesignCheckType.DeflectionLiveLoad,
                    Demand = results.ActualDeflectionLL,
                    Capacity = results.AllowableDeflectionLL,
                    Ratio = results.ActualDeflectionLL / results.AllowableDeflectionLL,
                    Passes = results.ActualDeflectionLL <= results.AllowableDeflectionLL,
                    Units = "in",
                    CodeReference = "NDS 3.5.1"
                });
            }
        }

        #endregion

        #region Helper Methods

        private string GetLoadCodeString(LoadCode code) => code switch
        {
            LoadCode.ASCE7_16 => "ASCE 7-16",
            LoadCode.ASCE7_22 => "ASCE 7-22",
            _ => "ASCE 7-22"
        };

        private string GetDesignCodeString(MaterialType material, LoadSettings settings) => material switch
        {
            MaterialType.Steel => settings.SteelCode switch
            {
                SteelCode.AISC360_16 => "AISC 360-16",
                SteelCode.AISC360_22 => "AISC 360-22",
                _ => "AISC 360-22"
            },
            MaterialType.Concrete => settings.ConcreteCode switch
            {
                ConcreteCode.ACI318_14 => "ACI 318-14",
                ConcreteCode.ACI318_19 => "ACI 318-19",
                _ => "ACI 318-19"
            },
            MaterialType.Wood => settings.WoodCode switch
            {
                WoodCode.NDS_2018 => "NDS-2018",
                WoodCode.NDS_2024 => "NDS-2024",
                _ => "NDS-2024"
            },
            _ => "AISC 360-22"
        };

        #endregion
    }
}
