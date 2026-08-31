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

        #endregion

        public ModStatusView()
        {
            InitializeComponent();
        }

        private void OnProcessClicked(object? sender, RoutedEventArgs e) => OnRunRequested();
    }
}
