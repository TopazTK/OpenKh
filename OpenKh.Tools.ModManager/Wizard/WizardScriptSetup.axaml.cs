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
        private static string _templateConfig = "[{0}]\n" +
                                                "scripts = [{{ path = \"{1}\", relative = false }}]\n" +
                                                "exe = \"{2}\"\n" +
                                                "game_docs = \"{3}\"\n";

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
                if (message.Self != this)
                    return;

                var _fetchFrontend = message.CurrentConfig.Frontend;

                if (_fetchFrontend.TargetPlatform == Platform.PCSX2)
                    message.Reply(false);

                else
                {
                    InstallPanel.IsVisible = false;
                    NotInstallPanel.IsVisible = true;

                    var _fetchConfig = message.CurrentConfig;

                    var _fetchPath1525 = PathService.ResolvePath1525(_fetchConfig);
                    var _fetchPath28 = PathService.ResolvePath28(_fetchConfig);

                    var _fetchPanacea1525 = Path.Combine(_fetchPath1525, "panacea_settings.txt");
                    var _fetchPanacea28 = Path.Combine(_fetchPath28, "panacea_settings.txt");

                    var _fetchSettings1525 = Path.Combine(_fetchPath1525, "LuaBackend.toml");
                    var _fetchAssembly1525 = Path.Combine(_fetchPath1525, File.Exists(_fetchPanacea1525) ? "LuaBackend.dll" : (OperatingSystem.IsWindows() ? "DBGHELP.dll" : "version.dll"));

                    var _fetchSettings28 = Path.Combine(_fetchPath28, "LuaBackend.toml");
                    var _fetchAssembly28 = Path.Combine(_fetchPath28, File.Exists(_fetchPanacea28) ? "LuaBackend.dll" : (OperatingSystem.IsWindows() ? "DBGHELP.dll" : "version.dll"));

                    var _isConfigValid1525 = false;
                    var _isConfigValid28 = false;

                    if (File.Exists(_fetchAssembly1525) && File.Exists(_fetchSettings1525))
                    {
                        var _fetchSettingsRAW = File.ReadAllText(_fetchSettings1525);
                        var _fetchSettingsSerial = TomlSerializer.Deserialize<TomlTable>(_fetchSettingsRAW);

                        var _fetchTruthTable = new bool[4];

                        for (var i = 0; i < _fetchTruthTable.Length; i++)
                        {
                            var _fetchGame = (Game)i;
                            var _fetchLuaGameID = GameIds[_fetchGame];
                            var _fetchManagerGameID = Config.GameShorthand[_fetchGame];

                            var _fetchGameBuildPath = PathService.ResolveBuild(_fetchConfig, true);
                            var _fetchGameScriptPath = Path.Combine(_fetchGameBuildPath, _fetchManagerGameID, "scripts");

                            var _fetchTableRoot = _fetchSettingsSerial[_fetchLuaGameID] as TomlTable;
                            var _fetchTableScript = _fetchTableRoot["scripts"] as TomlArray;

                            foreach (TomlTable _fetchScript in _fetchTableScript)
                            {
                                var _fetchPath = (string)_fetchScript["path"];
                                var _fetchIsRelative = (bool)_fetchScript["relative"];

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
                        }
                        ;

                        _isConfigValid1525 = _fetchTruthTable.All(x => x == true);
                    }

                    if (File.Exists(_fetchAssembly28) && File.Exists(_fetchSettings28))
                    {
                        var _fetchSettingsRAW = File.ReadAllText(_fetchSettings28);
                        var _fetchSettingsSerial = TomlSerializer.Deserialize<TomlTable>(_fetchSettingsRAW);

                        var _fetchGameBuildPath = PathService.ResolveBuild(_fetchConfig, true);
                        var _fetchGameScriptPath = Path.Combine(_fetchGameBuildPath, "ddd", "scripts");

                        var _fetchTableRoot = _fetchSettingsSerial["kh3d"] as TomlTable;
                        var _fetchTableScript = _fetchTableRoot["scripts"] as TomlArray;

                        foreach (TomlTable _fetchScript in _fetchTableScript)
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
                    }
                    ;

                    _isConfigValid1525 = String.IsNullOrEmpty(_fetchPath1525) || _isConfigValid1525;
                    _isConfigValid28 = String.IsNullOrEmpty(_fetchPath28) || _isConfigValid28;

                    if (_isConfigValid1525 && _isConfigValid28)
                    {
                        InstallPanel.IsVisible = true;
                        NotInstallPanel.IsVisible = false;
                    }

                    message.Reply(true);
                }
            });
        }

        private void OnInstallClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var _fetchContext = DataContext as MainViewModel;
            var _fetchConfig = _fetchContext.CurrentConfig.Frontend;

            var _fetchPath1525 = PathService.ResolvePath1525(_fetchContext.CurrentConfig);
            var _fetchPath28 = PathService.ResolvePath28(_fetchContext.CurrentConfig);

            var _configLines = new List<string>();

            for (int i = 0x00; i < GameIds.Count(); i++)
            {
                var _fetchGame = (Game)i;
                var _fetchLuaGameID = GameIds[_fetchGame];
                var _fetchExecutable = Config.GameExecutable[_fetchGame];
                var _fetchManagerGameID = Config.GameShorthand[_fetchGame];

                var _fetchGamePath = _fetchGame == Game.DREAM_DROP_DISTANCE ? "KINGDOM HEARTS HD 2.8 Final Chapter Prologue" : "KINGDOM HEARTS HD 1.5+2.5 ReMIX";
                var _fetchFolderBuild = PathService.ResolveBuild(_fetchContext.CurrentConfig, true);

                var _fetchFolderScripts = Path.Combine(_fetchFolderBuild, _fetchManagerGameID, "scripts");
                var _fetchFolderDocs = Path.Combine(_fetchConfig.TargetPlatform == Platform.STEAM ? "My Games" : "", _fetchGamePath);

                var _formatTemplate = String.Format(_templateConfig, _fetchLuaGameID, _fetchFolderScripts, _fetchExecutable, _fetchFolderDocs).Replace("\\", "/");

                _configLines.AddRange(_formatTemplate.Split('\n'));
            }

            var _fetchBackendPath = Path.Combine(AppContext.BaseDirectory, "resources/LuaBackend.dll");

            if (!String.IsNullOrEmpty(_fetchPath1525))
            {
                var _fetchPanacea1525 = Path.Combine(_fetchPath1525, "panacea_settings.txt");
                var _fetchAssembly1525 = Path.Combine(_fetchPath1525, File.Exists(_fetchPanacea1525) ? "LuaBackend.dll" : (OperatingSystem.IsWindows() ? "DBGHELP.dll" : "version.dll"));

                File.Copy(_fetchBackendPath, _fetchAssembly1525, true);
                File.WriteAllLines(Path.Combine(_fetchPath1525, "LuaBackend.toml"), _configLines);
            }

            if (!String.IsNullOrEmpty(_fetchPath28))
            {
                var _fetchPanacea28 = Path.Combine(_fetchPath28, "panacea_settings.txt");
                var _fetchAssembly28 = Path.Combine(_fetchPath28, File.Exists(_fetchPanacea28) ? "LuaBackend.dll" : (OperatingSystem.IsWindows() ? "DBGHELP.dll" : "version.dll"));

                File.Copy(_fetchBackendPath, _fetchAssembly28, true);
                File.WriteAllLines(Path.Combine(_fetchPath28, "LuaBackend.toml"), _configLines);
            }

            InstallPanel.IsVisible = true;
            NotInstallPanel.IsVisible = false;
        }

        private void OnUninstallClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var _fetchContext = DataContext as MainViewModel;
            var _fetchConfig = _fetchContext.CurrentConfig.Frontend;

            var _fetchPath1525 = PathService.ResolvePath1525(_fetchContext.CurrentConfig);
            var _fetchPath28 = PathService.ResolvePath28(_fetchContext.CurrentConfig);

            if (!String.IsNullOrEmpty(_fetchPath1525))
            {
                var _fetchPanacea1525 = Path.Combine(_fetchPath1525, "panacea_settings.txt");
                var _fetchAssembly1525 = Path.Combine(_fetchPath1525, File.Exists(_fetchPanacea1525) ? "LuaBackend.dll" : (OperatingSystem.IsWindows() ? "DBGHELP.dll" : "version.dll"));

                File.Delete(_fetchAssembly1525);
                File.Delete(Path.Combine(_fetchPath1525, "LuaBackend.toml"));
            }

            if (!String.IsNullOrEmpty(_fetchPath28))
            {
                var _fetchPanacea28 = Path.Combine(_fetchPath28, "panacea_settings.txt");
                var _fetchAssembly28 = Path.Combine(_fetchPath28, File.Exists(_fetchPanacea28) ? "LuaBackend.dll" : (OperatingSystem.IsWindows() ? "DBGHELP.dll" : "version.dll"));

                File.Delete(_fetchAssembly28);
                File.Delete(Path.Combine(_fetchPath28, "LuaBackend.toml"));
            }

            InstallPanel.IsVisible = false;
            NotInstallPanel.IsVisible = true;
        }
    }
}
