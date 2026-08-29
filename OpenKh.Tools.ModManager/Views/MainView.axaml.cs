using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenKh.Tools.ModManager.Models;
using OpenKh.Tools.ModManager.Services;
using OpenKh.Tools.ModManager.ViewModels;
using SharpYaml;
using System;
using System.IO;

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
        var _fetchSerialize = YamlSerializer.Serialize<Configuration>(_fetchViewModel.CurrentConfig);

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
}
