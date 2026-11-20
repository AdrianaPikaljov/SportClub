using CommunityToolkit.Mvvm.ComponentModel;
using SportClub.Maui.Services;

namespace SportClub.Maui.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool IsNotBusy => !IsBusy;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>Runs an API call with busy flag and friendly error message.</summary>
    protected async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ootamatu viga: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected static Task ToastAsync(string title, string message) =>
        Shell.Current.DisplayAlertAsync(title, message, "OK");
}
