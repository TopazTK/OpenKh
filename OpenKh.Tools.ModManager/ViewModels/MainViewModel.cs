using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenKh.Patcher;
using OpenKh.Tools.ModManager.Models;
using OpenKh.Tools.ModManager.Services;
using OpenKh.Tools.ModManager.Views;
using SharpYaml;
using SharpYaml.Serialization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace OpenKh.Tools.ModManager.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ModModel? _currentMod = null;

    [ObservableProperty]
    private ObservableCollection<ModModel>? _installedMods = null;

    [ObservableProperty]
    private bool _configurationValid = true;

    [ObservableProperty]
    private Configuration? _currentConfig = null;

    public void InitializeView()
    {
        CurrentMod = null;
        InstalledMods = null;
        ConfigurationValid = true;

        // === Configuration Parsing and Verification === //

        // If the config is null, parse it.
        // TODO: If it does not exist, pop-up the setup wizard.

        if (CurrentConfig == null)
        {
            var _fetchConfigFile = File.ReadAllText("config.yml");
            CurrentConfig = YamlSerializer.Deserialize<Configuration>(_fetchConfigFile);
        }

        // Handle these if the platform is NOT set to PCSX2.

        if (CurrentConfig.TargetPlatform != Platform.PCSX2)
        {
            // Construct the paths for the game executable and Panacea.
            var _gameExecutablePath = Path.Combine(CurrentConfig.GamePath, Configuration.GameExecutable[CurrentConfig.TargetGame]);
            var _panaceaPath = Path.Combine(CurrentConfig.GamePath, OperatingSystem.IsWindows() ? "DBGHELP.dll" : "version.dll");

            // Verify the game executable and directory exists as configured. If the build type is PANACEA, also verify Panacea's existence.
            var _isGameConfigValid = Directory.Exists(CurrentConfig.GamePath) && File.Exists(_gameExecutablePath);
            var _isPanaceaConfigValid = (CurrentConfig.ModBuildType == BuildType.PANACEA && File.Exists(_panaceaPath)) || CurrentConfig.ModBuildType == BuildType.PATCH;

            // If either are not valid, mark the config as faulty.
            if (!_isGameConfigValid || !_isPanaceaConfigValid)
                ConfigurationValid = false;
        }

        // === Mod Parsing and Verification === //

        // Construct the mod folder path for the specified game.

        var _fetchMods = new ObservableCollection<ModModel>();
        var _fetchModsPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "mods", CurrentConfig.TargetGame.ToString().ToLower());

        // For each directory that exists in the mod folder:

        foreach (var _fetchDirectory in Directory.EnumerateDirectories(_fetchModsPath))
        {
            // Construct the paths to YAML and PNG files.
            var _fetchPathYaml = Path.Combine(_fetchDirectory, "mod.yml");
            var _fetchPathIcon = Path.Combine(_fetchDirectory, "icon.png");

            // YAML don't do it? Don't do it!
            if (!File.Exists(_fetchPathYaml))
                continue;

            // Fetch and read the YAML to parse the metadata.
            var _metadata = Metadata.Read(_fetchPathYaml);

            // If the metadata is valid, parse the mod and push it to the ViewModel.

            if (_metadata.IsValid)
            {
                var _modModel = new ModModel
                {
                    ModTitle = _metadata.Title,
                    ModAuthor = _metadata.OriginalAuthor,
                    ModDescription = _metadata.Description,
                    ModPath = _fetchDirectory,
                    ModFilesList = _metadata.Assets.Select(x => x.Name).ToArray(),
                    ModIcon = new Bitmap(_fetchPathIcon),
                    ModActive = true,
                    ModValid = true
                };

                _fetchMods.Add(_modModel);
            }

            // Otherwise, make it known that the mod sucks ASS and is no good for us, but still push it to the ViewModel so we know about it :D

            else
            {
                var uri = new Uri("avares://OpenKh.Tools.ModManager/Assets/invalid_mod.png");

                var _modModel = new ModModel
                {
                    ModTitle = _metadata.Title,
                    ModAuthor = "This mod is invalid!",
                    ModDescription = "This mod contains errors within its YAML file. Please check the formatting!",
                    ModIcon = new Bitmap(AssetLoader.Open(uri)),
                    ModPath = _fetchDirectory,
                    ModValid = false
                };

                _fetchMods.Add(_modModel);
            }
        }

        // Sync the list.
        InstalledMods = _fetchMods;
    }

    public MainViewModel()
    {
        InitializeView();
    }
}
