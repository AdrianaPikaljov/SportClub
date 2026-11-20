using SportClub.Maui.ViewModels;

namespace SportClub.Maui.Views;

public partial class TrainerSessionsPage : ContentPage
{
    private readonly TrainerSessionsViewModel _vm;

    public TrainerSessionsPage(TrainerSessionsViewModel vm)
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
