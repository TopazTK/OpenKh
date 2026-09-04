using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Microsoft.Win32;
using OpenKh.Tools.ModManager.Classes;
using OpenKh.Tools.ModManager.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OpenKh.Tools.ModManager.Wizard
{
    public partial class WizardGameSetup : ContentPage
    {
        SolidColorBrush _successBrush = SolidColorBrush.Parse("#40F040");
        SolidColorBrush _failBrush = SolidColorBrush.Parse("#F04040");

        public WizardGameSetup()
        {
            InitializeComponent();
        }

        private async void OnFolderClickFirst(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var _fetchTopLevel = TopLevel.GetTopLevel(this);

            var _fetchFolder = await _fetchTopLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select a Folder for 1.5+2.5...",
                AllowMultiple = false
            });

            if (_fetchFolder.Count >= 1)
                PathCollectionFirst.Text = _fetchFolder[0].Path.LocalPath;
        }

        private async void OnFolderClickSecond(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var _fetchTopLevel = TopLevel.GetTopLevel(this);

            var _fetchFolder = await _fetchTopLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select a Folder for 2.8...",
                AllowMultiple = false
            });

            if (_fetchFolder.Count >= 1)
                PathCollectionSecond.Text = _fetchFolder[0].Path.LocalPath;
        }

        private async void OnDetectClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var _fetchContext = DataContext as MainViewModel;
            var _fetchConfig = _fetchContext.CurrentConfig;

            if (_fetchConfig.Frontend.TargetPlatform == Platform.STEAM)
            {
                var _fetchFolders = new List<string>();
                var _fetchConfigPath = "";

                if (OperatingSystem.IsWindows())
                {
                    var _fetchSteamKey = Registry.LocalMachine.OpenSubKey("Software\\Valve\\Steam") ?? Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Valve\\Steam");
                    var _fetchInstallDir = _fetchSteamKey.GetValue("InstallPath").ToString();
                    _fetchConfigPath = Path.Combine(_fetchInstallDir, "steamapps", "libraryfolders.vdf");
                }

                else if (OperatingSystem.IsLinux())
                {
                    var _fetchHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                    // It is stupid that I have to do this.
                    // If someone knows a better way PLEASE tell me.
                    var _steamPossibleDirs = new List<string>()
                    {
                        Path.Combine(_fetchHome, ".steam/steam"),
                        Path.Combine(_fetchHome, ".local/share/Steam"),
                        Path.Combine(_fetchHome, ".var/app/com.valvesoftware.Steam/.steam"),
                        Path.Combine(_fetchHome, ".var/app/com.valvesoftware.Steam/data/Steam")
                    };

                    var _fetchInstallDir = _steamPossibleDirs.FirstOrDefault(x => Directory.Exists(x));
                    _fetchConfigPath = Path.Combine(_fetchInstallDir, "steamapps", "libraryfolders.vdf");
                }

                if (String.IsNullOrEmpty(_fetchConfigPath))
                    return;

                var _fetchLibraryConfig = File.ReadAllLines(_fetchConfigPath);
                var _pathRegex = new Regex("path[^\"]*\"\\s*\"([^\"]*)\"");

                var _fetchPath1525 = "";
                var _fetchPath28 = "";

                _fetchLibraryConfig.AsParallel().ForAll(_fetchLine =>
                {
                    var _fetchMatch = _pathRegex.Match(_fetchLine);

                    if (_fetchMatch.Success)
                    {
                        var _fetchValue = Regex.Unescape(_fetchMatch.Groups[1].Value);
                        _fetchFolders.Add(_fetchValue);
                    }
                });

                _fetchFolders.AsParallel().ForAll(_fetchFolder =>
                {
                    var _manifestPath1525 = Path.Combine(_fetchFolder, "steamapps", "appmanifest_2552430.acf");
                    var _manifestPath28 = Path.Combine(_fetchFolder, "steamapps", "appmanifest_2552440.acf");

                    var _installDirRegex = new Regex("installdir[^\"]*\"\\s*\"([^\"]*)\"");

                    if (File.Exists(_manifestPath1525))
                    {
                        var _fetchManifest = File.ReadAllLines(_manifestPath1525);

                        _fetchManifest.AsParallel().ForAll(_fetchLine =>
                        {
                            var _fetchMatch = _installDirRegex.Match(_fetchLine);

                            if (_fetchMatch.Success)
                            {
                                var _fetchValue = Regex.Unescape(_fetchMatch.Groups[1].Value);
                                var _fetchLibraryPath = Path.Combine(_fetchFolder, "steamapps", "common", _fetchValue);
                                _fetchPath1525 = _fetchLibraryPath.Replace("\\", "/");
                            }
                        });
                    }

                    if (File.Exists(_manifestPath28))
                    {
                        var _fetchManifest = File.ReadAllLines(_manifestPath28);

                        _fetchManifest.AsParallel().ForAll(_fetchLine =>
                        {
                            var _fetchMatch = _installDirRegex.Match(_fetchLine);

                            if (_fetchMatch.Success)
                            {
                                var _fetchValue = Regex.Unescape(_fetchMatch.Groups[1].Value);
                                var _fetchLibraryPath = Path.Combine(_fetchFolder, "steamapps", "common", _fetchValue);
                                _fetchPath28 = _fetchLibraryPath.Replace("\\", "/");
                            }
                        });
                    }
                });


                PathCollectionFirst.Text = !String.IsNullOrEmpty(_fetchPath1525) ? _fetchPath1525 : PathCollectionFirst.Text;
                PathCollectionFirst.BorderBrush = !String.IsNullOrEmpty(_fetchPath1525) ? _successBrush : _failBrush;

                PathCollectionSecond.Text = !String.IsNullOrEmpty(_fetchPath28) ? _fetchPath28 : PathCollectionSecond.Text;
                PathCollectionSecond.BorderBrush = !String.IsNullOrEmpty(_fetchPath28) ? _successBrush : _failBrush;
            }

            else if (_fetchConfig.Frontend.TargetPlatform == Platform.EPIC_GAMES_STORE)
            {
                if (OperatingSystem.IsWindows())
                {
                    var _fetchProgramData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    var _fetchEpicManifestPath = Path.Combine(_fetchProgramData, "Epic", "EpicGamesLauncher", "Data", "Manifests");

                    var _fetchAllFiles = Directory.GetFiles(_fetchEpicManifestPath);

                    var _fetchPath1525 = "";
                    var _fetchPath28 = "";

                    _fetchAllFiles.AsParallel().ForAll(_fetchFile =>
                    {
                        if (Path.GetExtension(_fetchFile) == ".item")
                        {
                            var _fetchFileRAW = File.ReadAllLines(_fetchFile);
                            var _catalogRegex = new Regex("\"CatalogNamespace\":\\s+\"([^\"]*)\"");
                            var _installDirRegex = new Regex("\"InstallLocation\":\\s+\"([^\"]*)\"");

                            var _isManifest1525 = false;
                            var _isManifest28 = false;

                            var _fetchDirectory = "";

                            _fetchFileRAW.AsParallel().ForAll(_fetchLine =>
                            {
                                var _fetchCatalogMatch = _catalogRegex.Match(_fetchLine);
                                var _fetchDirectoryMatch = _installDirRegex.Match(_fetchLine);

                                if (_fetchCatalogMatch.Success)
                                {
                                    var _fetchValue = Regex.Unescape(_fetchCatalogMatch.Groups[1].Value);

                                    _isManifest1525 = _fetchValue == "4158b699dd70447a981fee752d970a3e";
                                    _isManifest28 = _fetchValue == "c8ff067c1c984cd7ab1998e8a9afc8b6";
                                }

                                if (_fetchDirectoryMatch.Success)
                                    _fetchDirectory = Regex.Unescape(_fetchDirectoryMatch.Groups[1].Value);
                            });

                            if (_fetchDirectory != "" && _isManifest1525)
                                _fetchPath1525 = _fetchDirectory.Replace("\\", "/");

                            if (_fetchDirectory != "" && _isManifest28)
                                _fetchPath28 = _fetchDirectory.Replace("\\", "/");
                        }
                    });

                    PathCollectionFirst.Text = !String.IsNullOrEmpty(_fetchPath1525) ? _fetchPath1525 : PathCollectionFirst.Text;
                    PathCollectionFirst.BorderBrush = !String.IsNullOrEmpty(_fetchPath1525) ? _successBrush : _failBrush;

                    PathCollectionSecond.Text = !String.IsNullOrEmpty(_fetchPath28) ? _fetchPath28 : PathCollectionSecond.Text;
                    PathCollectionSecond.BorderBrush = !String.IsNullOrEmpty(_fetchPath28) ? _successBrush : _failBrush;
                }

                else
                {
                    PathCollectionFirst.BorderBrush = _failBrush;
                    PathCollectionSecond.BorderBrush =  _failBrush;
                }
            }
        }
    }
}
