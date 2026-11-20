using SportClub.Maui.ViewModels;

namespace SportClub.Maui.Views;

public partial class AttendancePage : ContentPage
{
    private readonly AttendanceViewModel _vm;

    public AttendancePage(AttendanceViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }
}
