using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OpenKh.Tools.ModManager.Models;
using System;

namespace OpenKh.Tools.ModManager.Views
{
    public partial class ModListView : ContentPage
    {
        public ModListView()
        {
            InitializeComponent();

            ModList.ItemsSource = new ModModel[]
            {
                new ModModel{ ModTitle = "Kingdom Hearts II - Re:Fined", ModAuthor = "The Re:Fined Team", ModDescription = "Kingdom Hearts - Re:Fined for Kingdom Hearts II | r190826-0855-FC", ModSource = new Uri("https://codeberg.org/KH-ReFined/KH-ReFined"), ModIssues = new Uri("https://codeberg.org/KH-ReFined/KH-ReFined/issues"), ModActive = true },
                new ModModel{ ModTitle = "GOA ROM Edition", ModAuthor = "1234567890num", ModActive = false }
            };
        }
    }
}
