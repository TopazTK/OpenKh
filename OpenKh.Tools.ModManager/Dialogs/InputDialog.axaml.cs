using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System;
using System.Threading.Tasks;

namespace OpenKh.Tools.ModManager.Dialogs
{
    public partial class InputDialog : Window
    {
        public Func<Window?, Task<string?>>? MiscButtonCallback { get; set; }

        public string? Result { get; set; }

        public InputDialog()
        {
            InitializeComponent();
            Loaded += OnViewLoaded;
        }

        private void OnViewLoaded(object? sender, RoutedEventArgs e)
        {
            InputText.Focus();

            InputText.MaxWidth = this.Width;
            MiscButton.IsVisible = MiscButtonCallback == null ? false : true;
        }

        private void OnAcceptClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(Result = InputText.Text);

        private async void OnMiscClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (MiscButtonCallback != null)
            {
                var _fetchResult = await MiscButtonCallback(this);

                if (!String.IsNullOrEmpty(_fetchResult))
                    Close(Result = _fetchResult);
            }
        }
    }
}
