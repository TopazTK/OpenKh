#pragma warning disable CS4014

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using OpenKh.Bbs.SystemData;
using OpenKh.Patcher;
using OpenKh.Tools.ModManager.Classes;
using OpenKh.Tools.ModManager.Dialogs;
using OpenKh.Tools.ModManager.Models;
using OpenKh.Tools.ModManager.Services;
using OpenKh.Tools.ModManager.Views;
using SharpYaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

    [ObservableProperty]
    private ObservableCollection<object> _presetItems;

    /// <summary>
    /// Activates whenever InstalledMods has a property change.
    /// It commits said changes to the targeted game's mod memory.
    /// </summary>
    protected void OnModPropertyChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Fetch the modlist from the sender.
        var _fetchModList = sender as ObservableCollection<ModModel>;

        if (CurrentConfig != null && _fetchModList != null)
        {
            // Fetch the mod memory path.
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
    }

    public TopLevel? FetchTopLevel()
    {
        var _fetchApplication = Avalonia.Application.Current;

        if (_fetchApplication == null)
            return null;

        var _fetchLifetime = _fetchApplication.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var _fetchMainView = _fetchLifetime != null ? _fetchLifetime.MainWindow : null;

        return _fetchMainView;
    }

    public MainViewModel() => InitializeView();

    public void InitializeMods()
    {
        if (CurrentConfig != null)
        {
            InstalledMods = null;
            HasModsInstalled = false;

            // Construct the mod folder path for the specified game.

            var _fetchMods = new ObservableCollection<ModModel>();
            var _fetchModsPath = PathService.ResolveMod(CurrentConfig);

            // For each directory that exists in the mod folder:

            foreach (var _fetchDirectory in Directory.EnumerateDirectories(_fetchModsPath))
            {
                foreach (var _fetchChild in Directory.EnumerateDirectories(_fetchDirectory))
                {
                    // Construct the paths to YAML and PNG files.
                    var _fetchPathYaml = Path.Combine(_fetchChild, "mod.yml");
                    var _fetchPathIcon = Path.Combine(_fetchChild, "icon.png");

                    var _fetchPathGit = Path.Combine(_fetchChild, ".git");

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
                            ModPath = _fetchChild,
                            ModFilesList = _metadata.Assets.Select(x => x.Name).ToArray(),
                            ModIcon = File.Exists(_fetchPathIcon) ? new Bitmap(_fetchPathIcon) : null,
                            ModActive = true,
                            ModValid = true
                        };

                        if (_metadata.Preferences != null)
                        {
                            _modModel.ModPreferences = new List<PreferenceModel>();

                            foreach (var _preference in _metadata.Preferences)
                            {
                                var _fetchPref = new PreferenceModel
                                {
                                    Title = _preference.Title,
                                    Key = _preference.Key,
                                    Description = _preference.Description,
                                    Type = _preference.Type,
                                    Options = _preference.Options,
                                    Value = _preference.Value
                                };

                                _modModel.ModPreferences.Add(_fetchPref);
                            }
                        }

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
                            ModPath = _fetchChild,
                            ModActive = false,
                            ModValid = false
                        };

                        _fetchMods.Add(_modModel);
                    }
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
        }
    }

    public void InitializeView()
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
            var CurrentConfigFile = Path.Combine(AppContext.BaseDirectory, "config.yml");

            if (File.Exists(CurrentConfigFile))
            {
                var CurrentConfigRAW = File.ReadAllText(CurrentConfigFile);

                DoesConfigExist = true;
                CurrentConfig = Config.Load();
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
                        TargetGame = Game.KINGDOM_HEARTS_II,
                        TargetPreset = new string[5]
                    },

                    Emulator = new Emulator()
                    {
                        EmuPath = new string[1],
                        RomPath = new string[3],
                    }
                };

                CurrentConfig.Commit();
            }
        }

        ConfigurationValid = CurrentConfig.IsValid();

        // === Preset Parsing and Verification === //

        PresetItems = new ObservableCollection<object>();

        PresetItems.Add(new MenuItem()
        {
            Header = "Create New Preset...",
            Command = CreatePresetCommand,
            InputGesture = new KeyGesture(Key.N, KeyModifiers.Alt)
        });

        PresetItems.Add(new Separator());

        var _fetchGame = (int)CurrentConfig.Frontend.TargetGame;
        var _fetchPreset = CurrentConfig.Frontend.TargetPreset[_fetchGame];

        var _fetchPresetPath = Path.Combine(AppContext.BaseDirectory, "preset.yml");

        if (File.Exists(_fetchPresetPath))
        {
            var _fetchPresetRAW = File.ReadAllText(_fetchPresetPath);
            var _fetchPresetList = YamlSerializer.Deserialize<List<PresetModel>>(_fetchPresetRAW);

            if (_fetchPresetList.Count > 0)
            {
                var _fetchKeyIndex = 34;

                PresetItems.Add(new MenuItem()
                {
                    Header = "Default",
                    ToggleType = MenuItemToggleType.Radio,
                    IsChecked = String.IsNullOrEmpty(_fetchPreset),
                    Command = SwitchPresetCommand,
                    CommandParameter = "",
                    InputGesture = new KeyGesture(Key.D, KeyModifiers.Alt)
                });

                foreach (var _preset in _fetchPresetList)
                {
                    var _fetchKey = (Key)_fetchKeyIndex;

                    var _fetchMenuItem = new MenuItem()
                    {
                        Header = _preset.PresetName,
                        ToggleType = MenuItemToggleType.Radio,
                        IsChecked = _fetchPreset == _preset.PresetName,
                        Command = SwitchPresetCommand,
                        CommandParameter = _preset.PresetName,
                        ContextMenu = new ContextMenu
                        {
                            ItemsSource = new[]
                            {
                                new MenuItem
                                {
                                    Header = "Rename Preset...",
                                    Command = RenamePresetCommand,
                                    CommandParameter = _preset.PresetName
                                },

                                new MenuItem
                                {
                                    Header = "Delete Preset...",
                                    Command = DeletePresetCommand,
                                    CommandParameter = _preset.PresetName
                                }
                            }
                        },
                        InputGesture = new KeyGesture(_fetchKey, KeyModifiers.Alt)
                    };

                    PresetItems.Add(_fetchMenuItem);

                    _fetchKeyIndex++;
                }
            }

            if (CurrentConfig.Frontend.TargetPreset != null)
            {
                var _tryFetchPreset = _fetchPresetList.FirstOrDefault(x => x.PresetName == _fetchPreset);

                if (_tryFetchPreset == null)
                    CurrentConfig.Frontend.TargetPreset[_fetchGame] = "";
            }
        }

        else
            File.WriteAllText(_fetchPresetPath, "[]");

        if (PresetItems.Count == 0x02)
        {
            PresetItems.Add(new MenuItem()
            {
                Header = "No Presets Available.",
                IsEnabled = false
            });

            if (CurrentConfig.Frontend.TargetPreset != null)
                CurrentConfig.Frontend.TargetPreset[_fetchGame] = "";
        }

        InitializeMods();

        // Initialization is complete.
        Initialized = true;
    }

    [RelayCommand]
    private async Task<bool> Install(string? inputParameter)
    {
        var _fetchErroredList = new List<string>();
        var _fetchSuccessList = new List<string>();

        if (CurrentConfig != null && InstalledMods != null)
        {
            var _fetchTopLevel = FetchTopLevel() as MainView;

            if (_fetchTopLevel == null)
                return false;

            var _fetchResult = inputParameter ?? await DialogService.ShowInput(_fetchTopLevel, "Install a new Mod", "Enter the name of the repository to install.", "Install", "Ex. OpenKH/a-very-cool-mod@github.com", null, "Select and Install an Archive or Script", async (inputParent) =>
            {
                var _fetchFiles = await _fetchTopLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select an Archive or Script File...",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("OpenKH Mod Archive") { Patterns = ["*.zip"] },
                        new FilePickerFileType("PCPatch Package") { Patterns = ["*.kh1pcpatch", "*.kh2pcpatch", "*.bbspcpatch", "*.compcpatch", "*.dddpcpatch"] },
                        new FilePickerFileType("LuaBackend Script") { Patterns = ["*.lua"] },
                        new FilePickerFileType("All Files") { Patterns = ["*"] },
                    ]
                });

                if (_fetchFiles.Count >= 1)
                    return _fetchFiles[0].Path.LocalPath;

                else
                    return null;
            });

            var _fetchModPath = PathService.ResolveMod(CurrentConfig);
            var _fetchInstallResult = 0x00;

            if (_fetchResult != null && !string.IsNullOrEmpty(_fetchResult))
            {
                var _fetchFileInfo = new FileInfo(@$"{_fetchResult}");

                if (_fetchFileInfo.Exists)
                {
                    SafePtr _progressCurrentPtr = 0x00;
                    SafePtr _progressMaximumPtr = 0x00;

                    SafePtr _progressTextPtr = "Processing Local Files: {0} / {3}";

                    var _singleProgress = DialogService.ShowProgress(_fetchTopLevel, "Installing Mod...", "Installing declared Mod... Please be patient...", ModService.CancelTokenSource, _progressCurrentPtr, _progressMaximumPtr, _progressTextPtr);

                    _fetchInstallResult =
                    await ModService.InstallLocal
                    (
                        _fetchResult,
                        CurrentConfig,
                        (int processed, int total) =>
                        {
                           _progressCurrentPtr /= processed;
                           _progressMaximumPtr /= total;

                            if (ModService.CancelToken.IsCancellationRequested)
                                return false;

                            return true;
                        }
                    );

                    // Free all the pointers allocated.

                    _progressTextPtr.Dispose();

                    _progressMaximumPtr.Dispose();
                    _progressCurrentPtr.Dispose();
                }

                else
                {
                    var _fetchMultiInstall = _fetchResult.Contains(';') ? _fetchResult.Split(';') : null;

                    if (_fetchMultiInstall == null)
                    {
                        // Allocate the pointers necessary.

                        SafePtr _progressCurrentPtr = 0x00;
                        SafePtr _progressMaximumPtr = 0x00;

                        SafePtr _progressTextPtr = "Receiving Git Objects: {0} / {3}";

                        var _singleProgress = DialogService.ShowProgress(_fetchTopLevel, "Installing Mod...", "Installing declared Mod... Please be patient...", ModService.CancelTokenSource, _progressCurrentPtr, _progressMaximumPtr, _progressTextPtr);

                        _fetchInstallResult =
                        await ModService.InstallGit
                        (
                            _fetchResult,
                            CurrentConfig,
                            new TransferProgressHandler((progress) =>
                            {
                                _progressCurrentPtr /= progress.ReceivedObjects;
                                _progressMaximumPtr /= progress.TotalObjects;

                                if (ModService.CancelToken.IsCancellationRequested)
                                    return false;

                                return true;
                            }
                        ));

                        // Free all the pointers allocated.

                        _progressTextPtr.Dispose();

                        _progressMaximumPtr.Dispose();
                        _progressCurrentPtr.Dispose();
                    }

                    else
                    {
                        // Allocate all the pointers to use for the progress bars.

                        SafePtr _firstCurrentPtr = 0x00;
                        SafePtr _firstMaximumPtr = 0x00;

                        SafePtr _secondCurrentPtr = 0x00;
                        SafePtr _secondMaximumPtr = 0x00;

                        SafePtr _firstTextPtr = $"Processing Mod: N/A";
                        SafePtr _secondTextPtr = "Receiving Git Objects: {0} / {3}";

                        // Show the dialog.

                        var _multiProgress = DialogService.ShowProgress(_fetchTopLevel, "Installing Mod...", "Installing declared Mod... Please be patient...", ModService.CancelTokenSource, _firstCurrentPtr, _firstMaximumPtr, _firstTextPtr, _secondCurrentPtr, _secondMaximumPtr, _secondTextPtr);

                        for (int i = 0; i < _fetchMultiInstall.Length; i++)
                        {
                            var _fetchMod = _fetchMultiInstall[i];

                            var _fetchInstallStatus =
                            await ModService.InstallGit
                            (
                                _fetchMod,
                                CurrentConfig,
                                new TransferProgressHandler((progress) =>
                                {
                                    _firstTextPtr /= $"Processing Mod: {_fetchMod}";

                                    _firstCurrentPtr /= i + 0x01;
                                    _firstMaximumPtr /= _fetchMultiInstall.Length;

                                    _secondCurrentPtr /= progress.ReceivedObjects;
                                    _secondMaximumPtr /= progress.TotalObjects;

                                    if (ModService.CancelToken.IsCancellationRequested)
                                        return false;

                                    return true;
                                }
                            ));

                            if (_fetchInstallStatus == 0x01)
                                _fetchErroredList.Add(_fetchMod);

                            else
                                _fetchSuccessList.Add(_fetchMod);

                            if (ModService.CancelToken.IsCancellationRequested)
                                break;
                        }

                        // Free all the pointers allocated.

                        _secondTextPtr.Dispose();
                        _firstTextPtr.Dispose();

                        _secondMaximumPtr.Dispose();
                        _secondCurrentPtr.Dispose();

                        _firstMaximumPtr.Dispose();
                        _firstCurrentPtr.Dispose();

                        // Return the result.

                        _fetchInstallResult = 0x02;
                    }
                }

                if (ModService.CancelToken.IsCancellationRequested || _fetchInstallResult == 0x03)
                    return false;

                var _fetchGameNames = _fetchTopLevel.GameBox.Items.OfType<ComboBoxItem>()
                                                                  .Select(x => x.Content as string)
                                                                  .ToList();

                switch (_fetchInstallResult)
                {
                    case 0x00:
                    {
                        var _fetchModFolder = "";

                        var _fetchModAuthor = "";
                        var _fetchModName = "";

                        var _fetchGitMod = false;

                        if (_fetchFileInfo.Exists)
                            _fetchModFolder = Path.Combine(_fetchModPath, $"local/{Path.GetFileNameWithoutExtension(_fetchResult)}");

                        else
                        {
                            _fetchModAuthor = _fetchResult.Split('/').First();
                            _fetchModName = _fetchResult.Split('/').Last();

                            _fetchModName = _fetchModName.Split(':').First();
                            _fetchModName = _fetchModName.Split('@').First();

                            _fetchModFolder = Path.Combine(_fetchModPath, _fetchModAuthor, _fetchModName);
                            _fetchGitMod = true;
                        }

                        var _fetchPathGit = Path.Combine(_fetchModFolder, ".git");
                        var _fetchYamlName = Path.Combine(_fetchModFolder, "mod.yml");
                        var _fetchPathIcon = Path.Combine(_fetchModFolder, "icon.png");

                        var _fetchMetadata = Metadata.Read(_fetchYamlName);

                        if (_fetchMetadata.Game != null)
                        { 
                            var _fetchMetadataGame = Config.GameShorthand.FirstOrDefault(x => x.Value == _fetchMetadata.Game).Key;

                            var _fullNameMetadata = _fetchGameNames[(int)_fetchMetadataGame];
                            var _fullNameCurrent = _fetchGameNames[(int)CurrentConfig.Frontend.TargetGame];

                            if (_fetchMetadataGame != CurrentConfig.Frontend.TargetGame)
                            {
                                if (Directory.Exists(_fetchModFolder))
                                    Directory.Delete(_fetchModFolder, true);

                                await DialogService.ShowMessage(_fetchTopLevel, "Unsupported Game", $"This mod is made for {_fullNameMetadata} but you are trying to install it for {_fullNameCurrent}!\nPlease check to make sure the selected game matches the mod's requirements.", MessageType.ERROR);

                                return false;
                            }
                        }

                        if (_fetchMetadata.Dependencies != null)
                        {
                            var _fetchDependencies = _fetchMetadata.Dependencies;
                            var _fetchMissingDeps = new List<string>();

                            foreach (var _fetchDependency in _fetchDependencies)
                            {
                                var _doesHaveDependency = InstalledMods.FirstOrDefault(x => x.ModGitAddress != null && x.ModGitAddress.ToLower() == _fetchDependency.ToLower());

                                if (_doesHaveDependency == null)
                                    _fetchMissingDeps.Add(_fetchDependency);
                            }

                            if (_fetchMissingDeps.Count > 0)
                            {
                                var _fetchDepString = String.Join("\n- ", _fetchMissingDeps);
                                var _fetchDepQuestion = await DialogService.ShowQuestion(_fetchTopLevel, "Missing Dependencies", $"This Mod has some dependencies that are not installed.\nTo proceed, all the following dependencies must be installed:\n\n- {_fetchDepString}\n\nDo you want to proceed? [Not doing so will cancel this mod's install.]");

                                if (!_fetchDepQuestion)
                                {
                                    if (Directory.Exists(_fetchModFolder))
                                        Directory.Delete(_fetchModFolder, true);

                                    return false;
                                }

                                else
                                {
                                    var _fetchInstallStr = String.Join(";", _fetchMissingDeps);

                                    var _fetchInstall = await Install(_fetchInstallStr);

                                    if (!_fetchInstall)
                                    {
                                        if (Directory.Exists(_fetchModFolder))
                                            Directory.Delete(_fetchModFolder, true);

                                        return false;
                                    }
                                }
                            }
                        }

                        if (_fetchMetadata.IsValid)
                        {
                            var _modModel = new ModModel
                            {
                                ModTitle = !String.IsNullOrEmpty(_fetchMetadata.Title) ? _fetchMetadata.Title : _fetchModName,
                                ModAuthor = !String.IsNullOrEmpty(_fetchMetadata.OriginalAuthor) ? _fetchMetadata.OriginalAuthor : _fetchModAuthor,
                                ModGitAddress = _fetchGitMod ? _fetchResult : null,
                                ModDescription = !String.IsNullOrEmpty(_fetchMetadata.Description) ? _fetchMetadata.Description : "This mod does not have a description, but we believe it's pretty cool.",
                                ModPath = _fetchModFolder,
                                ModFilesList = _fetchMetadata.Assets.Select(x => x.Name).ToArray(),
                                ModIcon = File.Exists(_fetchPathIcon) ? new Bitmap(_fetchPathIcon) : null,
                                ModActive = true,
                                ModValid = true
                            };

                            if (_fetchMetadata.Preferences != null)
                            {
                                _modModel.ModPreferences = new List<PreferenceModel>();

                                foreach (var _preference in _fetchMetadata.Preferences)
                                {
                                    var _fetchPref = new PreferenceModel
                                    {
                                        Title = _preference.Title,
                                        Key = _preference.Key,
                                        Description = _preference.Description,
                                        Type = _preference.Type,
                                        Options = _preference.Options,
                                        Value = _preference.Value
                                    };

                                    _modModel.ModPreferences.Add(_fetchPref);
                                }
                            }

                            if (Directory.Exists(_fetchPathGit))
                            {
                                if (Repository.IsValid(_fetchPathGit))
                                {
                                    var _fetchGit = new Repository(_fetchPathGit);

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

                                    _fetchGit.Dispose();

                                    var _fetchGitDir = new DirectoryInfo(_fetchPathGit);

                                    foreach (var _fetchFile in _fetchGitDir.GetFiles("*", SearchOption.AllDirectories))
                                        if (_fetchFile.Exists)
                                            _fetchFile.Attributes &= ~FileAttributes.ReadOnly;
                                }
                            }

                            var _fetchExistingMod = InstalledMods.FirstOrDefault(x => x.ModPath == _modModel.ModPath);

                            if (_fetchExistingMod != null)
                                InstalledMods.Remove(_fetchExistingMod);

                            var _fetchFirstInvalid = InstalledMods.FirstOrDefault(x => !x.ModValid);
                            var _fetchModIndex = _fetchFirstInvalid != null ? InstalledMods.IndexOf(_fetchFirstInvalid) : 0x00;

                            InstalledMods.Insert(_fetchModIndex, _modModel);
                            HasModsInstalled = true;
                        }

                        else
                        {
                            var uri = new Uri("avares://OpenKh.Tools.ModManager/Assets/invalid_mod.png");

                            var _modModel = new ModModel
                            {
                                ModTitle = !String.IsNullOrEmpty(_fetchMetadata.Title) ? _fetchMetadata.Title : _fetchModName,
                                ModAuthor = "This mod is invalid!",
                                ModDescription = "This mod contains errors within its YAML file. Please check the formatting!",
                                ModIcon = new Bitmap(AssetLoader.Open(uri)),
                                ModPath = _fetchModFolder,
                                ModActive = false,
                                ModValid = false
                            };

                            var _fetchExistingMod = InstalledMods.FirstOrDefault(x => x.ModPath == _modModel.ModPath);

                            if (_fetchExistingMod != null)
                                InstalledMods.Remove(_fetchExistingMod);

                            InstalledMods.Add(_modModel);
                        }

                        return true;
                    }

                    case 0x01:
                        await DialogService.ShowMessage(_fetchTopLevel, "ERROR - Invalid Mod", "This is NOT a valid/compliant Mod Manager Mod. Please make sure it exists and it is valid.", MessageType.ERROR);
                        return false;

                    case 0x02:
                    {
                        var _fetchIncompatibleList = new List<Tuple<string, string>>();
                        var _fetchNoDependencyList = new List<string>();

                        foreach (var _fetchSuccess in _fetchSuccessList)
                        {
                            var _fetchModAuthor = _fetchSuccess.Split('/').First();
                            var _fetchModName = _fetchSuccess.Split('/').Last();

                            // Just in case we have platform and branch info, lose them.
                            _fetchModName = _fetchModName.Split(':').First();
                            _fetchModName = _fetchModName.Split('@').First();

                            var _fetchCurrentPath = Path.Combine(_fetchModPath, _fetchModAuthor, _fetchModName);

                            var _fetchPathGit = Path.Combine(_fetchCurrentPath, ".git");
                            var _fetchYamlName = Path.Combine(_fetchCurrentPath, "mod.yml");
                            var _fetchPathIcon = Path.Combine(_fetchCurrentPath, "icon.png");

                            var _fetchMetadata = Metadata.Read(_fetchYamlName);

                            if (_fetchMetadata.Game != null)
                            {
                                var _fetchMetadataGame = Config.GameShorthand.FirstOrDefault(x => x.Value == _fetchMetadata.Game).Key;

                                var _fullNameMetadata = _fetchGameNames[(int)_fetchMetadataGame];
                                var _fullNameCurrent = _fetchGameNames[(int)CurrentConfig.Frontend.TargetGame];

                                if (_fetchMetadataGame != CurrentConfig.Frontend.TargetGame)
                                {
                                    if (Directory.Exists(_fetchCurrentPath))
                                        Directory.Delete(_fetchCurrentPath, true);

                                    var _fetchGameTuple = new Tuple<string, string>(_fetchSuccess, _fullNameMetadata);
                                    _fetchIncompatibleList.Add(_fetchGameTuple);

                                    continue;
                                }
                            }

                            if (_fetchMetadata.Dependencies != null)
                            {
                                var _fetchDependencies = _fetchMetadata.Dependencies;
                                var _fetchMissingDeps = new List<string>();

                                foreach (var _fetchDependency in _fetchDependencies)
                                {
                                    var _doesHaveDependency = InstalledMods.FirstOrDefault(x => x.ModGitAddress != null && x.ModGitAddress.ToLower() == _fetchDependency.ToLower());

                                    if (_doesHaveDependency == null)
                                        _fetchMissingDeps.Add(_fetchDependency);
                                }

                                if (_fetchMissingDeps.Count > 0)
                                {
                                    var _fetchDepString = String.Join("\n- ", _fetchMissingDeps);
                                    var _fetchDepQuestion = await DialogService.ShowQuestion(_fetchTopLevel, "Missing Dependencies", $"This Mod has some dependencies that are not installed.\nTo proceed, all the following dependencies must be installed:\n\n- {_fetchDepString}\n\nDo you want to proceed? [Not doing so will cancel this mod's install.]");

                                    if (!_fetchDepQuestion)
                                    {
                                        if (Directory.Exists(_fetchCurrentPath))
                                            Directory.Delete(_fetchCurrentPath, true);

                                        _fetchNoDependencyList.Add(_fetchSuccess);
                                        continue;
                                    }

                                    else
                                    {
                                        var _fetchInstallStr = String.Join(";", _fetchMissingDeps);
                                        var _fetchInstall = await Install(_fetchInstallStr);

                                        if (!_fetchInstall)
                                        {
                                            if (Directory.Exists(_fetchCurrentPath))
                                                Directory.Delete(_fetchCurrentPath, true);

                                            _fetchNoDependencyList.Add(_fetchSuccess);
                                            continue;
                                        }
                                    }
                                }
                            }

                            if (_fetchMetadata.IsValid)
                            {
                                var _modModel = new ModModel
                                {
                                    ModTitle = _fetchMetadata.Title,
                                    ModAuthor = _fetchMetadata.OriginalAuthor,
                                    ModDescription = _fetchMetadata.Description,
                                    ModGitAddress = _fetchSuccess,
                                    ModPath = _fetchCurrentPath,
                                    ModFilesList = _fetchMetadata.Assets.Select(x => x.Name).ToArray(),
                                    ModIcon = File.Exists(_fetchPathIcon) ? new Bitmap(_fetchPathIcon) : null,
                                    ModActive = true,
                                    ModValid = true
                                };

                                if (_fetchMetadata.Preferences != null)
                                {
                                    _modModel.ModPreferences = new List<PreferenceModel>();

                                    foreach (var _preference in _fetchMetadata.Preferences)
                                    {
                                        var _fetchPref = new PreferenceModel
                                        {
                                            Title = _preference.Title,
                                            Key = _preference.Key,
                                            Description = _preference.Description,
                                            Type = _preference.Type,
                                            Options = _preference.Options,
                                            Value = _preference.Value
                                        };

                                        _modModel.ModPreferences.Add(_fetchPref);
                                    }
                                }

                                if (Directory.Exists(_fetchPathGit))
                                {
                                    if (Repository.IsValid(_fetchPathGit))
                                    {
                                        var _fetchGit = new Repository(_fetchPathGit);

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

                                        _fetchGit.Dispose();

                                        var _fetchGitDir = new DirectoryInfo(_fetchPathGit);

                                        foreach (var _fetchFile in _fetchGitDir.GetFiles("*", SearchOption.AllDirectories))
                                            if (_fetchFile.Exists)
                                                _fetchFile.Attributes &= ~FileAttributes.ReadOnly;
                                    }
                                }

                                var _fetchExistingMod = InstalledMods.FirstOrDefault(x => x.ModPath == _modModel.ModPath);

                                if (_fetchExistingMod != null)
                                    InstalledMods.Remove(_fetchExistingMod);

                                var _fetchFirstInvalid = InstalledMods.FirstOrDefault(x => !x.ModValid);
                                var _fetchModIndex = _fetchFirstInvalid != null ? InstalledMods.IndexOf(_fetchFirstInvalid) : 0x00;

                                InstalledMods.Insert(_fetchModIndex, _modModel);
                                HasModsInstalled = true;
                            }

                            else
                            {
                                var uri = new Uri("avares://OpenKh.Tools.ModManager/Assets/invalid_mod.png");

                                var _modModel = new ModModel
                                {
                                    ModTitle = _fetchMetadata.Title,
                                    ModAuthor = "This mod is invalid!",
                                    ModDescription = "This mod contains errors within its YAML file. Please check the formatting!",
                                    ModIcon = new Bitmap(AssetLoader.Open(uri)),
                                    ModPath = _fetchCurrentPath,
                                    ModActive = false,
                                    ModValid = false
                                };

                                var _fetchExistingMod = InstalledMods.FirstOrDefault(x => x.ModPath == _modModel.ModPath);

                                if (_fetchExistingMod != null)
                                    InstalledMods.Remove(_fetchExistingMod);

                                InstalledMods.Add(_modModel);
                            }
                        }

                        if (_fetchErroredList.Count > 0)
                        {
                            var _fetchError = String.Join('\n', _fetchErroredList);
                            await DialogService.ShowMessage(_fetchTopLevel, "Some Mods were invalid!", $"The following mods were invalid and thus were not installed:\n {_fetchError}", MessageType.WARNING);
                        }

                        return true;
                    }

                    default:
                        return false;
                }
            }

            else
                return false;
        }

        else
            return false;
    }

    [RelayCommand]
    private async Task<bool> Remove()
    {
        if (CurrentMod != null && InstalledMods != null)
        {
            var _fetchTopLevel = FetchTopLevel() as Window;

            if (_fetchTopLevel == null)
                return false;

            bool? _fetchResult = await DialogService.ShowQuestion(_fetchTopLevel, "Remove Mod?", "Are you sure you want to remove the selected mod?");

            // If they indeed do:

            if (_fetchResult == true)
            {
                // Fetch the path too we kinda need it.
                var _fetchModPath = CurrentMod.ModPath;

                // Remove the mod from the list.
                InstalledMods.Remove(CurrentMod);

                // If we still have mods, select one.
                if (InstalledMods.Count > 0)
                    CurrentMod = InstalledMods.First();

                // If we do not, nullify selection.
                else
                {
                    HasModsInstalled = false;
                    CurrentMod = null;
                }

                // Remove the mod directory.
                // This is a task for a reason. This isn't being awaited for a reason. Do not listen to IntelliSense on this one.
                Task.Run(() =>
                {
                    if (Directory.Exists(_fetchModPath))
                        Directory.Delete(_fetchModPath, true);
                });

                return true;
            }

            else
                return false;
        }

        else
            return false;
    }

    [RelayCommand]
    private void Toggle(Tuple<ModModel, bool> inputParameter)
    {
        if (CurrentConfig != null && InstalledMods != null)
        {
            var _fetchMod = inputParameter.Item1;
            var _fetchIsChecked = inputParameter.Item2;

            var _fetchMemoryPath = Path.Combine(PathService.ResolveMod(CurrentConfig), "mod_memory.yml");

            var _fetchMemoryRAW = File.Exists(_fetchMemoryPath) ? File.ReadAllText(_fetchMemoryPath) : "[]";
            var _fetchMemory = YamlSerializer.Deserialize<ObservableCollection<MemoryModel>>(_fetchMemoryRAW);

            if (_fetchMemory == null)
                return;

            var _fetchSenderHash = ModService.ResolveMD5(_fetchMod, CurrentConfig);
            var _fetchMemoryItem = _fetchMemory.FirstOrDefault(x => x.ModHash == _fetchSenderHash);

            if (_fetchMemoryItem == null)
            {
                _fetchMemoryItem = new MemoryModel
                {
                    ModHash = _fetchSenderHash,
                    ModActive = _fetchIsChecked,
                    ModIndex = InstalledMods.IndexOf(inputParameter.Item1)
                };

                _fetchMemory.Add(_fetchMemoryItem);
            }

            else
            {
                var _fetchMemoryIndex = _fetchMemory.IndexOf(_fetchMemoryItem);
                _fetchMemory[_fetchMemoryIndex].ModActive = _fetchIsChecked;
            }

            var _fetchSerial = YamlSerializer.Serialize(_fetchMemory);
            File.WriteAllText(_fetchMemoryPath, _fetchSerial);
        }
    }


    [RelayCommand]
    private void MoveUtmost()
    {
        if (InstalledMods != null && CurrentMod != null)
        {
            var _fetchModIndex = InstalledMods.IndexOf(CurrentMod);
            InstalledMods.Move(_fetchModIndex, 0);

            CurrentMod = InstalledMods[0];
        }
    }

    [RelayCommand]
    private void MoveUp()
    {
        if (InstalledMods != null && CurrentMod != null)
        {
            var _fetchModIndex = InstalledMods.IndexOf(CurrentMod);

            if (_fetchModIndex != 0 && CurrentMod.ModValid)
            {
                InstalledMods.Move(_fetchModIndex, _fetchModIndex - 1);
                CurrentMod = InstalledMods[_fetchModIndex - 1];
            }
        }
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (InstalledMods != null && CurrentMod != null)
        {
            var _fetchModIndex = InstalledMods.IndexOf(CurrentMod);

            if ( _fetchModIndex != InstalledMods.Count - 1 && CurrentMod.ModValid)
            {
                InstalledMods.Move(_fetchModIndex, _fetchModIndex + 1);
                CurrentMod = InstalledMods[_fetchModIndex + 1];
            }
        }
    }

    [RelayCommand]
    private async Task<bool> OpenFolder()
    {
        if (CurrentMod != null)
        {
            var _fetchTopLevel = FetchTopLevel();

            if (_fetchTopLevel == null)
                return false;

            var _fetchDirectoryInfo = new DirectoryInfo(CurrentMod.ModPath);

            if (_fetchTopLevel?.Launcher != null)
                await _fetchTopLevel.Launcher.LaunchDirectoryInfoAsync(_fetchDirectoryInfo);

            return true;
        }

        else
            return false;
    }


    [RelayCommand]
    private async Task<bool> Run()
    {
        if (CurrentConfig != null)
        {
            var _fetchTopLevel = FetchTopLevel();

            if (_fetchTopLevel == null)
                return false;

            return await ModService.Run(CurrentConfig, _fetchTopLevel);
        }

        else
            return false;
    }

    [RelayCommand]
    private async Task<bool> Build()
    {
        if (CurrentConfig != null && InstalledMods != null)
        {
            var _fetchTopLevel = FetchTopLevel() as Window;

            if (_fetchTopLevel == null)
                return false;

            // Allocate all the pointers to use for the progress bars.

            SafePtr _firstCurrentPtr = 0x00;
            SafePtr _firstMaximumPtr = 0x00;

            SafePtr _secondCurrentPtr = 0x00;
            SafePtr _secondMaximumPtr = 0x00;

            SafePtr _firstTextPtr = "Currently building: N/A";
            SafePtr _secondTextPtr = "Processing Files: {0} / {3}";

            // Show the dialog.

            var _multiProgress = DialogService.ShowProgress(_fetchTopLevel, "Installing Mod...", "Installing declared Mod... Please be patient...", ModService.CancelTokenSource, _firstCurrentPtr, _firstMaximumPtr, _firstTextPtr, _secondCurrentPtr, _secondMaximumPtr, _secondTextPtr);

            string _currentModName = "N/A";

            var _buildResult =
                await ModService.Build
                (
                    InstalledMods,
                    CurrentConfig,

                    (string currModName, int procMod, int totalMod) =>
                    {
                        _currentModName = currModName;

                        _firstTextPtr /= $"Currently building: {currModName}";

                        _firstCurrentPtr /= procMod;
                        _firstMaximumPtr /= totalMod;

                        if (ModService.CancelToken.IsCancellationRequested)
                            return false;

                        return true;
                    },

                    (int processed, int total) =>
                    {
                        _secondCurrentPtr /= processed;
                        _secondMaximumPtr /= total;

                        if (ModService.CancelToken.IsCancellationRequested)
                            return false;

                        return true;
                    }
                );

            // Free all the pointers allocated.

            _secondTextPtr.Dispose();
            _firstTextPtr.Dispose();

            _secondMaximumPtr.Dispose();
            _secondCurrentPtr.Dispose();

            _firstMaximumPtr.Dispose();
            _firstCurrentPtr.Dispose();

            if (_buildResult == 0x00)
                return true;

            else if (_buildResult == 0x01)
                await DialogService.ShowMessage(_fetchTopLevel, "ERROR - Failed to build Mod", string.Format("Building failed on this Mod: {0}\nPlease re-install the Mod and try again!", _currentModName), MessageType.ERROR);
            return false;
        }

        else
            return false;
    }

    [RelayCommand]
    private async Task<bool> Restore()
    {
        if (CurrentConfig != null)
        {
            var _fetchTopLevel = FetchTopLevel() as Window;

            if (_fetchTopLevel == null)
                return false;

            var _fetchBuildDir = PathService.ResolveBuild(CurrentConfig);
            Directory.Delete(_fetchBuildDir, true);

            await DialogService.ShowMessage(_fetchTopLevel, "Restore Completed!", "Restoration complete! The game should run as if it isn't modded!", MessageType.INFO);

            return true;
        }

        else
            return false;
    }

    [RelayCommand]
    private async Task<bool> BuildRun()
    {
        if (CurrentConfig != null && InstalledMods != null)
        {
            var _buildResult = await Build();

            if (!_buildResult)
                return false;

            var _runResult = await Run();

            if (!_runResult)
                return false;

            return true;
        }

        else
            return false;
    }


    [RelayCommand]
    private async Task<bool> InstallPanacea()
    {
        if (CurrentConfig != null)
        {
            var _fetchTopLevel = FetchTopLevel() as Window;

            if (_fetchTopLevel == null)
                return false;

            PackageService.InstallPanacea(CurrentConfig);
            ConfigurationValid = CurrentConfig.IsValid();

            await DialogService.ShowMessage(_fetchTopLevel, "Panacea Installed!", "Panacea has been installed in accordance to current settings.", MessageType.INFO);
            return true;
        }

        else
            return false;
    }

    [RelayCommand]
    private async Task<bool> InstallBackend()
    {
        if (CurrentConfig != null)
        {
            var _fetchTopLevel = FetchTopLevel() as Window;

            if (_fetchTopLevel == null)
                return false;

            PackageService.InstallBackend(CurrentConfig);
            ConfigurationValid = CurrentConfig.IsValid();

            await DialogService.ShowMessage(_fetchTopLevel, "LuaBackend Installed!", "LuaBackend has been installed in accordance to current settings.", MessageType.INFO);
            return true;
        }

        else
            return false;
    }


    [RelayCommand]
    private async Task<bool> LaunchSetup()
    {
        if (CurrentConfig != null && InstalledMods != null)
        {
            var _fetchTopLevel = FetchTopLevel() as Window;

            if (_fetchTopLevel == null)
                return false;

            await new SetupWizardView().ShowDialog(_fetchTopLevel);
            return true;
        }

        else
            return false;
    }

    [RelayCommand]
    private async Task<bool> LaunchCatalog()
    {
        if (CurrentConfig != null && InstalledMods != null)
        {
            var _fetchTopLevel = FetchTopLevel() as Window;

            if (_fetchTopLevel == null)
                return false;

            var _catalogWindow = new CatalogView();
            var _fetchResult = await _catalogWindow.ShowDialog<string?>(_fetchTopLevel);

            if (!String.IsNullOrEmpty(_fetchResult) && InstallCommand.CanExecute(_fetchResult))
            {
                InstallCommand.Execute(_fetchResult);
                return true;
            }

            else
                return false;
        }

        else
            return false;
    }

    [RelayCommand]
    private async Task<bool> PromptArguments()
    {
        if (CurrentConfig != null)
        {
            var _fetchTopLevel = FetchTopLevel() as Window;

            if (_fetchTopLevel == null)
                return false;

            var _fetchResult = await DialogService.ShowInput(_fetchTopLevel, "Declare Launch Aruguments", "Enter the launch arguments to use when launching on Steam.", "Done", "Ex. -fastboot -noaspect", CurrentConfig.Frontend.LaunchArguments);

            if (!String.IsNullOrEmpty(_fetchResult))
                CurrentConfig.Frontend.LaunchArguments = _fetchResult;

            return true;
        }

        else
            return false;
    }

    [RelayCommand]
    private async Task<bool> CreatePreset()
    {
        if (CurrentConfig != null)
        {
            var _fetchGame = (int)CurrentConfig.Frontend.TargetGame;
            var _fetchPreset = CurrentConfig.Frontend.TargetPreset[_fetchGame];

            var _fetchTopLevel = FetchTopLevel() as Window;

            if (_fetchTopLevel == null)
                return false;

            var _fetchResult = await DialogService.ShowInput(_fetchTopLevel, "Create New Preset", "Please enter a name for the new preset.", "OK", "Ex. \"Some Very Cool Preset\"");

            if (!String.IsNullOrEmpty(_fetchResult))
            {
                var _isPresetPresent = PresetItems.OfType<MenuItem>().FirstOrDefault(x => x.Header as string == "Default") != null;

                if (!_isPresetPresent)
                {
                    PresetItems.RemoveAt(0x02);

                    PresetItems.Add(new MenuItem()
                    {
                        Header = "Default",
                        ToggleType = MenuItemToggleType.Radio,
                        IsChecked = true,
                        Command = SwitchPresetCommand,
                        CommandParameter = "",
                        InputGesture = new KeyGesture(Key.D, KeyModifiers.Alt)
                    });

                    PresetItems.Add(new MenuItem()
                    {
                        Header = _fetchResult,
                        ToggleType = MenuItemToggleType.Radio,
                        IsChecked = false,
                        Command = SwitchPresetCommand,
                        CommandParameter = _fetchResult,
                        ContextMenu = new ContextMenu
                        {
                            ItemsSource = new[]
                            {
                                new MenuItem
                                {
                                    Header = "Rename Preset...",
                                    Command = RenamePresetCommand,
                                    CommandParameter = _fetchResult
                                },

                                new MenuItem
                                {
                                    Header = "Delete Preset...",
                                    Command = DeletePresetCommand,
                                    CommandParameter = _fetchResult
                                }
                            }
                        },
                        InputGesture = new KeyGesture(Key.D0, KeyModifiers.Alt)
                    });
                }

                else
                {
                    PresetItems.Add(new MenuItem()
                    {
                        Header = _fetchResult,
                        ToggleType = MenuItemToggleType.Radio,
                        IsChecked = false,
                        Command = SwitchPresetCommand,
                        CommandParameter = _fetchResult,
                        ContextMenu = new ContextMenu
                        {
                            ItemsSource = new[]
                            {
                                new MenuItem
                                {
                                    Header = "Rename Preset...",
                                    Command = RenamePresetCommand,
                                    CommandParameter = _fetchResult
                                },

                                new MenuItem
                                {
                                    Header = "Delete Preset...",
                                    Command = DeletePresetCommand,
                                    CommandParameter = _fetchResult
                                }
                            }
                        },
                        InputGesture = new KeyGesture((Key)(34 + PresetItems.Count - 0x03), KeyModifiers.Alt)
                    });
                }

                var _fetchPresetPath = Path.Combine(AppContext.BaseDirectory, "preset.yml");

                var _fetchPresetRAW = File.ReadAllText(_fetchPresetPath);
                var _fetchPresetList = YamlSerializer.Deserialize<List<PresetModel>>(_fetchPresetRAW);

                _fetchPresetList.Add(new PresetModel
                {
                    PresetName = _fetchResult,
                    PresetGame = CurrentConfig.Frontend.TargetGame
                });

                _fetchPresetRAW = YamlSerializer.Serialize(_fetchPresetList);
                File.WriteAllText(_fetchPresetPath, _fetchPresetRAW);
            }
        }

        return false;
    }

    [RelayCommand]
    private async Task<bool> SwitchPreset(string input)
    {
        if (CurrentConfig != null)
        {
            var _fetchGame = (int)CurrentConfig.Frontend.TargetGame;
            var _fetchPreset = CurrentConfig.Frontend.TargetPreset[_fetchGame];

            if (_fetchPreset == input)
                return false;

            CurrentConfig.Frontend.TargetPreset[_fetchGame] = input;
            InitializeMods();

            return true;
        }

        return false;
    }

    [RelayCommand]
    private async Task<bool> DeletePreset(string input)
    {
        if (CurrentConfig != null)
        {
            var _fetchGame = (int)CurrentConfig.Frontend.TargetGame;
            var _fetchPreset = CurrentConfig.Frontend.TargetPreset[_fetchGame];

            var _fetchTopLevel = FetchTopLevel() as Window;

            if (_fetchTopLevel == null)
                return false;

            var _fetchResult = await DialogService.ShowQuestion(_fetchTopLevel, "Delete this Preset?", $"Are you sure you want to delete the preset \"{input}\"?\nIf it is currently active, the Default preset will be loaded.");

            if (_fetchResult)
            {
                if (_fetchPreset == input)
                {
                    CurrentConfig.Frontend.TargetPreset[_fetchGame] = "";
                    (PresetItems[0x02] as MenuItem).IsChecked = true;
                }

                var _fetchPresetPath = Path.Combine(AppContext.BaseDirectory, "preset.yml");

                if (File.Exists(_fetchPresetPath))
                {
                    var _fetchPresetRAW = File.ReadAllText(_fetchPresetPath);
                    var _fetchPresetList = YamlSerializer.Deserialize<List<PresetModel>>(_fetchPresetRAW);

                    var _fetchPresetItem = _fetchPresetList.FirstOrDefault(x => x.PresetName == input);
                    var _fetchPresetMenu = PresetItems.OfType<MenuItem>().FirstOrDefault(x => x.Header == input);

                    _fetchPresetList.Remove(_fetchPresetItem);
                    PresetItems.Remove(_fetchPresetMenu);

                    if (PresetItems.Count > 3)
                    {
                        var _fetchKeyIndex = 34;

                        for (int i = 0x03; i < PresetItems.Count; i++)
                            (PresetItems[i] as MenuItem).InputGesture = new KeyGesture((Key)(_fetchKeyIndex++), KeyModifiers.Alt);
                    }

                    else
                    {
                        PresetItems[0x02] = new MenuItem()
                        {
                            Header = "No Presets Available.",
                            IsEnabled = false
                        };
                    }

                    Task.Run(() =>
                    {
                        var _fetchNormalStr = input.ToLower().Replace(" ", "_");

                        foreach (var _fetchChar in Path.GetInvalidFileNameChars())
                            _fetchNormalStr = _fetchNormalStr.Replace(_fetchChar, '-');

                        var _fetchFinalPath = Path.Combine(PathService.ResolvePreset(CurrentConfig, false), _fetchNormalStr);

                        if (Directory.Exists(_fetchFinalPath))
                            Directory.Delete(_fetchFinalPath, true);

                    });

                    var _fetchSerial = YamlSerializer.Serialize(_fetchPresetList);
                    File.WriteAllText(_fetchPresetPath, _fetchSerial);

                    InitializeMods();
                }
            }

            return true;
        }

        return false;
    }

    [RelayCommand]
    private async Task<bool> RenamePreset(string input)
    {
        if (CurrentConfig != null)
        {
            var _fetchGame = (int)CurrentConfig.Frontend.TargetGame;
            var _fetchPreset = CurrentConfig.Frontend.TargetPreset[_fetchGame];

            var _fetchTopLevel = FetchTopLevel() as Window;

            if (_fetchTopLevel == null)
                return false;

            var _fetchResult = await DialogService.ShowInput(_fetchTopLevel, "Rename Preset", "Please enter a new name for this preset.", "Rename", "", input);

            if (_fetchResult != null)
            {
                return true;
            }
        }

        return false;
    }
}

#pragma warning restore CS4014
