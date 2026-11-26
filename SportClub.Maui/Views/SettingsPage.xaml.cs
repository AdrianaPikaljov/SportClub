using SportClub.Maui.ViewModels;

namespace SportClub.Maui.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _vm;

    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Refresh(); // kasutaja andmed ja valitud nupud
    }
}
