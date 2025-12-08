using System;
using System.Windows;
using System.Windows.Controls;
using RevitBeamCalculator.Models;

namespace RevitBeamCalculator.UI
{
    /// <summary>
    /// Settings window for configuring beam calculation parameters.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private LoadSettings _settings;

        public SettingsWindow()
        {
            InitializeComponent();
            _settings = SettingsManager.GetLoadSettings();
            LoadSettingsToUI();
        }

        private void LoadSettingsToUI()
        {
            // Default Loads
            SDLTextBox.Text = _settings.DefaultSuperimposedDeadLoad.ToString();
            LLTextBox.Text = _settings.DefaultLiveLoad.ToString();
            SlabThicknessTextBox.Text = _settings.DefaultSlabThickness.ToString();
            ConcreteDensityTextBox.Text = _settings.DefaultConcreteDensity.ToString();

            // Design Codes
            SelectComboBoxItem(DesignMethodCombo, _settings.DesignMethod.ToString());
            SelectComboBoxItem(SteelCodeCombo, _settings.SteelCode.ToString());
            SelectComboBoxItem(ConcreteCodeCombo, _settings.ConcreteCode.ToString());
            SelectComboBoxItem(LoadCodeCombo, _settings.LoadCode.ToString());
            SelectComboBoxItem(WoodCodeCombo, _settings.WoodCode.ToString());

            // Deflection Criteria
            CheckLLDeflection.IsChecked = _settings.DeflectionCriteria.CheckLiveLoadDeflection;
            LLDeflectionLimit.Text = _settings.DeflectionCriteria.LiveLoadLimit.ToString();
            CheckTotalDeflection.IsChecked = _settings.DeflectionCriteria.CheckTotalDeflection;
            TotalDeflectionLimit.Text = _settings.DeflectionCriteria.TotalLoadLimit.ToString();
            CheckVibration.IsChecked = _settings.DeflectionCriteria.CheckVibration;

            // Calculation Options
            IncludeSelfWeight.IsChecked = _settings.IncludeSelfWeight;
            UseEffectiveLength.IsChecked = _settings.UseEffectiveLength;
            ConsiderLateralBracing.IsChecked = _settings.ConsiderLateralBracing;
            UseReducedLiveLoad.IsChecked = _settings.UseReducedLiveLoad;

            // Material Defaults
            SteelFyTextBox.Text = _settings.DefaultSteelFy.ToString();
            SteelETextBox.Text = _settings.DefaultSteelE.ToString();
            ConcreteFcTextBox.Text = _settings.DefaultConcreteFc.ToString();
        }

        private void SelectComboBoxItem(ComboBox comboBox, string value)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Tag?.ToString() == value)
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
            if (comboBox.Items.Count > 0)
                comboBox.SelectedIndex = 0;
        }

        private string GetSelectedComboValue(ComboBox comboBox)
        {
            if (comboBox.SelectedItem is ComboBoxItem item)
            {
                return item.Tag?.ToString() ?? string.Empty;
            }
            return string.Empty;
        }

        private void SaveSettingsFromUI()
        {
            // Default Loads
            if (double.TryParse(SDLTextBox.Text, out double sdl))
                _settings.DefaultSuperimposedDeadLoad = sdl;
            if (double.TryParse(LLTextBox.Text, out double ll))
                _settings.DefaultLiveLoad = ll;
            if (double.TryParse(SlabThicknessTextBox.Text, out double thickness))
                _settings.DefaultSlabThickness = thickness;
            if (double.TryParse(ConcreteDensityTextBox.Text, out double density))
                _settings.DefaultConcreteDensity = density;

            // Design Codes
            if (Enum.TryParse(GetSelectedComboValue(DesignMethodCombo), out DesignMethod dm))
                _settings.DesignMethod = dm;
            if (Enum.TryParse(GetSelectedComboValue(SteelCodeCombo), out SteelCode sc))
                _settings.SteelCode = sc;
            if (Enum.TryParse(GetSelectedComboValue(ConcreteCodeCombo), out ConcreteCode cc))
                _settings.ConcreteCode = cc;
            if (Enum.TryParse(GetSelectedComboValue(LoadCodeCombo), out LoadCode lc))
                _settings.LoadCode = lc;
            if (Enum.TryParse(GetSelectedComboValue(WoodCodeCombo), out WoodCode wc))
                _settings.WoodCode = wc;

            // Deflection Criteria
            _settings.DeflectionCriteria.CheckLiveLoadDeflection = CheckLLDeflection.IsChecked ?? true;
            if (double.TryParse(LLDeflectionLimit.Text, out double llLimit))
                _settings.DeflectionCriteria.LiveLoadLimit = llLimit;
            _settings.DeflectionCriteria.CheckTotalDeflection = CheckTotalDeflection.IsChecked ?? true;
            if (double.TryParse(TotalDeflectionLimit.Text, out double totalLimit))
                _settings.DeflectionCriteria.TotalLoadLimit = totalLimit;
            _settings.DeflectionCriteria.CheckVibration = CheckVibration.IsChecked ?? false;

            // Calculation Options
            _settings.IncludeSelfWeight = IncludeSelfWeight.IsChecked ?? true;
            _settings.UseEffectiveLength = UseEffectiveLength.IsChecked ?? true;
            _settings.ConsiderLateralBracing = ConsiderLateralBracing.IsChecked ?? true;
            _settings.UseReducedLiveLoad = UseReducedLiveLoad.IsChecked ?? false;

            // Material Defaults
            if (double.TryParse(SteelFyTextBox.Text, out double fy))
                _settings.DefaultSteelFy = fy;
            if (double.TryParse(SteelETextBox.Text, out double e))
                _settings.DefaultSteelE = e;
            if (double.TryParse(ConcreteFcTextBox.Text, out double fc))
                _settings.DefaultConcreteFc = fc;
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Reset all settings to default values?",
                "Reset Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SettingsManager.ResetToDefaults();
                _settings = SettingsManager.GetLoadSettings();
                LoadSettingsToUI();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromUI();
            SettingsManager.SaveSettings(_settings);
            DialogResult = true;
            Close();
        }
    }
}
