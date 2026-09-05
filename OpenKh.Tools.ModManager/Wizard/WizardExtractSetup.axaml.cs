using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using OpenKh.Tools.ModManager.Views;

namespace OpenKh.Tools.ModManager.Wizard
{
    public partial class WizardExtractSetup : ContentPage
    {
        public WizardExtractSetup()
        {
            InitializeComponent();

            WeakReferenceMessenger.Default.Register<PageRequestMessage>(this, (registrar, message) =>
            {
                if (message.Self != this)
                    return;

                message.Reply(true);
            });
        }
    }
}
