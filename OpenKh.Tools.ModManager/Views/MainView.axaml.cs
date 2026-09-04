using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using OpenKh.Patcher;
using OpenKh.Tools.ModManager.Dialogs;
using OpenKh.Tools.ModManager.Models;
using OpenKh.Tools.ModManager.Classes;
using OpenKh.Tools.ModManager.Services;
using OpenKh.Tools.ModManager.ViewModels;
using OpenKh.Tools.ModManager.Views;
using SharpYaml;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media;
using System.Collections.Generic;

namespace OpenKh.Tools.ModManager.Views;

public partial class MainView : Window
{
    public MainView()
    {
        InitializeComponent();
        Loaded += OnViewLoaded;
    }

    private async void OnViewLoaded(object? sender, RoutedEventArgs e)
    {
        // Yea so Avalonia lists do not automatically highlight shit if they ain't empty so we gotta do it here.
        // If this ever breaks that means something is wrong.

        var _fetchContext = DataContext as MainViewModel;

        if (_fetchContext == null)
            return;

        if (_fetchContext.InstalledMods != null && _fetchContext.InstalledMods.Count > 0)
            _fetchContext.CurrentMod = _fetchContext.InstalledMods[0];

        if (!_fetchContext.DoesConfigExist)
        {
            this.IsVisible = false;

            var _temporaryOwner = new Window
            {
                ShowInTaskbar = false,
                Background = Brushes.Transparent,
                WindowDecorations = WindowDecorations.None,
                TransparencyLevelHint = new List<WindowTransparencyLevel>() { WindowTransparencyLevel.Transparent }
            };

            _temporaryOwner.Show();
            _fetchContext.DoesConfigExist = true;

            var _fetchWizard = new SetupWizardView();
            await _fetchWizard.ShowDialog(_temporaryOwner);

            _temporaryOwner.Close();
            this.IsVisible = true;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Fetch the main ViewModel and serialize the config it has.
        var _fetchViewModel = DataContext as MainViewModel;
        var _fetchSerialize = YamlSerializer.Serialize<Config>(_fetchViewModel.CurrentConfig);

        // Overwrite the config file.
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "config.yml"), _fetchSerialize);

        // We gotta still CLOSE the thing, no?
        base.OnClosing(e);
    }

    // This executes if the combobox that holds the games changes its value.
    private void OnTargetGameChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Fetch the context.
        var _fetchContext = DataContext as MainViewModel;

        // Re-Initialize the view.
        _fetchContext.InitializeView();
    }

    // This executes if the utmost button is clicked.
    private void OnUtmostClicked(object? sender, RoutedEventArgs e)
    {
        // Fetch the context and the mod list.
        var _fetchContext = DataContext as MainViewModel;
        var _fetchModList = _fetchContext.InstalledMods;

        // Make the move, select the mod.
        _fetchModList.Move(ModList.MainList.SelectedIndex, 0);
        _fetchContext.CurrentMod = _fetchModList[0];
    }

    // This executes if the move up button is clicked.
    private void OnUpPriorityClicked(object? sender, RoutedEventArgs e)
    {
        // Fetch the context, the mod list, and the current index.
        var _fetchContext = DataContext as MainViewModel;
        var _fetchModList = _fetchContext.InstalledMods;
        var _fetchCurrentIndex = ModList.MainList.SelectedIndex;

        // If the mod is already super-hot-fire number one, we don't need to do nothin'.
        if (_fetchCurrentIndex == 0)
            return;

        // Make the move, select the mod.
        _fetchModList.Move(_fetchCurrentIndex, _fetchCurrentIndex - 1);
        _fetchContext.CurrentMod = _fetchModList[_fetchCurrentIndex - 1];
    }

    // This executes if the move up button is clicked.
    // ...why am I even commenting this, ain't it obvious?!
    private void OnDownPriorityClicked(object? sender, RoutedEventArgs e)
    {
        // Same shit as OnUpPriorityClicked, but reversed.

        var _fetchContext = DataContext as MainViewModel;
        var _fetchModList = _fetchContext.InstalledMods;
        var _fetchCurrentIndex = ModList.MainList.SelectedIndex;

        if (_fetchCurrentIndex == ModList.MainList.ItemCount - 1)
            return;

        if (!_fetchModList[_fetchCurrentIndex + 1].ModValid)
            return;

        _fetchModList.Move(_fetchCurrentIndex, _fetchCurrentIndex + 1);
        _fetchContext.CurrentMod = _fetchModList[_fetchCurrentIndex + 1];
    }

    // This executes when we click the remove mod button.
    private async void OnRemoveClicked(object? sender, RoutedEventArgs e)
    {
        // Ask the user if they *really* want to remove said mod.

        var _removeDialog = new RemoveModDialog();
        bool? _fetchResult = await _removeDialog.ShowDialog<bool?>(this);

        // If they indeed do:

        if (_fetchResult == true)
        {
            // Fetch the context, the mod list, and the current mod.
            var _fetchContext = DataContext as MainViewModel;
            var _fetchModList = _fetchContext.InstalledMods;
            var _fetchCurrentMod = _fetchContext.CurrentMod;

            // Fetch the path too we kinda need it.
            var _fetchModPath = _fetchCurrentMod.ModPath;

            // Remove the mod from the list.
            _fetchModList.Remove(_fetchCurrentMod);

            // If we still have mods, select one.
            if (_fetchModList.Count > 0)
                _fetchContext.CurrentMod = _fetchModList.First();

            // If we do not, nullify selection.
            else
            {
                _fetchContext.HasModsInstalled = false;
                _fetchContext.CurrentMod = null;
            }

            // Remove the mod directory.
            // This is a task for a reason. This isn't being awaited for a reason. Do not listen to IntelliSense on this one.
            Task.Run(() =>
            {
                if (Directory.Exists(_fetchModPath))
                    Directory.Delete(_fetchModPath, true);
            });
        }
    }

    private async void OnInstallClicked(object? sender, RoutedEventArgs e)
    {
        var _installDialog = new InstallModDialog();
        string? _fetchResult = await _installDialog.ShowDialog<string?>(this);

        var _fetchContext = DataContext as MainViewModel;
        var _fetchModsList = _fetchContext.InstalledMods;
        var _fetchConfig = _fetchContext.CurrentConfig;

        var _fetchModPath = PathService.ResolveMod(_fetchConfig);

        var _fetchInstallResult = 0x00;

        if (_fetchResult != null && !string.IsNullOrEmpty(_fetchResult))
        {
            var _progressDialog = new ModProgressDialog();
            _progressDialog.ShowDialog(this);

            var _gitProcessHandler = new TransferProgressHandler((progress) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _progressDialog.InstallProgress.Maximum = progress.TotalObjects;
                    _progressDialog.InstallProgress.Value = progress.ReceivedObjects;
                });

                if (ModService.CancelToken.IsCancellationRequested)
                    return false;

                return true;
            });

            bool _localProcessHandler(int processed, int total)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _progressDialog.InstallProgress.Maximum = total;
                    _progressDialog.InstallProgress.Value = processed;
                });

                if (ModService.CancelToken.IsCancellationRequested)
                    return false;

                return true;
            }
            ;

            if (File.Exists(_fetchResult))
                _fetchInstallResult = await ModService.InstallLocal(_fetchModPath, _fetchResult, _localProcessHandler);

            else
                _fetchInstallResult = await ModService.InstallGit(_fetchModPath, _fetchResult, _gitProcessHandler);

            _progressDialog.Close(true);

            if (_fetchInstallResult == 0x01)
            {
                var _errorDialog = new InvalidYamlError();
                await _errorDialog.ShowDialog(this);
            }

            else if (_fetchInstallResult == 0x00)
            {
                var _fetchLatestMod = new DirectoryInfo(_fetchModPath).GetDirectories()
                                                                      .OrderByDescending(d => d.LastWriteTimeUtc)
                                                                      .First();

                var _fetchPathGit = Path.Combine(_fetchLatestMod.FullName, ".git");
                var _fetchYamlName = Path.Combine(_fetchLatestMod.FullName, "mod.yml");
                var _fetchPathIcon = Path.Combine(_fetchLatestMod.FullName, "icon.png");

                var _fetchMetadata = Metadata.Read(_fetchYamlName);

                if (_fetchMetadata.IsValid)
                {
                    var _modModel = new ModModel
                    {
                        ModTitle = _fetchMetadata.Title,
                        ModAuthor = _fetchMetadata.OriginalAuthor,
                        ModDescription = _fetchMetadata.Description,
                        ModPath = _fetchLatestMod.FullName,
                        ModFilesList = _fetchMetadata.Assets.Select(x => x.Name).ToArray(),
                        ModIcon = File.Exists(_fetchPathIcon) ? new Bitmap(_fetchPathIcon) : null,
                        ModActive = true,
                        ModValid = true
                    };

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

                    var _fetchFirstInvalid = _fetchModsList.FirstOrDefault(x => !x.ModValid);
                    var _fetchModIndex = _fetchFirstInvalid != null ? _fetchModsList.IndexOf(_fetchFirstInvalid) : _fetchModsList.Count;

                    _fetchModsList.Insert(_fetchModIndex, _modModel);
                    _fetchContext.HasModsInstalled = true;
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
                        ModPath = _fetchLatestMod.FullName,
                        ModActive = false,
                        ModValid = false
                    };

                    _fetchModsList.Add(_modModel);
                }
            }
        }
    }

    private async void OnOpenFolderClicked(object? sender, RoutedEventArgs e)
    {
        var _fetchContext = DataContext as MainViewModel;
        var _fetchCurrentMod = _fetchContext.CurrentMod;

        var _fetchTopLevel = GetTopLevel(this);
        var _fetchDirectoryInfo = new DirectoryInfo(_fetchCurrentMod.ModPath);

        if (_fetchTopLevel?.Launcher != null)
            await _fetchTopLevel.Launcher.LaunchDirectoryInfoAsync(_fetchDirectoryInfo);
    }

    private void OnModActiveChanged(object? sender, ModListView.ModActiveChangedEventArgs e)
    {
        var _fetchContext = DataContext as MainViewModel;
        var _fetchModList = _fetchContext.InstalledMods;
        var _fetchConfig = _fetchContext.CurrentConfig;

        var _fetchMemoryPath = Path.Combine(PathService.ResolveMod(_fetchConfig), "mod_memory.yml");

        var _fetchMemoryRAW = File.ReadAllText(_fetchMemoryPath);
        var _fetchMemory = YamlSerializer.Deserialize<ObservableCollection<MemoryModel>>(_fetchMemoryRAW);

        var _fetchSenderHash = ModService.ResolveMD5(e.TargetMod, _fetchConfig);
        var _fetchMemoryItem = _fetchMemory.FirstOrDefault(x => x.ModHash == _fetchSenderHash);

        if (_fetchMemoryItem == null)
        {
            _fetchMemoryItem = new MemoryModel
            {
                ModHash = _fetchSenderHash,
                ModActive = e.IsChecked,
                ModIndex = _fetchModList.IndexOf(e.TargetMod)
            };

            _fetchMemory.Add(_fetchMemoryItem);
        }

        else
        {
            var _fetchMemoryIndex = _fetchMemory.IndexOf(_fetchMemoryItem);
            _fetchMemory[_fetchMemoryIndex].ModActive = e.IsChecked;
        }

        var _fetchSerial = YamlSerializer.Serialize(_fetchMemory);
        File.WriteAllText(_fetchMemoryPath, _fetchSerial);
    }

    private async void OnRunRequested(object? sender, RoutedEventArgs e)
    {
        var _fetchContext = DataContext as MainViewModel;
        var _fetchConfig = _fetchContext.CurrentConfig;

        await ModService.Run(_fetchConfig, GetTopLevel(this));
    }


    private async void OnBuildRequested(object? sender, RoutedEventArgs e)
    {
        var _progressDialog = new BuildProgressDialog();
        _progressDialog.ShowDialog(this);

        int _assetProcessed = 0;
        int _assetTotal = 0;
        string _currentModName = "N/A";

        var _fetchContext = DataContext as MainViewModel;

        var _fetchConfig = _fetchContext.CurrentConfig;
        var _fetchFrontConfig = _fetchConfig.Frontend;

        var _buildResult =
            await ModService.Build
            (
                _fetchContext.InstalledMods,
                _fetchConfig,

                (string currModName, int procMod, int totalMod) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        _currentModName = currModName;

                        _progressDialog.ModProgress.Maximum = totalMod;
                        _progressDialog.ModProgress.Value = procMod;
                        _progressDialog.ModProgress.ProgressTextFormat = $"Currently Building: {currModName}";

                        _progressDialog.AssetProgress.Maximum = _assetTotal;
                        _progressDialog.AssetProgress.Value = _assetProcessed;
                    });

                    if (ModService.CancelToken.IsCancellationRequested)
                        return false;

                    return true;
                },

                (int processed, int total) =>
                {
                    _assetProcessed = processed;
                    _assetTotal = total;

                    if (ModService.CancelToken.IsCancellationRequested)
                        return false;

                    return true;
                });

        _progressDialog.Close(true);

        if (_buildResult == 1)
        {
            var _errorDialog = new BuildModError();
            _errorDialog.MainText.Text = string.Format(_errorDialog.MainText.Text, _currentModName);
            await _errorDialog.ShowDialog(this);
        }
    }

    private async void OnBuildRunRequested(object? sender, RoutedEventArgs e)
    {
        var _progressDialog = new BuildProgressDialog();
        _progressDialog.ShowDialog(this);

        int _assetProcessed = 0;
        int _assetTotal = 0;
        string _currentModName = "N/A";

        var _fetchContext = DataContext as MainViewModel;

        var _fetchConfig = _fetchContext.CurrentConfig;
        var _fetchFrontConfig = _fetchConfig.Frontend;

        var _buildResult =
            await ModService.Build
            (
                _fetchContext.InstalledMods,
                _fetchConfig,

                (string currModName, int procMod, int totalMod) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        _currentModName = currModName;

                        _progressDialog.ModProgress.Maximum = totalMod;
                        _progressDialog.ModProgress.Value = procMod;
                        _progressDialog.ModProgress.ProgressTextFormat = $"Currently Building: {currModName}";

                        _progressDialog.AssetProgress.Maximum = _assetTotal;
                        _progressDialog.AssetProgress.Value = _assetProcessed;
                    });

                    if (ModService.CancelToken.IsCancellationRequested)
                        return false;

                    return true;
                },

                (int processed, int total) =>
                {
                    _assetProcessed = processed;
                    _assetTotal = total;

                    if (ModService.CancelToken.IsCancellationRequested)
                        return false;

                    return true;
                });

        _progressDialog.Close(true);

        switch (_buildResult)
        {
            case 0:
                await ModService.Run(_fetchConfig, GetTopLevel(this));
                break;

            case 1:
            {
                var _errorDialog = new BuildModError();
                _errorDialog.MainText.Text = string.Format(_errorDialog.MainText.Text, _currentModName);
                await _errorDialog.ShowDialog(this);
            }
            break;
        }
    }

    private async void OnSetupRequested(object? sender, RoutedEventArgs e) => await new SetupWizardView().ShowDialog(this);
}
