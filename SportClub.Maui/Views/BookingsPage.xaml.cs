using SportClub.Maui.ViewModels;

namespace SportClub.Maui.Views;

public partial class BookingsPage : ContentPage
{
    private readonly BookingsViewModel _vm;

    public BookingsPage(BookingsViewModel vm)
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
