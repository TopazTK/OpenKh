using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using OpenKh.Tools.ModManager.Dialogs;
using OpenKh.Tools.ModManager.Models;
using OpenKh.Tools.ModManager.Services;
using OpenKh.Tools.ModManager.ViewModels;
using SharpYaml;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using static OpenKh.Kh2.Ard.AreaDataScript;

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

        if (_fetchContext == null)
            return;

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
                _fetchCurrentMod = _fetchModList.First();

            // If we do not, nullify selection.
            else
                _fetchCurrentMod = null;

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
        var _fetchModsPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "mods", _fetchContext.CurrentConfig.TargetGame.ToString().ToLower());

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

                if (InstallService.CancelToken.IsCancellationRequested)
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

                if (InstallService.CancelToken.IsCancellationRequested)
                    return false;

                return true;
            };

            if (File.Exists(_fetchResult))
                _fetchInstallResult = await InstallService.InstallLocal(_fetchModsPath, _fetchResult, _localProcessHandler);

            else
                _fetchInstallResult = await InstallService.InstallGit(_fetchModsPath, _fetchResult, _gitProcessHandler);

            _progressDialog.Close(true);

            if (_fetchInstallResult == 0x01)
            {
                var _errorDialog = new InvalidYamlError();
                await _errorDialog.ShowDialog(this);
            }

            _fetchContext.InitializeView();

            // If there is at least one mod, select the first mod.
            if (_fetchContext.InstalledMods.Count > 0)
                _fetchContext.CurrentMod = _fetchContext.InstalledMods[0];
        }
    }
}
