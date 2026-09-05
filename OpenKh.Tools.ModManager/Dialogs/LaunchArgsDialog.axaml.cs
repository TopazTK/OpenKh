using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenKh.Tools.ModManager.ViewModels;
using System;

namespace OpenKh.Tools.ModManager.Dialogs
{
    public partial class LaunchArgsDialog : Window
    {
        public LaunchArgsDialog()
        {
            InitializeComponent();

            var _fetchApplication = Application.Current;

            // Uhh how the fuck?
            if (_fetchApplication == null)
                throw new NullReferenceException("Application is null, this should not be possible.");

            var _fetchLifetime = _fetchApplication.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var _fetchMainView = _fetchLifetime.MainWindow;

            // Uhh how the fuck, electric bogaloo?
            if (_fetchMainView == null)
                throw new NullReferenceException("MainView is null, this ALSO should not be possible.");

            var _fetchContext = _fetchMainView.DataContext as MainViewModel;
            DataContext = _fetchContext;

            Loaded += OnViewLoaded;
        }

        private async void OnViewLoaded(object? sender, RoutedEventArgs e)
        {
            Arguments.Focus();
        }

        private void OnCommitClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(Arguments.Text);
    }
}
