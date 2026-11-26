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

    /// <summary>
    /// RefreshView ("tõmba alla") oma lipp. Eraldi IsBusy-st, sest iOS-is kirjutab RefreshView selle ise true-ks
    /// ja kui see oleks IsBusy, jääks rakendus "laadima" ning ükski nupp (nt Tühista) ei töötaks.
    /// </summary>
    [ObservableProperty]
    private bool isRefreshing;

    public bool IsNotBusy => !IsBusy;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>Runs an API call with busy flag and friendly error message.</summary>
    protected async Task RunAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            IsRefreshing = false;
            return;
        }
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
            ErrorMessage = Loc.T("error.unexpected", ex.Message);
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    protected static Task ToastAsync(string title, string message) =>
        Shell.Current.DisplayAlertAsync(title, message, Loc.T("ok"));

    /// <summary>
    /// Turvaline "Tagasi": kui eelmist lehte pole, ei tehta midagi; viga ei sulge rakendust.
    /// </summary>
    protected async Task GoBackAsync()
    {
        try
        {
            if (Shell.Current.Navigation.NavigationStack.Count > 1)
                await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "Back");
            ErrorMessage = Loc.T("error.unexpected", ex.Message);
        }
    }

    /// <summary>Kutsutakse pärast keele või teema vahetust, et koodis arvutatud tekstid ja värvid uueneksid.</summary>
    public virtual void OnPreferencesChanged() { }
}
