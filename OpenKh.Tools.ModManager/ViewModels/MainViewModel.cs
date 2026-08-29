using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenKh.Tools.ModManager.Models;
using OpenKh.Tools.ModManager.Services;
using OpenKh.Tools.ModManager.Views;
using SharpYaml;
using SharpYaml.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using OpenKh.Patcher;
using System.Linq;
using Avalonia.Media.Imaging;

namespace OpenKh.Tools.ModManager.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ModModel? _currentMod = null;

    [ObservableProperty]
    private List<ModModel>? _installedMods = null;

    [ObservableProperty]
    private bool _configurationValid = true;

    [ObservableProperty]
    private Configuration? _currentConfig = null;

    public MainViewModel()
    {
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

        InstalledMods = new List<ModModel>();
        var _fetchModsPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "mods", CurrentConfig.TargetGame.ToString().ToLower());

        foreach (var _fetchDirectory in Directory.EnumerateDirectories(_fetchModsPath))
        {
            var _fetchPathYaml = Path.Combine(_fetchDirectory, "mod.yml");
            var _fetchPathIcon = Path.Combine(_fetchDirectory, "icon.png");

            if (!File.Exists(_fetchPathYaml))
                continue;

            using (var _fileStream = new FileStream(_fetchPathYaml, FileMode.Open))
            {
                var _metadata = Metadata.Read(_fileStream);
                var _modModel = new ModModel
                {
                    ModTitle = _metadata.Title,
                    ModAuthor = _metadata.OriginalAuthor,
                    ModDescription = _metadata.Description,
                    ModPath = _fetchDirectory,
                    ModFilesList = string.Join("\n", _metadata.Assets.Select(x => x.Name)),
                    ModIcon = new Bitmap(_fetchPathIcon),
                    ModActive = true
                };

                InstalledMods.Add(_modModel);
            }
        }
    }
}
