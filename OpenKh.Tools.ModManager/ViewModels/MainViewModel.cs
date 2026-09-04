using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using LibGit2Sharp;
using OpenKh.Patcher;
using OpenKh.Tools.ModManager.Classes;
using OpenKh.Tools.ModManager.Models;
using OpenKh.Tools.ModManager.Services;
using OpenKh.Tools.ModManager.Views;
using SharpYaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xe.BinaryMapper;

namespace OpenKh.Tools.ModManager.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _initialized = false;

    [ObservableProperty]
    private ModModel? _currentMod = null;

    [ObservableProperty]
    private ObservableCollection<ModModel>? _installedMods = null;

    [ObservableProperty]
    private bool _configurationValid = true;

    [ObservableProperty]
    private Config? _currentConfig = null;

    [ObservableProperty]
    private bool _doesConfigExist = false;

    [ObservableProperty]
    private bool _hasModsInstalled = false;

    /// <summary>
    /// Activates whenever InstalledMods has a property change.
    /// It commits said changes to the targeted game's mod memory.
    /// </summary>
    protected void OnModPropertyChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Fetch the mod memory path and fetch the modlist from the sender.
        var _fetchModList = sender as ObservableCollection<ModModel>;
        var _fetchMemoryPath = Path.Combine(PathService.ResolveMod(CurrentConfig), "mod_memory.yml");

        // Create the actual mod memory collection.
        var _modMemoryList = new ObservableCollection<MemoryModel>();

        // For every mod that exists:
        foreach (var _fetchMod in _fetchModList)
        {
            // If the mod is invalid do not memorize it.
            if (!_fetchMod.ModValid)
                continue;

            // Construct the memory structure (Hash, Is Active, Current Index)
            _modMemoryList.Add
            (
                new MemoryModel 
                { 
                    ModHash = ModService.ResolveMD5(_fetchMod, CurrentConfig), 
                    ModActive = _fetchMod.ModActive, 
                    ModIndex = _fetchModList.IndexOf(_fetchMod) 
                }
            );
        }

        // Serialize and commit the mod memory.
        var _fetchSerial = YamlSerializer.Serialize(_modMemoryList);
        File.WriteAllText(_fetchMemoryPath, _fetchSerial);
    }

    public void InitializeView(bool selectLast = false)
    {
        Initialized = false;

        CurrentMod = null;
        InstalledMods = null;
        HasModsInstalled = false;
        ConfigurationValid = true;

        // === Configuration Parsing and Verification === //

        // If the config is null, parse it.
        // TODO: If it does not exist, pop-up the setup wizard.

        if (CurrentConfig == null)
        {
            var _fetchConfigFile = Path.Combine(AppContext.BaseDirectory, "config.yml");

            if (File.Exists(_fetchConfigFile))
            {
                var _fetchConfigRAW = File.ReadAllText(_fetchConfigFile);

                DoesConfigExist = true;
                CurrentConfig = YamlSerializer.Deserialize<Config>(_fetchConfigRAW);
            }

            else
            {
                DoesConfigExist = false;

                CurrentConfig = new Config
                {
                    Panacea = new Panacea(),

                    Frontend = new Frontend()
                    {
                        GamePath = new string[2],
                        TargetPlatform = Platform.STEAM,
                        ModBuildType = BuildType.PANACEA,
                        TargetGame = Game.KINGDOM_HEARTS_II
                    },

                    Emulator = new Emulator()
                    {
                        EmuPath = new string[1],
                        RomPath = new string[3],
                    }
                };

                var _fetchSerialize = YamlSerializer.Serialize(CurrentConfig);
                File.WriteAllText(_fetchConfigFile, _fetchSerialize);
            }
        }


        // Handle these if the platform is NOT set to PCSX2.

        if (CurrentConfig.Frontend.TargetPlatform != Platform.PCSX2)
        {
            // Fetch the bare arguments we will use.
            var _configFrontend = CurrentConfig.Frontend;
            var _fetchTargetGame = _configFrontend.TargetGame;

            // Check if the game is Dream Drop Distance and the second Game Path has been declared.
            // Otherwise, check if the game is NOT Dream Drop Distance.
            // If neither of these are true, the config isn't valid.

            var _isDDDConfigValid = _fetchTargetGame != Game.DREAM_DROP_DISTANCE || (_fetchTargetGame == Game.DREAM_DROP_DISTANCE && _configFrontend.GamePath.Length < 2);

            if (!_isDDDConfigValid)
                ConfigurationValid = false;

            else
            {
                // Fetch the second Game Path if the game is Dream Drop Distance, fetch the first one otherwise.
                var _fetchGamePath = _fetchTargetGame == Game.DREAM_DROP_DISTANCE ? PathService.ResolvePath28(CurrentConfig) : PathService.ResolvePath1525(CurrentConfig);

                if (String.IsNullOrEmpty(_fetchGamePath))
                    ConfigurationValid = false;

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

                                var _fetchManagerPath = PathService.ResolveBuild(CurrentConfig, true);
                                var _comparisonRules = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

                                if (!String.Equals(_fetchConfigValue, _fetchManagerPath, _comparisonRules))
                                    _isPanaceaConfigValid = false;
                            }
                        }
                    }

                    // If either are not valid, mark the config as faulty.
                    if (!_isGameConfigValid || !_isPanaceaConfigValid)
                        ConfigurationValid = false;
                }
            }
        }

        // === Mod Parsing and Verification === //

        // Construct the mod folder path for the specified game.

        var _fetchMods = new ObservableCollection<ModModel>();
        var _fetchModsPath = PathService.ResolveMod(CurrentConfig);

        // For each directory that exists in the mod folder:

        foreach (var _fetchDirectory in Directory.EnumerateDirectories(_fetchModsPath))
        {
            // Construct the paths to YAML and PNG files.
            var _fetchPathYaml = Path.Combine(_fetchDirectory, "mod.yml");
            var _fetchPathIcon = Path.Combine(_fetchDirectory, "icon.png");

            var _fetchPathGit = Path.Combine(_fetchDirectory, ".git");

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
                    ModIcon = File.Exists(_fetchPathIcon) ? new Bitmap(_fetchPathIcon) : null,
                    ModActive = true,
                    ModValid = true
                };

                // We have found a Git Repository, let's see what's up.

                Task.Run(() =>
                {
                    if (Directory.Exists(_fetchPathGit))
                    {
                        if (Repository.IsValid(_fetchPathGit))
                        {
                            var _fetchGit = new Repository(_fetchPathGit);

                            try
                            {
                                if (!_fetchGit.Info.IsHeadDetached)
                                {
                                    var _fetchRemote = _fetchGit.Network.Remotes["origin"];

                                    _modModel.ModSource = new Uri(_fetchRemote.Url);
                                    _modModel.ModIssues = new Uri(_fetchRemote.Url + "/issues");

                                    _modModel.ModPlatform = _modModel.ModSource.Host;

                                    Commands.Fetch(_fetchGit, _fetchRemote.Name, Array.Empty<string>(), null, null);

                                    var _fetchBehind = _fetchGit.Head.TrackingDetails.BehindBy;
                                    _modModel.ModBehindBy = _fetchBehind != null ? _fetchBehind.Value : 0;

                                }
                            }

                            catch (LibGit2SharpException) { }

                            _fetchGit.Dispose();
                            var _fetchGitDir = new DirectoryInfo(_fetchPathGit);

                            foreach (var _fetchFile in _fetchGitDir.GetFiles("*", SearchOption.AllDirectories))
                            {
                                try
                                {
                                    if (_fetchFile.Exists)
                                        _fetchFile.Attributes &= ~FileAttributes.ReadOnly;
                                }

                                catch (Exception) { }
                            }
                        }
                    }
                });

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
                    ModActive = false,
                    ModValid = false
                };

                _fetchMods.Add(_modModel);
            }
        }

        // When all mods are loaded, start processing the "mod memory" for the targeted game.\
        // If the mod memory does not exist, assume default order and mark all active.

        var _fetchModMemoryPath = Path.Combine(_fetchModsPath, "mod_memory.yml");

        if (File.Exists(_fetchModMemoryPath))
        {
            // Fetch the raw YAML data and deserialize it.
            var _fetchRawYaml = File.ReadAllText(_fetchModMemoryPath);
            var _fetchModMemory = YamlSerializer.Deserialize<ObservableCollection<MemoryModel>>(_fetchRawYaml);

            // Make a temporary array for us to order the mods.
            // And a temporary list for us to commit the mods.

            var _tempModArray = new ModModel[_fetchMods.Count];
            var _tempModList = new List<ModModel>();

            foreach (var _fetchMemory in _fetchModMemory)
            {
                var _fetchMod = _fetchMods.FirstOrDefault(x => ModService.ResolveMD5(x, CurrentConfig) == _fetchMemory.ModHash);

                if (_fetchMod == null || !_fetchMod.ModValid)
                    continue;

                _fetchMod.ModActive = _fetchMemory.ModActive;
                _tempModArray[_fetchMemory.ModIndex] = _fetchMod;

                _fetchMods.Remove(_fetchMod);
            }

            // Add all the existing mods from the array.
            _tempModList.AddRange(_tempModArray.Where(x => x != null));

            // Add all the valid mods from the original list which didn't exist on the mod memory.
            _tempModList.AddRange(_fetchMods.Where(x => x.ModValid));

            // Add all the invalid mods from the original list which didn't exist on the mod memory.
            _tempModList.AddRange(_fetchMods.Where(x => !x.ModValid));

            // Sync to the actual installed mods collection.
            InstalledMods = new ObservableCollection<ModModel>(_tempModList);
        }

        else
            InstalledMods = _fetchMods;

        // Register mod property event to handle mod memory.
        InstalledMods.CollectionChanged += OnModPropertyChanged;

        // If there is at least one mod, select the first mod and declare we have mods.
        if (InstalledMods.Count > 0)
        {
            HasModsInstalled = true;
            CurrentMod = InstalledMods.First();
        }

        // Initialization is complete.
        Initialized = true;
    }

    public MainViewModel()
    {
        InitializeView();
    }
}
