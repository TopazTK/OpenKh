using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using OpenKh.Tools.ModManager.Classes;
using OpenKh.Tools.ModManager.ViewModels;
using OpenKh.Tools.ModManager.Wizard;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using static OpenKh.Tools.ModManager.Wizard.WizardPanaceaSetup;

namespace OpenKh.Tools.ModManager.Views
{
    public record PageRequestMessage(object self, Config currentConfig);

    public partial class SetupWizardView : Window
    {
        private WizardViewModel _rootModel { get; set; }

        public SetupWizardView()
        {
            _rootModel = new WizardViewModel();

            _rootModel.CurrentWizardPage = new WizardGameSetup();
            _rootModel.PastWizardPages = new ObservableCollection<object?>();

            _rootModel.FutureWizardPages = new ObservableCollection<object?>() 
            { 
                new WizardPanaceaSetup(),
                new WizardScriptSetup(),
                new WizardDirectLaunch(),
                new WizardExtractSetup(),
            };

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

            InitializeComponent();
        }

        private void OnNextPage(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var _fetchContext = DataContext as MainViewModel;

            var _fetchBackList = _rootModel.PastWizardPages;
            var _fetchNextList = _rootModel.FutureWizardPages;

            var _fetchNextPage = _fetchNextList.First();
            _fetchNextList.Remove(_fetchNextPage);

            _fetchBackList.Add(_rootModel.CurrentWizardPage);
            _rootModel.CurrentWizardPage = _fetchNextPage;

            if (_fetchNextList.Count == 0)
            {
                NextButton.IsEnabled = false;

                FinishButton.IsVisible = true;
                CancelButton.IsVisible = false;
            }

            WeakReferenceMessenger.Default.Send(new PageRequestMessage(_rootModel.CurrentWizardPage, _fetchContext.CurrentConfig));

            BackButton.IsEnabled = true;
        }

        private void OnBackPage(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var _fetchContext = DataContext as MainViewModel;

            var _fetchBackList = _rootModel.PastWizardPages;
            var _fetchNextList = _rootModel.FutureWizardPages;

            var _fetchBackPage = _fetchBackList.Last();
            _fetchBackList.Remove(_fetchBackPage);

            _fetchNextList.Insert(0, _rootModel.CurrentWizardPage);
            _rootModel.CurrentWizardPage = _fetchBackPage;

            if (_fetchBackList.Count == 0)
                BackButton.IsEnabled = false;

            FinishButton.IsVisible = false;
            CancelButton.IsVisible = true;

            WeakReferenceMessenger.Default.Send(new PageRequestMessage(_rootModel.CurrentWizardPage, _fetchContext.CurrentConfig));

            NextButton.IsEnabled = true;
        }
    }
}
