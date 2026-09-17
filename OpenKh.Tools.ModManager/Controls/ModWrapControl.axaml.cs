using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenKh.Tools.ModManager.Models;
using System;

namespace OpenKh.Tools.ModManager.Views
{
    public class ModSelectedChangedEventArgs : RoutedEventArgs
    {
        public ModModel TargetMod { get; }
        public bool IsChecked { get; }

        public ModSelectedChangedEventArgs(RoutedEvent routedEvent, ModModel targetMod, bool? isChecked) : base(routedEvent)
        {
            TargetMod = targetMod;
            IsChecked = isChecked.HasValue ? isChecked.Value : false;
        }
    }

    public partial class ModWrapView : ContentPage
    {
        #region Custom Events

        public static readonly RoutedEvent<ModSelectedChangedEventArgs> ModSelectedChangedEvent = RoutedEvent.Register<ModDetailsView, ModSelectedChangedEventArgs>(nameof(ModSelectedChanged), RoutingStrategies.Direct);

        public event EventHandler<ModSelectedChangedEventArgs> ModSelectedChanged
        {
            add => AddHandler(ModSelectedChangedEvent, value);
            remove => RemoveHandler(ModSelectedChangedEvent, value);
        }

        protected virtual void OnModSelectedChanged(ModModel targetMod, bool? isChecked)
        {
            RoutedEventArgs args = new ModSelectedChangedEventArgs(ModSelectedChangedEvent, targetMod, isChecked);
            RaiseEvent(args);
        }

        #endregion

        public bool CanTriggerEvents = false;

        public ModWrapView()
        {
            InitializeComponent();

            Loaded += OnViewLoaded;
        }

        private void OnViewLoaded(object? sender, RoutedEventArgs e) => CanTriggerEvents = true;

        private void OnModCheckChanged(object? sender, RoutedEventArgs e)
        {
            var _fetchSender = sender as CheckBox;
            var _fetchParent = _fetchSender.Parent.DataContext as ModModel;

            if (_fetchParent == null || !CanTriggerEvents)
                return;

            OnModSelectedChanged(_fetchParent, _fetchSender.IsChecked);
        }
    }
}
