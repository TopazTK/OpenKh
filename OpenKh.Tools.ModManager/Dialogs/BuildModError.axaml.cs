using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OpenKh.Tools.ModManager.Dialogs
{
    public partial class BuildModError : Window
    {
        public BuildModError()
        {
            InitializeComponent();
        }

        private void AcceptDialog(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(true);
    }
}
