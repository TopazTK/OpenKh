using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenKh.Tools.ModManager.Models;
using OpenKh.Tools.ModManager.ViewModels;
using System;

namespace OpenKh.Tools.ModManager.Views
{
    public partial class ModListView : ContentPage
    {
        #region Custom Events
            public class ModActiveChangedEventArgs : RoutedEventArgs
            {
                public ModModel TargetMod { get; }
                public bool IsChecked { get; }

                public ModActiveChangedEventArgs(RoutedEvent routedEvent, ModModel targetMod, bool? isChecked) : base(routedEvent)
                {
                    TargetMod = targetMod;
                    IsChecked = isChecked.HasValue ? isChecked.Value : false;
                }
            }

            public static readonly RoutedEvent<ModActiveChangedEventArgs> ModActiveChangedEvent = RoutedEvent.Register<ModDetailsView, ModActiveChangedEventArgs>(nameof(ModActiveChanged), RoutingStrategies.Direct);

            public event EventHandler<ModActiveChangedEventArgs> ModActiveChanged
            {
                add => AddHandler(ModActiveChangedEvent, value);
                remove => RemoveHandler(ModActiveChangedEvent, value);
            }

            protected virtual void OnModActiveChanged(ModModel targetMod, bool? isChecked)
            {
                RoutedEventArgs args = new ModActiveChangedEventArgs(ModActiveChangedEvent, targetMod, isChecked);
                RaiseEvent(args);
            }

        #endregion

        private bool _canTriggerEvents = false;

        public ModListView()
        {
            InitializeComponent();
            Loaded += OnViewLoaded;
        }

        private void OnViewLoaded(object? sender, RoutedEventArgs e) => _canTriggerEvents = true;

        private void OnModCheckChanged(object? sender, RoutedEventArgs e)
        {
            var _fetchSender = sender as CheckBox;
            var _fetchParent = _fetchSender.Parent.DataContext as ModModel;

            if (_fetchParent == null || !_canTriggerEvents)
                return;

            OnModActiveChanged(_fetchParent, _fetchSender.IsChecked);
        }
    }
}
