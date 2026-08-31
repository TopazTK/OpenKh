using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OpenKh.Tools.ModManager.Services;

namespace OpenKh.Tools.ModManager.Dialogs
{
    public partial class BuildProgressDialog : Window
    {
        public BuildProgressDialog()
        {
            InitializeComponent();
        }

        private void OnCancelEvent(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            InstallService.CancelTokenSource.Cancel();
            Close(false);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (e.CloseReason == WindowCloseReason.WindowClosing && !e.IsProgrammatic)
                InstallService.CancelTokenSource.Cancel();
        }
    }
}
