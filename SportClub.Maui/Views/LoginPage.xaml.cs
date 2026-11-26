using SportClub.Maui.ViewModels;

namespace SportClub.Maui.Views;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _vm;

    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // telefonis otsitakse server ise üles
        _ = _vm.FindServerAsync();
    }
}
