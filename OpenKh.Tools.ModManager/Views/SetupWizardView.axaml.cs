using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using OpenKh.Tools.ModManager.Classes;
using OpenKh.Tools.ModManager.Services;
using OpenKh.Tools.ModManager.ViewModels;
using OpenKh.Tools.ModManager.Wizard;
using SharpYaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static OpenKh.Tools.ModManager.Wizard.WizardPanaceaSetup;

namespace OpenKh.Tools.ModManager.Views
{
    public record PageRequestMessage(object self, Config currentConfig);

    public partial class SetupWizardView : Window
    {
        private WizardViewModel _rootModel { get; set; }

        public SetupWizardView()
        {
            _rootModel = new WizardViewModel();

            _rootModel.CurrentWizardPage = new WizardGameSetup();
            _rootModel.PastWizardPages = new ObservableCollection<object?>();

            _rootModel.FutureWizardPages = new ObservableCollection<object?>()
            {
                new WizardPanaceaSetup(),
                new WizardScriptSetup(),
                new WizardDirectLaunch(),
                new WizardExtractSetup(),
            };

            var _fetchApplication = Application.Current;

            // Uhh how the fuck?
            if (_fetchApplication == null)
                throw new NullReferenceException("Application is null, this should not be possible.");

            var _fetchLifetime = _fetchApplication.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var _fetchMainView = _fetchLifetime.MainWindow;

            // Uhh how the fuck, electric bogaloo?
            if (_fetchMainView == null)
                throw new NullReferenceException("MainView is null, this ALSO should not be possible.");

            var _fetchContext = _fetchMainView.DataContext as MainViewModel;
            DataContext = _fetchContext;

            InitializeComponent();
        }

        private void OnNextPage(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var _fetchContext = DataContext as MainViewModel;

            var _fetchBackList = _rootModel.PastWizardPages;
            var _fetchNextList = _rootModel.FutureWizardPages;

            var _fetchNextPage = _fetchNextList.First();
            _fetchNextList.Remove(_fetchNextPage);

            _fetchBackList.Add(_rootModel.CurrentWizardPage);
            _rootModel.CurrentWizardPage = _fetchNextPage;

            if (_fetchNextList.Count == 0)
            {
                NextButton.IsEnabled = false;

                FinishButton.IsVisible = true;
                CancelButton.IsVisible = false;
            }

            WeakReferenceMessenger.Default.Send(new PageRequestMessage(_rootModel.CurrentWizardPage, _fetchContext.CurrentConfig));

            BackButton.IsEnabled = true;
        }

        private void OnBackPage(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var _fetchContext = DataContext as MainViewModel;

            var _fetchBackList = _rootModel.PastWizardPages;
            var _fetchNextList = _rootModel.FutureWizardPages;

            var _fetchBackPage = _fetchBackList.Last();
            _fetchBackList.Remove(_fetchBackPage);

            _fetchNextList.Insert(0, _rootModel.CurrentWizardPage);
            _rootModel.CurrentWizardPage = _fetchBackPage;

            if (_fetchBackList.Count == 0)
                BackButton.IsEnabled = false;

            FinishButton.IsVisible = false;
            CancelButton.IsVisible = true;

            WeakReferenceMessenger.Default.Send(new PageRequestMessage(_rootModel.CurrentWizardPage, _fetchContext.CurrentConfig));

            NextButton.IsEnabled = true;
        }

        private void OnSubmitClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var _fetchContext = DataContext as MainViewModel;
            var _fetchPathYAML = Path.Combine(AppContext.BaseDirectory, "config.yml");

            // Fetch the bare arguments we will use.
            var _configFrontend = _fetchContext.CurrentConfig.Frontend;
            var _fetchTargetGame = _configFrontend.TargetGame;

            // Check if the game is Dream Drop Distance and the second Game Path has been declared.
            // Otherwise, check if the game is NOT Dream Drop Distance.
            // If neither of these are true, the config isn't valid.

            var _isDDDConfigValid = _fetchTargetGame != Game.DREAM_DROP_DISTANCE || (_fetchTargetGame == Game.DREAM_DROP_DISTANCE && _configFrontend.GamePath.Length < 2);

            if (!_isDDDConfigValid)
                _fetchContext.ConfigurationValid = false;

            else
            {
                // Fetch the second Game Path if the game is Dream Drop Distance, fetch the first one otherwise.
                var _fetchGamePath = _fetchTargetGame == Game.DREAM_DROP_DISTANCE ? PathService.ResolvePath28(_fetchContext.CurrentConfig) : PathService.ResolvePath1525(_fetchContext.CurrentConfig);

                if (String.IsNullOrEmpty(_fetchGamePath))
                    _fetchContext.ConfigurationValid = false;

                else
                {
                    // Construct the paths for the game executable and Panacea.
                    var _fetchExePath = Path.Combine(_fetchGamePath, Config.GameExecutable[_configFrontend.TargetGame]);
                    var _fetchSettingsPath = Path.Combine(_fetchGamePath, "panacea_settings.txt");
                    var _fetchPanaceaPath = Path.Combine(_fetchGamePath, OperatingSystem.IsWindows() ? "DBGHELP.dll" : "version.dll");

                    // Verify the game executable and directory exists as configured. If the build type is PANACEA, also verify Panacea's existence.
                    var _isGameConfigValid = Directory.Exists(_fetchGamePath) && File.Exists(_fetchExePath);
                    var _isPanaceaConfigValid = (_configFrontend.ModBuildType == BuildType.PANACEA && File.Exists(_fetchPanaceaPath)) || _configFrontend.ModBuildType == BuildType.PATCH;

                    if (_isPanaceaConfigValid && _configFrontend.ModBuildType == BuildType.PANACEA)
                    {
                        var _regexModPath = new Regex("mod_path=(.*)");

                        if (File.Exists(_fetchPanaceaPath) && File.Exists(_fetchSettingsPath))
                        {
                            var _fetchSettingsRAW = File.ReadAllLines(_fetchSettingsPath);
                            var _fetchConfigPath = _fetchSettingsRAW.FirstOrDefault(x => _regexModPath.IsMatch(x));

                            if (_fetchConfigPath != null)
                            {
                                var _fetchMatch = _regexModPath.Match(_fetchConfigPath);
                                var _fetchValue = _fetchMatch.Groups[1].Value.Replace("\"", "");

                                var _fetchConfigValue = Path.GetFullPath(_fetchValue);

                                var _fetchManagerPath = PathService.ResolveBuild(_fetchContext.CurrentConfig, true);
                                var _comparisonRules = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

                                if (!String.Equals(_fetchConfigValue, _fetchManagerPath, _comparisonRules))
                                    _isPanaceaConfigValid = false;
                            }
                        }
                    }

                    // If either are not valid, mark the config as faulty.
                    if (!_isGameConfigValid || !_isPanaceaConfigValid)
                        _fetchContext.ConfigurationValid = false;

                    else
                        _fetchContext.ConfigurationValid = true;
                }
            }

            var _fetchSerialize = YamlSerializer.Serialize(_fetchContext.CurrentConfig);
            File.WriteAllText(_fetchPathYAML, _fetchSerialize);

            Close();
        }
    }
}
