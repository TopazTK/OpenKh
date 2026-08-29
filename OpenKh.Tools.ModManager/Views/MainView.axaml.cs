using Avalonia.Controls;
using Avalonia.Interactivity;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using OpenKh.Tools.ModManager.Dialogs;
using OpenKh.Tools.ModManager.Models;
using OpenKh.Tools.ModManager.Services;
using OpenKh.Tools.ModManager.ViewModels;
using SharpYaml;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace OpenKh.Tools.ModManager.Views;

public partial class MainView : Window
{
    public MainView()
    {
        InitializeComponent();
        Loaded += OnViewLoaded;
    }

    private void OnViewLoaded(object? sender, RoutedEventArgs e)
    {
        // Yea so Avalonia lists do not automatically highlight shit if they ain't empty so we gotta do it here.
        // If this ever breaks that means something is wrong.

        var _fetchContext = DataContext as MainViewModel;

        if (_fetchContext.InstalledMods != null && _fetchContext.InstalledMods.Count > 0)
            _fetchContext.CurrentMod = _fetchContext.InstalledMods[0];
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Fetch the main ViewModel and serialize the config it has.
        var _fetchViewModel = DataContext as MainViewModel;
        var _fetchSerialize = YamlSerializer.Serialize<Config>(_fetchViewModel.CurrentConfig);

        // Overwrite the config file.
        File.WriteAllText("config.yml", _fetchSerialize);

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

        // If there is at least one mod, select the first mod.
        if (_fetchContext.InstalledMods.Count > 0)
            _fetchContext.CurrentMod = _fetchContext.InstalledMods[0];
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

        _fetchModList.Move(_fetchCurrentIndex, _fetchCurrentIndex + 1);
        _fetchContext.CurrentMod = _fetchModList[_fetchCurrentIndex + 1];
    }

    private async void OnRemoveClicked(object? sender, RoutedEventArgs e)
    {
        var _removeDialog = new RemoveModDialog();
        bool? _fetchResult = await _removeDialog.ShowDialog<bool?>(this);

        if (_fetchResult == true)
        {
            var _fetchContext = DataContext as MainViewModel;
            var _fetchModList = _fetchContext.InstalledMods;
            var _fetchCurrentMod = _fetchContext.CurrentMod;

            var _fetchModPath = _fetchCurrentMod.ModPath;

            _fetchModList.Remove(_fetchCurrentMod);

            if (_fetchModList.Count > 0)
                _fetchCurrentMod = _fetchModList[0];

            else
                _fetchCurrentMod = null;

            if (Directory.Exists(_fetchModPath))
                Directory.Delete(_fetchModPath, true);
        }
    }

    private async void OnInstallClicked(object? sender, RoutedEventArgs e)
    {
        var _installDialog = new InstallModDialog();
        string? _fetchResult = await _installDialog.ShowDialog<string?>(this);

        var _fetchContext = DataContext as MainViewModel;
        var _fetchModsPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "mods", _fetchContext.CurrentConfig.TargetGame.ToString().ToLower());

        if (_fetchResult != null && !string.IsNullOrEmpty(_fetchResult))
        {
            if (File.Exists(_fetchResult))
            {

            }

            else
            {
                var _fetchPlatform = _fetchResult.Contains('@') ? _fetchResult.Split('@').Last() : null;
                _fetchResult = _fetchPlatform != null ? _fetchResult.Replace("@" + _fetchPlatform, "") : _fetchResult;

                var _fetchBranch = _fetchResult.Contains(':') ? _fetchResult.Split(':').Last() : null;
                _fetchResult = _fetchBranch != null ? _fetchResult.Replace(":" + _fetchBranch, "") : _fetchResult;

                var _fetchAuthor = _fetchResult.Split('/').First();
                var _fetchName = _fetchResult.Split('/').Last();

                var _fetchCurrentModDir = Path.Combine(_fetchModsPath, _fetchName);
                var _fetchCurrentGitPath = Path.Combine(_fetchCurrentModDir, ".git");

                var _fetchBaseUri = new Uri("https://" + (_fetchPlatform != null ? _fetchPlatform : "github.com"));
                var _fetchRelativeUri = new Uri(_fetchBaseUri, $"{_fetchResult}");

                var _cloneOptions = new CloneOptions
                {
                    Checkout = true,
                    BranchName = _fetchBranch,
                };

                _cloneOptions.FetchOptions.Depth = 1;
                _cloneOptions.FetchOptions.Prune = true;

                if (!Directory.Exists(_fetchCurrentModDir))
                    Directory.CreateDirectory(_fetchCurrentModDir);

                var _fetchRemotes = Repository.ListRemoteReferences(_fetchRelativeUri.ToString());

                if (_fetchPlatform == null)
                {
                    using var _makeClient = new HttpClient();
                    using var _fetchResponse = await _makeClient.GetAsync($"https://raw.githubusercontent.com/{_fetchResult}/" + (_fetchBranch != null ? _fetchBranch : _fetchRemotes.First().TargetIdentifier) + "/mod.yml", HttpCompletionOption.ResponseHeadersRead);

                    if (_fetchResponse.StatusCode != HttpStatusCode.OK)
                        return; // TODO: Error box -- Mod Invalid.

                    Repository.Clone(_fetchRelativeUri.ToString(), _fetchCurrentModDir, _cloneOptions);

                    var _fetchGitDir = new DirectoryInfo(_fetchCurrentGitPath);

                    foreach (var _fetchFile in _fetchGitDir.GetFiles("*", SearchOption.AllDirectories))
                        _fetchFile.Attributes &= ~FileAttributes.ReadOnly;
                }

                else
                {
                    Repository.Clone(_fetchRelativeUri.ToString(), _fetchCurrentModDir, _cloneOptions);

                    var _fetchGit = new Repository(_fetchCurrentModDir);

                    var _fetchCommit = _fetchGit.Head.Tip;
                    var _doesModFileExist = _fetchCommit["mod.yml"] != null;

                    _fetchGit.Dispose();

                    var _fetchGitDir = new DirectoryInfo(_fetchCurrentGitPath);

                    foreach (var _fetchFile in _fetchGitDir.GetFiles("*", SearchOption.AllDirectories))
                        _fetchFile.Attributes &= ~FileAttributes.ReadOnly;

                    if (!_doesModFileExist)
                    {
                        Directory.Delete(_fetchCurrentModDir, true);
                        return; // TODO: Error box -- Mod Invalid.
                    }
                }
            }
        
            _fetchContext.InitializeView();

            // If there is at least one mod, select the first mod.
            if (_fetchContext.InstalledMods.Count > 0)
                _fetchContext.CurrentMod = _fetchContext.InstalledMods[0];
        }
    }
}
