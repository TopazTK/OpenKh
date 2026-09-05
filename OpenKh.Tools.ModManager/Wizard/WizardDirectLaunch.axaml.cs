using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using OpenKh.Tools.ModManager.Classes;
using OpenKh.Tools.ModManager.Services;
using OpenKh.Tools.ModManager.ViewModels;
using OpenKh.Tools.ModManager.Views;
using System;
using System.IO;

namespace OpenKh.Tools.ModManager.Wizard
{
    public partial class WizardDirectLaunch : ContentPage
    {
        public WizardDirectLaunch()
        {
            InitializeComponent();

            WeakReferenceMessenger.Default.Register<PageRequestMessage>(this, (registrar, message) =>
            {
                if (message.Self != this)
                    return;

                var _fetchFrontend = message.CurrentConfig.Frontend;

                if (_fetchFrontend.TargetPlatform != Platform.STEAM)
                    message.Reply(false);

                else
                {
                    InstallPanel.IsVisible = false;
                    NotInstallPanel.IsVisible = true;

                    var _fetchConfig = message.CurrentConfig;

                    var _fetchPath1525 = PathService.ResolvePath1525(_fetchConfig);
                    var _fetchPath28 = PathService.ResolvePath28(_fetchConfig);

                    var _fetchSteamID1525 = Path.Combine(_fetchPath1525, "steam_appid.txt");
                    var _fetchSteamID28 = Path.Combine(_fetchPath28, "steam_appid.txt");

                    var _isValid1525 = String.IsNullOrEmpty(_fetchPath1525) || File.Exists(_fetchSteamID1525);
                    var _isValid28 = String.IsNullOrEmpty(_fetchPath28) || File.Exists(_fetchSteamID28);

                    if (_isValid1525 || _isValid28)
                    {
                        InstallPanel.IsVisible = true;
                        NotInstallPanel.IsVisible = false;
                    }

                    message.Reply(true);
                }
            });
        }

        private void OnInstallClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var _fetchContext = DataContext as MainViewModel;
            var _fetchConfig = _fetchContext.CurrentConfig;

            var _fetchPath1525 = PathService.ResolvePath1525(_fetchConfig);
            var _fetchPath28 = PathService.ResolvePath28(_fetchConfig);

            var _fetchSteamID1525 = Path.Combine(_fetchPath1525, "steam_appid.txt");
            var _fetchSteamID28 = Path.Combine(_fetchPath28, "steam_appid.txt");

            if (!String.IsNullOrEmpty(_fetchPath1525))
                File.WriteAllText(_fetchSteamID1525, "2552430");

            if (!String.IsNullOrEmpty(_fetchPath28))
                File.WriteAllText(_fetchSteamID28, "2552440");

            InstallPanel.IsVisible = true;
            NotInstallPanel.IsVisible = false;
        }

        private void OnUninstallClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var _fetchContext = DataContext as MainViewModel;
            var _fetchConfig = _fetchContext.CurrentConfig;

            var _fetchPath1525 = PathService.ResolvePath1525(_fetchConfig);
            var _fetchPath28 = PathService.ResolvePath28(_fetchConfig);

            var _fetchSteamID1525 = Path.Combine(_fetchPath1525, "steam_appid.txt");
            var _fetchSteamID28 = Path.Combine(_fetchPath28, "steam_appid.txt");

            var _isValid1525 = !String.IsNullOrEmpty(_fetchPath1525) && File.Exists(_fetchSteamID1525);
            var _isValid28 = !String.IsNullOrEmpty(_fetchPath28) && File.Exists(_fetchSteamID28);

            if (_isValid1525)
                File.Delete(_fetchSteamID1525);

            if (_isValid28)
                File.Delete(_fetchSteamID28);

            InstallPanel.IsVisible = false;
            NotInstallPanel.IsVisible = true;
        }
    }
}
