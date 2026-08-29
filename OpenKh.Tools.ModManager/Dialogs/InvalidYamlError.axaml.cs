using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OpenKh.Tools.ModManager.Services;

namespace OpenKh.Tools.ModManager.Dialogs
{
    public partial class InvalidYamlError : Window
    {
        public InvalidYamlError()
        {
            InitializeComponent();
        }

        private void AcceptDialog(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(true);
    }
}
