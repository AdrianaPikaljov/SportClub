using SportClub.Maui.ViewModels;

namespace SportClub.Maui.Views;

public partial class BuyPassPage : ContentPage
{
    private readonly BuyPassViewModel _vm;

    public BuyPassPage(BuyPassViewModel vm)
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
