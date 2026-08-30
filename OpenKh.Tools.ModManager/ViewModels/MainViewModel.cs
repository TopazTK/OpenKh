using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using LibGit2Sharp;
using OpenKh.Patcher;
using OpenKh.Tools.ModManager.Models;
using OpenKh.Tools.ModManager.Services;
using SharpYaml;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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
    private Config? _currentConfig = null;

    [ObservableProperty]
    private double _installProgress = 0;

    protected void OnModPropertyChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var _fetchModList = sender as ObservableCollection<ModModel>;
        var _fetchModsPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "mods", CurrentConfig.TargetGame.ToString().ToLower());

        var _fetchMemory = new ObservableCollection<MemoryModel>();

        foreach (var _fetchMod in _fetchModList)
        {
            var _fetchRelativePath = Path.GetRelativePath(System.AppDomain.CurrentDomain.BaseDirectory, _fetchMod.ModPath);

            var _fetchBytes = Encoding.ASCII.GetBytes(_fetchRelativePath);
            var _fetchHash = MD5.HashData(_fetchBytes);

            var _fetchHashStr = Convert.ToHexString(_fetchHash);

            _fetchMemory.Add(new MemoryModel { ModHash = _fetchHashStr, ModActive = _fetchMod.ModActive, ModIndex = _fetchModList.IndexOf(_fetchMod) });
        }

        var _fetchSerial = YamlSerializer.Serialize(_fetchMemory);
        File.WriteAllText(Path.Combine(_fetchModsPath, "mod_memory.yml"), _fetchSerial);
    }

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
            CurrentConfig = YamlSerializer.Deserialize<Config>(_fetchConfigFile);
        }

        // Handle these if the platform is NOT set to PCSX2.

        if (CurrentConfig.TargetPlatform != Platform.PCSX2)
        {
            // Construct the paths for the game executable and Panacea.
            var _gameExecutablePath = Path.Combine(CurrentConfig.GamePath, Config.GameExecutable[CurrentConfig.TargetGame]);
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
                            _fetchFile.Attributes &= ~FileAttributes.ReadOnly;
                    }
                }

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
        InstalledMods.CollectionChanged += OnModPropertyChanged;

        // If there is at least one mod, select the first mod.
        if (InstalledMods.Count > 0)
            CurrentMod = InstalledMods.First();
    }

    public MainViewModel()
    {
        InitializeView();
    }
}
