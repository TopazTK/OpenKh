using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenKh.Tools.ModManager.Models;
using System;
using System.Globalization;

namespace OpenKh.Tools.ModManager.Views
{
    public partial class ModStatusView : ContentPage
    {
        #region Custom Events

        public static readonly RoutedEvent<RoutedEventArgs> SetupRequestedEvent = RoutedEvent.Register<ModDetailsView, RoutedEventArgs>(nameof(SetupRequested), RoutingStrategies.Direct);

        public event EventHandler<RoutedEventArgs> SetupRequested
        {
            add => AddHandler(SetupRequestedEvent, value);
            remove => RemoveHandler(SetupRequestedEvent, value);
        }

        protected virtual void OnSetupRequested()
        {
            RoutedEventArgs args = new RoutedEventArgs(SetupRequestedEvent);
            RaiseEvent(args);
        }

        public static readonly RoutedEvent<RoutedEventArgs> RunRequestedEvent = RoutedEvent.Register<ModDetailsView, RoutedEventArgs>(nameof(RunRequested), RoutingStrategies.Direct);

        public event EventHandler<RoutedEventArgs> RunRequested
        {
            add => AddHandler(RunRequestedEvent, value);
            remove => RemoveHandler(RunRequestedEvent, value);
        }

        protected virtual void OnRunRequested()
        {
            RoutedEventArgs args = new RoutedEventArgs(RunRequestedEvent);
            RaiseEvent(args);
        }

        public static readonly RoutedEvent<RoutedEventArgs> BuildRunRequestedEvent = RoutedEvent.Register<ModDetailsView, RoutedEventArgs>(nameof(BuildRunRequested), RoutingStrategies.Direct);

        public event EventHandler<RoutedEventArgs> BuildRunRequested
        {
            add => AddHandler(BuildRunRequestedEvent, value);
            remove => RemoveHandler(BuildRunRequestedEvent, value);
        }

        protected virtual void OnBuildRunRequested()
        {
            RoutedEventArgs args = new RoutedEventArgs(BuildRunRequestedEvent);
            RaiseEvent(args);
        }

        #endregion

        public ModStatusView()
        {
            InitializeComponent();
        }

        private void OnProcessBuildRun(object? sender, RoutedEventArgs e) => OnBuildRunRequested();
        private void OnProcessRun(object? sender, RoutedEventArgs e) => OnRunRequested();

        private void OnSetupRequested(object? sender, RoutedEventArgs e) => OnSetupRequested();
    }
}
