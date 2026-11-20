using SportClub.Maui.ViewModels;

namespace SportClub.Maui.Views;

public partial class SessionDetailPage : ContentPage
{
    private readonly SessionDetailViewModel _vm;

    public SessionDetailPage(SessionDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Refresh();
    }
}
