using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using OpenKh.Tools.ModManager.Services;
using OpenKh.Tools.ModManager.ViewModels;
using OpenKh.Tools.ModManager.Views;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenKh.Tools.ModManager.Classes;

namespace OpenKh.Tools.ModManager.Wizard
{
    public partial class WizardExtractSetup : ContentPage
    {
        public WizardExtractSetup()
        {
            InitializeComponent();

            WeakReferenceMessenger.Default.Register<PageRequestMessage>(this, (registrar, message) =>
            {
                if (message.Self != this)
                    return;

                message.Reply(true);
            });
        }

        private async Task<bool> ExtractTask(List<bool> extractList, Config currentConfig)
        {
            var _fetchResult = await ModService.Extract
                                (
                                    extractList,
                                    currentConfig,
                                    currentConfig.Frontend.TargetPlatform != Platform.PCSX2,
                                    (int processed, int total) =>
                                    {
                                        Dispatcher.UIThread.Post(() =>
                                        {
                                            ProgressExtract.Maximum = total;
                                            ProgressExtract.Value = processed;
                                        });

                                        if (ModService.CancelToken.IsCancellationRequested)
                                            return false;

                                        return true;
                                    }
                                );

            ExtractStartButton.IsVisible = true;
            ExtractStopButton.IsVisible = false;

            WeakReferenceMessenger.Default.Send(new UnblockNextRequest());

            if (_fetchResult != 0x00)
            {
                currentConfig.Frontend.DataPath = null;

                ProgressExtract.Value = 0;
                ProgressExtract.Maximum = 100;

                return false;
            }

            else
                return true;
        }

        private async void OnExtractStart(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var _fetchContext = DataContext as MainViewModel;

            ExtractStartButton.IsVisible = false;
            ExtractStopButton.IsVisible = true;

            var _fetchExtractList = new List<bool>()
            {
                ExtractKH1.IsChecked.Value,
                ExtractKH2.IsChecked.Value,
                ExtractCOM.IsChecked.Value,
                ExtractBBS.IsChecked.Value,
                ExtractDDD.IsChecked.Value,
            };

            WeakReferenceMessenger.Default.Send(new BlockNextRequest());
            ExtractTask(_fetchExtractList, _fetchContext.CurrentConfig);
        }

        private async void OnExtractStop(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ModService.CancelTokenSource.Cancel();
    }
}
