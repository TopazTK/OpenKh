using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenKh.Tools.ModManager.Models;
using OpenKh.Tools.ModManager.Services;
using OpenKh.Tools.ModManager.ViewModels;
using SharpYaml;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace OpenKh.Tools.ModManager.Views
{
    public partial class CatalogView : Window
    {
        private MainViewModel _referenceModel { get; set; }

        public CatalogView()
        {
            InitializeComponent();
            Loaded += OnViewLoaded;
        }

        private async void OnViewLoaded(object? sender, RoutedEventArgs e)
        {
            if (!Design.IsDesignMode)
            {
                var _fetchContext = new CatalogViewModel();
                var _fetchApplication = Application.Current;

                // Uhh how the fuck?
                if (_fetchApplication != null)
                {
                    var _fetchLifetime = _fetchApplication.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                    var _fetchMainView = _fetchLifetime != null ? _fetchLifetime.MainWindow : null;

                    // Uhh how the fuck, electric bogaloo?
                    if (_fetchMainView != null)
                    {
                        var _fetchReference = _fetchMainView.DataContext;

                        if (_fetchReference != null)
                        {
                            _referenceModel = (MainViewModel)_fetchReference;

                            DataContext = _fetchContext;
                            _fetchContext.CurrentConfig = _referenceModel.CurrentConfig;

                            await _fetchContext.InitializeView();

                            MainProgress.IsVisible = false;
                        }
                    }
                }
            }
        }

        private void OnModActiveChanged(object? sender, ModWrapView.ModActiveChangedEventArgs e)
        {
            var _fetchContext = DataContext as CatalogViewModel;
            var _fetchConfig = _fetchContext != null ? _fetchContext.CurrentConfig : null;
            var _fetchSelectList = _fetchContext != null ? _fetchContext.SelectedMods : null;

            if (_fetchConfig != null && _fetchSelectList != null)
            {
                var _fetchSenderURL = e.TargetMod.ModPath;
                var _fetchFromList = _fetchSelectList.FirstOrDefault(x => x == _fetchSenderURL);

                if (_fetchFromList != null && !e.IsChecked)
                    _fetchSelectList.Remove(_fetchSenderURL);

                else if (_fetchFromList == null && e.IsChecked)
                    _fetchSelectList.Add(_fetchSenderURL);

                InstallButton.Content = $"Install {_fetchSelectList.Count} Mods...";
                InstallButton.IsEnabled = _fetchSelectList.Count > 0;
            }
        }

        private async void OnInstallClicked(object? sender, RoutedEventArgs e)
        {
            var _fetchContext = DataContext as CatalogViewModel;
            var _fetchSelectList = _fetchContext != null ? _fetchContext.SelectedMods : null;

            if (_fetchSelectList != null)
            {
                var _fetchReturn = _fetchSelectList.Count > 0 ? String.Join(';', _fetchSelectList) : null;
                Close(_fetchReturn);
            }
        }
    }
}
