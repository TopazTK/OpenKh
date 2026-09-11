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
        public Func<Window?, string?>? MiscButtonCallback { get; set; }

        public string? Result { get; set; }

        public InputDialog()
        {
            InitializeComponent();
            Loaded += OnViewLoaded;
        }

        private void OnViewLoaded(object? sender, RoutedEventArgs e)
        {
            InputText.Focus();
            MiscButton.IsVisible = MiscButtonCallback == null ? false : true;
        }

        private void OnAcceptClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(Result = InputText.Text);

        private void OnMiscClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (MiscButtonCallback != null)
                Close(Result = MiscButtonCallback(this));
        }
    }
}
