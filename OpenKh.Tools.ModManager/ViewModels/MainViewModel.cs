#pragma warning disable CS4014

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
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
using static OpenKh.Kh2.Ard.AreaDataScript;
using static System.Net.Mime.MediaTypeNames;

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
                        TargetGame = Game.KINGDOM_HEARTS_II
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

        // === Mod Parsing and Verification === //

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
            var _fetchTopLevel = FetchTopLevel() as Window;

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
                    IntPtr _progressCurrentPtr = Marshal.AllocHGlobal(4);
                    IntPtr _progressMaximumPtr = Marshal.AllocHGlobal(4);

                    IntPtr _progressTextPtr = Marshal.AllocHGlobal(256);

                    // Write the default strings to the pointers.

                    var _fetchProgressBytes = Encoding.Default.GetBytes("Processing Local Files: {0} / {3}" + "\x00");
                    Marshal.Copy(_fetchProgressBytes, 0, _progressTextPtr, _fetchProgressBytes.Length);

                    // Write the default ints to the pointers.

                    Marshal.WriteInt32(_progressCurrentPtr, 0x00);
                    Marshal.WriteInt32(_progressMaximumPtr, 0x00);

                    var _singleProgress = DialogService.ShowProgress(_fetchTopLevel, "Installing Mod...", "Installing declared Mod... Please be patient...", ModService.CancelTokenSource, _progressCurrentPtr, _progressMaximumPtr, _progressTextPtr);

                    _fetchInstallResult =
                    await ModService.InstallLocal
                    (
                        _fetchResult,
                        CurrentConfig,
                        (int processed, int total) =>
                        {
                            Marshal.WriteInt32(_progressCurrentPtr, processed);
                            Marshal.WriteInt32(_progressMaximumPtr, total);

                            if (ModService.CancelToken.IsCancellationRequested)
                                return false;

                            return true;
                        }
                    );

                    // Free all the pointers allocated.

                    Marshal.FreeHGlobal(_progressTextPtr);

                    Marshal.FreeHGlobal(_progressMaximumPtr);
                    Marshal.FreeHGlobal(_progressCurrentPtr);
                }

                else
                {
                    var _fetchMultiInstall = _fetchResult.Contains(';') ? _fetchResult.Split(';') : null;

                    if (_fetchMultiInstall == null)
                    {
                        // Allocate the pointers necessary.

                        IntPtr _progressCurrentPtr = Marshal.AllocHGlobal(4);
                        IntPtr _progressMaximumPtr = Marshal.AllocHGlobal(4);

                        IntPtr _progressTextPtr = Marshal.AllocHGlobal(256);

                        // Write the default strings to the pointers.

                        var _fetchProgressBytes = Encoding.Default.GetBytes("Receiving Git Objects: {0} / {3}" + "\x00");
                        Marshal.Copy(_fetchProgressBytes, 0, _progressTextPtr, _fetchProgressBytes.Length);

                        // Write the default ints to the pointers.

                        Marshal.WriteInt32(_progressCurrentPtr, 0x00);
                        Marshal.WriteInt32(_progressMaximumPtr, 0x00);


                        var _singleProgress = DialogService.ShowProgress(_fetchTopLevel, "Installing Mod...", "Installing declared Mod... Please be patient...", ModService.CancelTokenSource, _progressCurrentPtr, _progressMaximumPtr, _progressTextPtr);

                        _fetchInstallResult =
                        await ModService.InstallGit
                        (
                            _fetchResult,
                            CurrentConfig,
                            new TransferProgressHandler((progress) =>
                            {
                                Marshal.WriteInt32(_progressCurrentPtr, progress.ReceivedObjects);
                                Marshal.WriteInt32(_progressMaximumPtr, progress.TotalObjects);

                                if (ModService.CancelToken.IsCancellationRequested)
                                    return false;

                                return true;
                            }
                        ));

                        // Free all the pointers allocated.

                        Marshal.FreeHGlobal(_progressTextPtr);

                        Marshal.FreeHGlobal(_progressMaximumPtr);
                        Marshal.FreeHGlobal(_progressCurrentPtr);
                    }

                    else
                    {
                        // Allocate all the pointers to use for the progress bars.

                        IntPtr _firstCurrentPtr = Marshal.AllocHGlobal(4);
                        IntPtr _firstMaximumPtr = Marshal.AllocHGlobal(4);

                        IntPtr _secondCurrentPtr = Marshal.AllocHGlobal(4);
                        IntPtr _secondMaximumPtr = Marshal.AllocHGlobal(4);

                        IntPtr _firstTextPtr = Marshal.AllocHGlobal(256);
                        IntPtr _secondTextPtr = Marshal.AllocHGlobal(256);

                        // Write the default strings to the pointers.

                        var _fetchFirstBytes = Encoding.Default.GetBytes($"Processing Mod: N/A" + "\x00");
                        var _fetchSecondBytes = Encoding.Default.GetBytes("Receiving Git Objects: {0} / {3}" + "\x00");

                        Marshal.Copy(_fetchFirstBytes, 0, _firstTextPtr, _fetchFirstBytes.Length);
                        Marshal.Copy(_fetchSecondBytes, 0, _secondTextPtr, _fetchSecondBytes.Length);

                        // Write the default ints to the pointers.

                        Marshal.WriteInt32(_firstCurrentPtr, 0x00);
                        Marshal.WriteInt32(_firstMaximumPtr, 0x00);

                        Marshal.WriteInt32(_secondCurrentPtr, 0x00);
                        Marshal.WriteInt32(_secondMaximumPtr, 0x00);

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
                                    var _fetchFirstBytes = Encoding.Default.GetBytes($"Processing Mod: {_fetchMod}" + "\x00");
                                    Marshal.Copy(_fetchFirstBytes, 0, _firstTextPtr, _fetchFirstBytes.Length);

                                    Marshal.WriteInt32(_firstCurrentPtr, i + 0x01);
                                    Marshal.WriteInt32(_firstMaximumPtr, _fetchMultiInstall.Length);

                                    Marshal.WriteInt32(_secondCurrentPtr, progress.ReceivedObjects);
                                    Marshal.WriteInt32(_secondMaximumPtr, progress.TotalObjects);

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

                        Marshal.FreeHGlobal(_secondTextPtr);
                        Marshal.FreeHGlobal(_firstTextPtr);

                        Marshal.FreeHGlobal(_secondMaximumPtr);
                        Marshal.FreeHGlobal(_secondCurrentPtr);

                        Marshal.FreeHGlobal(_firstMaximumPtr);
                        Marshal.FreeHGlobal(_firstCurrentPtr);

                        // Return the result.

                        _fetchInstallResult = 0x02;
                    }
                }

                if (ModService.CancelToken.IsCancellationRequested || _fetchInstallResult == 0x03)
                    return false;

                switch (_fetchInstallResult)
                {
                    case 0x00:
                    {
                        var _fetchModFolder = "";

                        var _fetchModAuthor = "";
                        var _fetchModName = "";

                        if (_fetchFileInfo.Exists)
                            _fetchModFolder = Path.Combine(_fetchModPath, $"local/{Path.GetFileNameWithoutExtension(_fetchResult)}");

                        else
                        {
                            _fetchModAuthor = _fetchResult.Split('/').First();
                            _fetchModName = _fetchResult.Split('/').Last();

                            _fetchModName = _fetchModName.Split(':').First();
                            _fetchModName = _fetchModName.Split('@').First();

                            _fetchModFolder = Path.Combine(_fetchModPath, _fetchModAuthor, _fetchModName);
                        }

                        var _fetchPathGit = Path.Combine(_fetchModFolder, ".git");
                        var _fetchYamlName = Path.Combine(_fetchModFolder, "mod.yml");
                        var _fetchPathIcon = Path.Combine(_fetchModFolder, "icon.png");

                        var _fetchMetadata = Metadata.Read(_fetchYamlName);

                        if (_fetchMetadata.IsValid)
                        {
                            var _modModel = new ModModel
                            {
                                ModTitle = !String.IsNullOrEmpty(_fetchMetadata.Title) ? _fetchMetadata.Title : _fetchModName,
                                ModAuthor = !String.IsNullOrEmpty(_fetchMetadata.OriginalAuthor) ? _fetchMetadata.OriginalAuthor : _fetchModAuthor,
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

                            if (_fetchMetadata.IsValid)
                            {
                                var _modModel = new ModModel
                                {
                                    ModTitle = _fetchMetadata.Title,
                                    ModAuthor = _fetchMetadata.OriginalAuthor,
                                    ModDescription = _fetchMetadata.Description,
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

            IntPtr _firstCurrentPtr = Marshal.AllocHGlobal(4);
            IntPtr _firstMaximumPtr = Marshal.AllocHGlobal(4);

            IntPtr _secondCurrentPtr = Marshal.AllocHGlobal(4);
            IntPtr _secondMaximumPtr = Marshal.AllocHGlobal(4);

            IntPtr _firstTextPtr = Marshal.AllocHGlobal(256);
            IntPtr _secondTextPtr = Marshal.AllocHGlobal(256);

            // Write the default strings to the pointers.

            var _fetchFirstBytes = Encoding.Default.GetBytes("Currently building: N/A" + "\x00");
            var _fetchSecondBytes = Encoding.Default.GetBytes("Processing Files: {0} / {3}" + "\x00");

            Marshal.Copy(_fetchFirstBytes, 0, _firstTextPtr, _fetchFirstBytes.Length);
            Marshal.Copy(_fetchSecondBytes, 0, _secondTextPtr, _fetchSecondBytes.Length);

            // Write the default ints to the pointers.

            Marshal.WriteInt32(_firstCurrentPtr, 0x00);
            Marshal.WriteInt32(_firstMaximumPtr, 0x00);

            Marshal.WriteInt32(_secondCurrentPtr, 0x00);
            Marshal.WriteInt32(_secondMaximumPtr, 0x00);

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

                        var _fetchFirstBytes = Encoding.Default.GetBytes($"Currently building: {currModName}" + "\x00");
                        Marshal.Copy(_fetchFirstBytes, 0, _firstTextPtr, _fetchFirstBytes.Length);

                        Marshal.WriteInt32(_firstCurrentPtr, procMod);
                        Marshal.WriteInt32(_firstMaximumPtr, totalMod);

                        if (ModService.CancelToken.IsCancellationRequested)
                            return false;

                        return true;
                    },

                    (int processed, int total) =>
                    {
                        Marshal.WriteInt32(_secondCurrentPtr, processed);
                        Marshal.WriteInt32(_secondMaximumPtr, total);

                        if (ModService.CancelToken.IsCancellationRequested)
                            return false;

                        return true;
                    }
                );

            // Free all the pointers allocated.

            Marshal.FreeHGlobal(_secondTextPtr);
            Marshal.FreeHGlobal(_firstTextPtr);

            Marshal.FreeHGlobal(_secondMaximumPtr);
            Marshal.FreeHGlobal(_secondCurrentPtr);

            Marshal.FreeHGlobal(_firstMaximumPtr);
            Marshal.FreeHGlobal(_firstCurrentPtr);

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

}

#pragma warning restore CS4014
