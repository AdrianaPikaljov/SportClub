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

    /// <summary>Kaardi number neljaste gruppidena: 4242 4242 4242 4242.</summary>
    private void OnCardNumberChanged(object? sender, TextChangedEventArgs e)
    {
        var digits = new string((e.NewTextValue ?? "").Where(char.IsDigit).Take(19).ToArray());
        var formatted = string.Join(" ", Enumerable.Range(0, (digits.Length + 3) / 4)
            .Select(i => digits.Substring(i * 4, Math.Min(4, digits.Length - i * 4))));
        Apply(sender as Entry, e.NewTextValue, formatted);
    }

    /// <summary>Kehtivus: kaldkriips lisatakse ise, "1228" → "12/28".</summary>
    private void OnExpiryChanged(object? sender, TextChangedEventArgs e)
    {
        var digits = new string((e.NewTextValue ?? "").Where(char.IsDigit).Take(4).ToArray());
        var formatted = digits.Length > 2 ? $"{digits[..2]}/{digits[2..]}" : digits;
        Apply(sender as Entry, e.NewTextValue, formatted);
    }

    private void Apply(Entry? entry, string? current, string formatted)
    {
        if (entry is null || current == formatted) return;
        entry.Text = formatted;
        // kursor jääb teksti lõppu
        Dispatcher.Dispatch(() => entry.CursorPosition = entry.Text?.Length ?? 0);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.ClearCard(); // kaardi andmed ei jää mällu
    }
}
