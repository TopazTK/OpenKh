using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using System.IO;

namespace OpenKh.Tools.ModManager.Dialogs
{
    public partial class InstallModDialog : Window
    {
        public InstallModDialog()
        {
            InitializeComponent();
            Loaded += OnViewLoaded;
        }

        private async void OnViewLoaded(object? sender, RoutedEventArgs e)
        {
            RepositoryName.Focus();
        }

        private void AcceptDialog(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(RepositoryName.Text);

        private async void MiscAcceptDialog(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var _fetchTopLevel = TopLevel.GetTopLevel(this);

            var _fetchFiles = await _fetchTopLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select an Archive or Script File...",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("OpenKH Mod Archive") { Patterns = new[] { "*.zip" } },
                    new FilePickerFileType("PCPatch Package") { Patterns = new[] { "*.kh1pcpatch", "*.kh2pcpatch", "*.bbspcpatch", "*.compcpatch", "*.dddpcpatch" } },
                    new FilePickerFileType("LuaBackend Script") { Patterns = new[] { "*.lua" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } },
                }
            });

            if (_fetchFiles.Count >= 1)
                Close(_fetchFiles[0].Path.AbsolutePath);
        }
    }
}
