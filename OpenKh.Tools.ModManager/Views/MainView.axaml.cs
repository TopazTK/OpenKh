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
        // Yea so Avalonian lists do not automatically highlight shit if they ain't empty so we gotta do it here.
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

        base.OnClosing(e);
    }

    private void OnTargetGameChanged(object? sender, SelectionChangedEventArgs e)
    {
        var _fetchContext = DataContext as MainViewModel;

        _fetchContext.InitializeView();

        if (_fetchContext.InstalledMods.Count > 0)
            _fetchContext.CurrentMod = _fetchContext.InstalledMods[0];
    }

    private void OnUtmostClicked(object? sender, RoutedEventArgs e)
    {
        var _fetchContext = DataContext as MainViewModel;
        var _fetchModList = _fetchContext.InstalledMods;

        _fetchModList.Move(ModList.MainList.SelectedIndex, 0);
        _fetchContext.CurrentMod = _fetchModList[0];
    }

    private void OnUpPriorityClicked(object? sender, RoutedEventArgs e)
    {
        var _fetchContext = DataContext as MainViewModel;
        var _fetchModList = _fetchContext.InstalledMods;
        var _fetchCurrentIndex = ModList.MainList.SelectedIndex;

        if (_fetchCurrentIndex == 0)
            return;

        _fetchModList.Move(_fetchCurrentIndex, _fetchCurrentIndex - 1);
        _fetchContext.CurrentMod = _fetchModList[_fetchCurrentIndex - 1];
    }

    private void OnDownPriorityClicked(object? sender, RoutedEventArgs e)
    {
        var _fetchContext = DataContext as MainViewModel;
        var _fetchModList = _fetchContext.InstalledMods;
        var _fetchCurrentIndex = ModList.MainList.SelectedIndex;

        if (_fetchCurrentIndex == ModList.MainList.ItemCount - 1)
            return;

        _fetchModList.Move(_fetchCurrentIndex, _fetchCurrentIndex + 1);
        _fetchContext.CurrentMod = _fetchModList[_fetchCurrentIndex + 1];
    }
}
