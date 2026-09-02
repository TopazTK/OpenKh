using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OpenKh.Tools.ModManager.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenKh.Tools.ModManager.Dialogs
{
    public partial class ModProgressDialog : Window
    {
        public ModProgressDialog()
        {
            InitializeComponent();
        }

        private void OnCancelEvent(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ModService.CancelTokenSource.Cancel();
            Close(false);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (e.CloseReason == WindowCloseReason.WindowClosing && !e.IsProgrammatic)
                ModService.CancelTokenSource.Cancel();
        }
    }
}
