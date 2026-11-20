using SportClub.Maui.ViewModels;

namespace SportClub.Maui.Views;

public partial class SchedulePage : ContentPage
{
    private readonly ScheduleViewModel _vm;

    public SchedulePage(ScheduleViewModel vm)
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
