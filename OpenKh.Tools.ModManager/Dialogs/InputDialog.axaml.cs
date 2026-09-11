using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System;

namespace OpenKh.Tools.ModManager.Dialogs
{
    public partial class InputDialog : Window
    {
        private Func<object?, bool>? _callbackMisc { get; set; }

        public InputDialog()
        {
            InitializeComponent();
            Loaded += OnViewLoaded;
        }

        private void OnViewLoaded(object? sender, RoutedEventArgs e) => InputText.Focus();
        
        private void OnAcceptClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(InputText.Text);

        private void OnMiscClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _callbackMisc(this);
    }
}
