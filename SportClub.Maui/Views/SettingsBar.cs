using SportClub.Maui.Services;
using SportClub.Maui.ViewModels;

namespace SportClub.Maui.Views;

/// <summary>
/// Keele (ET / EN) ja teema (hele / tume) lülitid. Kasutus XAML-is: &lt;views:SettingsBar /&gt;.
/// Pärast vahetust kutsub lehe ViewModeli OnPreferencesChanged(), et koodis arvutatud tekstid uueneksid.
/// </summary>
public class SettingsBar : ContentView
{
    private readonly Button _et, _en, _light, _dark;
    private readonly Border _themeGroup;

    /// <summary>false = ainult keele valik (nt sisselogimise lehel). XAML: ShowTheme="False".</summary>
    public bool ShowTheme
    {
        get => _themeGroup.IsVisible;
        set => _themeGroup.IsVisible = value;
    }

    public SettingsBar()
    {
        _et = Segment("ET", () => Loc.Instance.SetLanguage("et"));
        _en = Segment("EN", () => Loc.Instance.SetLanguage("en"));
        _light = Segment("☀", () => ThemeService.Apply(false));
        _dark = Segment("☾", () => ThemeService.Apply(true));

        _themeGroup = Group(_light, _dark);
        Content = new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { Group(_et, _en), _themeGroup }
        };

        Loc.Instance.PropertyChanged += (_, _) => Refresh();
        ThemeService.Changed += (_, _) => Refresh();
        Refresh();
    }

    private Button Segment(string text, Action onTap)
    {
        var button = new Button
        {
            Text = text,
            FontFamily = "OpenSansSemibold",
            FontSize = 13,
            HeightRequest = 34,
            MinimumWidthRequest = 44,
            Padding = new Thickness(12, 0),
            CornerRadius = 9,
            BorderWidth = 0
        };
        button.Clicked += (_, _) =>
        {
            onTap();
            (BindingContext as BaseViewModel)?.OnPreferencesChanged();
        };
        return button;
    }

    private static Border Group(Button a, Button b) => new()
    {
        Padding = 3,
        StrokeThickness = 0,
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
        BackgroundColor = Color.FromArgb("#E6E8E3"),
        Content = new HorizontalStackLayout { Spacing = 2, Children = { a, b } }
    };

    private void Refresh()
    {
        Style(_et, !Loc.Instance.IsEnglish);
        Style(_en, Loc.Instance.IsEnglish);
        Style(_light, !ThemeService.IsDark);
        Style(_dark, ThemeService.IsDark);
    }

    // Valitud nupp on laimiroheline; lülitite taust on mõlemas teemas hele, seega tekst on alati tume.
    private static void Style(Button button, bool selected)
    {
        button.BackgroundColor = selected ? Color.FromArgb("#D4FF3A") : Colors.Transparent;
        button.TextColor = selected ? Color.FromArgb("#14161A") : Color.FromArgb("#5B616B");
    }
}
