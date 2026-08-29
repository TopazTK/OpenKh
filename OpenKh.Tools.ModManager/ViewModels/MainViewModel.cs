using CommunityToolkit.Mvvm.ComponentModel;
using OpenKh.Tools.ModManager.Models;

namespace OpenKh.Tools.ModManager.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ModModel? _currentMod = null;

    [ObservableProperty]
    private bool _configurationValid = true;
}
