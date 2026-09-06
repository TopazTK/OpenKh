using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OpenKh.Tools.ModManager.Dialogs
{
    public partial class PanaceaInstallDialog : Window
    {
        public PanaceaInstallDialog()
        {
            InitializeComponent();
        }
        private void AcceptDialog(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(true);
    }
}
