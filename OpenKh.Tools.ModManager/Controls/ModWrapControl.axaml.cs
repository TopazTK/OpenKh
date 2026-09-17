using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenKh.Tools.ModManager.Models;
using System;
using System.Windows.Input;

namespace OpenKh.Tools.ModManager.Views
{
    public partial class ModWrapView : ContentPage
    {
        public static readonly StyledProperty<ICommand?> ModSelectedRequestedProperty = AvaloniaProperty.Register<ModStatusView, ICommand?>(nameof(ModSelectedRequested));
        public ICommand? ModSelectedRequested { get => GetValue(ModSelectedRequestedProperty); set => SetValue(ModSelectedRequestedProperty, value); }

        private void OnModSelectedRequested(object? sender, RoutedEventArgs e)
        {
            if (ModSelectedRequested?.CanExecute(null) == true)
                ModSelectedRequested.Execute(null);
        }

        public ModWrapView()
        {
            InitializeComponent();
        }

        private void OnModSelectedChanged(object? sender, RoutedEventArgs e)
        {
            var _fetchSender = sender as CheckBox;
            var _fetchParent = _fetchSender.Parent.DataContext as ModModel;

            if (_fetchParent == null)
                return;

            var _targetTuple = new Tuple<ModModel, bool>(_fetchParent, _fetchSender.IsChecked ?? false);

            if (ModSelectedRequested?.CanExecute(_targetTuple) == true)
                ModSelectedRequested.Execute(_targetTuple);
        }
    }
}
