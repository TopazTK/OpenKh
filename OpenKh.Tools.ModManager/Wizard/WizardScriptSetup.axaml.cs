using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using OpenKh.Tools.ModManager.Classes;
using OpenKh.Tools.ModManager.Services;
using OpenKh.Tools.ModManager.ViewModels;
using OpenKh.Tools.ModManager.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Tomlyn;
using Tomlyn.Model;

namespace OpenKh.Tools.ModManager.Wizard
{
    public partial class WizardScriptSetup : ContentPage
    {
        public static Dictionary<Game, string> GameIds = new Dictionary<Game, string>()
        {
            { Game.KINGDOM_HEARTS, "kh1" },
            { Game.KINGDOM_HEARTS_II, "kh2" },
            { Game.CHAIN_OF_MEMORIES, "recom" },
            { Game.BIRTH_BY_SLEEP, "bbs" },
            { Game.DREAM_DROP_DISTANCE, "kh3d" },
        };


        public WizardScriptSetup()
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

                var _fetchPanacea1525 = Path.Combine(_fetchPaths[0], "panacea_settings.txt");
                var _fetchPanacea28 = Path.Combine(_fetchPaths[1], "panacea_settings.txt");

                var _checkPanacea1525 = File.Exists(_fetchPanacea1525);
                var _checkPanacea28 = File.Exists(_fetchPanacea1525);

                var _fetchSettings1525 = Path.Combine(_fetchPaths[0], "LuaBackend.toml");
                var _fetchAssembly1525 = Path.Combine(_fetchPaths[0], _checkPanacea1525 ? "LuaBackend.dll" : (OperatingSystem.IsWindows() ? "DBGHELP.dll" : "version.dll"));

                var _fetchSettings28 = Path.Combine(_fetchPaths[1], "LuaBackend.toml");
                var _fetchAssembly28 = Path.Combine(_fetchPaths[1], _checkPanacea1525 ? "LuaBackend.dll" : (OperatingSystem.IsWindows() ? "DBGHELP.dll" : "version.dll"));

                var _isConfigValid1525 = false;
                var _isConfigValid28 = false;

                if (File.Exists(_fetchAssembly1525) && File.Exists(_fetchSettings1525))
                {
                    var _fetchSettingsRAW = File.ReadAllText(_fetchSettings1525);
                    var _fetchSettingsSerial = TomlSerializer.Deserialize<TomlTable>(_fetchSettingsRAW);

                    var _fetchTruthTable = new bool[4];

                    for  (var i = 0; i < _fetchTruthTable.Length; i++)
                    {
                        var _fetchGame = (Game)i;
                        var _fetchLuaGameID = GameIds[_fetchGame];
                        var _fetchManagerGameID = Config.GameShorthand[_fetchGame];

                        var _fetchGameBuildPath = PathService.ResolveBuild(_fetchConfig, true);
                        var _fetchGameScriptPath = Path.Combine(_fetchGameBuildPath, _fetchManagerGameID, "scripts");

                        var _fetchTableRoot = _fetchSettingsSerial[_fetchLuaGameID] as TomlTable;
                        var _fetchTableScript = _fetchTableRoot["scripts"] as TomlTableArray;

                        foreach (var _fetchScript in _fetchTableScript)
                        {
                            var _fetchPath = (string) _fetchScript["path"];
                            var _fetchIsRelative = (bool) _fetchScript["relative"];

                            if (_fetchIsRelative || String.IsNullOrEmpty(_fetchPath))
                                continue;

                            var _fetchFullPath = Path.GetFullPath(_fetchPath);
                            var _comparisonRules = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

                            if (String.Equals(_fetchGameScriptPath, _fetchFullPath, _comparisonRules))
                            {
                                _fetchTruthTable[i] = true;
                                break;
                            }
                        }
                    };

                    _isConfigValid1525 = _fetchTruthTable.All(x => x == true);
                }

                if (File.Exists(_fetchAssembly28) && File.Exists(_fetchSettings28))
                {
                    var _fetchSettingsRAW = File.ReadAllText(_fetchSettings28);
                    var _fetchSettingsSerial = TomlSerializer.Deserialize<TomlTable>(_fetchSettingsRAW);

                    var _fetchGameBuildPath = PathService.ResolveBuild(_fetchConfig, true);
                    var _fetchGameScriptPath = Path.Combine(_fetchGameBuildPath, "ddd", "scripts");

                    var _fetchTableRoot = _fetchSettingsSerial["kh3d"] as TomlTable;
                    var _fetchTableScript = _fetchTableRoot["scripts"] as TomlTableArray;

                    foreach (var _fetchScript in _fetchTableScript)
                    {
                        var _fetchPath = (string)_fetchScript["path"];
                        var _fetchIsRelative = (bool)_fetchScript["relative"];

                        if (_fetchIsRelative || String.IsNullOrEmpty(_fetchPath))
                            continue;

                        var _fetchFullPath = Path.GetFullPath(_fetchPath);
                        var _comparisonRules = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

                        if (String.Equals(_fetchGameScriptPath, _fetchFullPath, _comparisonRules))
                        {
                            _isConfigValid28 = true;
                            break;
                        }
                    }
                };

                _isConfigValid1525 = String.IsNullOrEmpty(_fetchPaths[0]) || _isConfigValid1525;
                _isConfigValid28 = String.IsNullOrEmpty(_fetchPaths[1]) || _isConfigValid28;

                if (_isConfigValid1525 && _isConfigValid28)
                {
                    InstallPanel.IsVisible = true;
                    NotInstallPanel.IsVisible = false;
                }
            });
        }

        private void OnInstallClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var _fetchContext = DataContext as MainViewModel;
            var _fetchConfig = _fetchContext.CurrentConfig.Frontend;

            InstallPanel.IsVisible = true;
            NotInstallPanel.IsVisible = false;
        }

        private void OnUninstallClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var _fetchContext = DataContext as MainViewModel;
            var _fetchConfig = _fetchContext.CurrentConfig.Frontend;

            InstallPanel.IsVisible = false;
            NotInstallPanel.IsVisible = true;
        }
    }
}
