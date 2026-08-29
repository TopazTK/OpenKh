using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace OpenKh.Tools.ModManager.Dialogs
{
    public partial class RemoveModDialog : Window
    {
        public RemoveModDialog()
        {
            InitializeComponent();
        }
        
        private void AcceptDialog(object sender, RoutedEventArgs e) => Close(true); 

        private void RejectDialog(object sender, RoutedEventArgs e) => Close(false); 
    }
}
