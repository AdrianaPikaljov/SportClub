using SportClub.Maui.ViewModels;

namespace SportClub.Maui.Views;

public partial class PassPage : ContentPage
{
    private readonly PassViewModel _vm;

    public PassPage(PassViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadCommand.Execute(null);
    }
}
