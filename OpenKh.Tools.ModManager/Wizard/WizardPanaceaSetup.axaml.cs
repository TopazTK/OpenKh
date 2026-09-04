using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using OpenKh.Tools.ModManager.Classes;
using OpenKh.Tools.ModManager.Services;
using OpenKh.Tools.ModManager.ViewModels;
using OpenKh.Tools.ModManager.Views;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenKh.Tools.ModManager.Wizard
{
    public partial class WizardPanaceaSetup : ContentPage
    {
        public WizardPanaceaSetup()
        {
            InitializeComponent();

            WeakReferenceMessenger.Default.Register<PageRequestMessage>(this, (registrar, message) =>
            {
                if (message.self != this)
                    return;

                InstallPanel.IsVisible = false;
                NotInstallPanel.IsVisible = true;

                var _fetchConfig = message.currentConfig;
                var _fetchPaths = _fetchConfig.Frontend.GamePath;

                var _fetchPath1525 = PathService.ResolvePath1525(_fetchConfig);
                var _fetchPath28 = PathService.ResolvePath28(_fetchConfig);

                var _fetchSettings1525 = Path.Combine(_fetchPath1525, "panacea_settings.txt");
                var _fetchAssembly1525 = Path.Combine(_fetchPath1525, OperatingSystem.IsWindows() ? "DBGHELP.dll" : "version.dll");

                var _fetchSettings28 = Path.Combine(_fetchPath28, "panacea_settings.txt");
                var _fetchAssembly28 = Path.Combine(_fetchPath28, OperatingSystem.IsWindows() ? "DBGHELP.dll" : "version.dll");

                var _isConfigValid1525 = false;
                var _isConfigValid28 = false;

                var _regexModPath = new Regex("mod_path=(.*)");

                if (File.Exists(_fetchAssembly1525) && File.Exists(_fetchSettings1525))
                {
                    var _fetchSettingsRAW = File.ReadAllLines(_fetchSettings1525);
                    var _fetchPanaceaPath = _fetchSettingsRAW.FirstOrDefault(x => _regexModPath.IsMatch(x));

                    if (_fetchPanaceaPath != null)
                    {
                        var _fetchMatch = _regexModPath.Match(_fetchPanaceaPath);
                        var _fetchValue = _fetchMatch.Groups[1].Value.Replace("\"", "");

                        var _fetchConfigPath = Path.GetFullPath(_fetchValue);

                        var _fetchManagerPath = PathService.ResolveBuild(_fetchConfig, true);
                        var _comparisonRules = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

                        if (String.Equals(_fetchConfigPath, _fetchManagerPath, _comparisonRules))
                            _isConfigValid1525 = true;
                    }
                }

                if (File.Exists(_fetchAssembly28) && File.Exists(_fetchSettings28))
                {
                    var _fetchSettingsRAW = File.ReadAllLines(_fetchSettings28);
                    var _fetchPanaceaPath = _fetchSettingsRAW.FirstOrDefault(x => _regexModPath.IsMatch(x));

                    if (_fetchPanaceaPath != null)
                    {
                        var _fetchMatch = _regexModPath.Match(_fetchPanaceaPath);
                        var _fetchValue = _fetchMatch.Groups[1].Value.Replace("\"", "");

                        var _fetchConfigPath = Path.GetFullPath(_fetchValue);

                        var _fetchManagerPath = PathService.ResolveBuild(_fetchConfig, true);
                        var _comparisonRules = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

                        if (String.Equals(_fetchConfigPath, _fetchManagerPath, _comparisonRules))
                            _isConfigValid28 = true;
                    }
                }

                _isConfigValid1525 = String.IsNullOrEmpty(_fetchPath1525) || _isConfigValid1525;
                _isConfigValid28 = String.IsNullOrEmpty(_fetchPath28) || _isConfigValid28;

                if (_isConfigValid1525 && _isConfigValid28)
                {
                    _fetchConfig.Frontend.ModBuildType = BuildType.PANACEA;

                    InstallPanel.IsVisible = true;
                    NotInstallPanel.IsVisible = false;
                }

                else
                    _fetchConfig.Frontend.ModBuildType = BuildType.PATCH;
            });
        }

        private void OnInstallClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var _fetchContext = DataContext as MainViewModel;
            var _fetchConfig = _fetchContext.CurrentConfig.Frontend;
            var _fetchBuildPath = PathService.ResolveBuild(_fetchContext.CurrentConfig, true);

            var _createPath = $"mod_path=\"{_fetchBuildPath}\"";
            var _fetchPanaceaPath = Path.Combine(AppContext.BaseDirectory, "resources/OpenKh.Research.Panacea.dll");

            var _fetchPath1525 = PathService.ResolvePath1525(_fetchContext.CurrentConfig);
            var _fetchPath28 = PathService.ResolvePath28(_fetchContext.CurrentConfig);

            var _fetchTarget1525 = Path.Combine(_fetchPath1525, OperatingSystem.IsWindows() ? "DBGHELP.dll" : "version.dll");
            var _fetchTarget28 = Path.Combine(_fetchPath28, OperatingSystem.IsWindows() ? "DBGHELP.dll" : "version.dll");

            if (!String.IsNullOrEmpty(_fetchPath1525))
            {
                File.Copy(_fetchPanaceaPath, _fetchTarget1525);
                File.WriteAllText(Path.Combine(_fetchPath1525, "panacea_settings.txt"), _createPath);
            }

            if (!String.IsNullOrEmpty(_fetchPath28))
            {
                File.Copy(_fetchPanaceaPath, _fetchTarget28);
                File.WriteAllText(Path.Combine(_fetchPath28, "panacea_settings.txt"), _createPath);
            }

            InstallPanel.IsVisible = true;
            NotInstallPanel.IsVisible = false;

            _fetchConfig.ModBuildType = BuildType.PANACEA;
        }

        private void OnUninstallClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var _fetchContext = DataContext as MainViewModel;
            var _fetchConfig = _fetchContext.CurrentConfig.Frontend;

            var _fetchPath1525 = PathService.ResolvePath1525(_fetchContext.CurrentConfig);
            var _fetchPath28 = PathService.ResolvePath28(_fetchContext.CurrentConfig);

            var _fetchTarget1525 = Path.Combine(_fetchPath1525, OperatingSystem.IsWindows() ? "DBGHELP.dll" : "version.dll");
            var _fetchTarget28 = Path.Combine(_fetchPath28, OperatingSystem.IsWindows() ? "DBGHELP.dll" : "version.dll");

            if (!String.IsNullOrEmpty(_fetchPath1525))
            {
                File.Delete(_fetchTarget1525);
                File.Delete(Path.Combine(_fetchPath1525, "panacea_settings.txt"));
            }

            if (!String.IsNullOrEmpty(_fetchPath28))
            {
                File.Delete(_fetchTarget28);
                File.Delete(Path.Combine(_fetchPath28, "panacea_settings.txt"));
            }

            InstallPanel.IsVisible = false;
            NotInstallPanel.IsVisible = true;

            _fetchConfig.ModBuildType = BuildType.PATCH;
        }
    }
}
