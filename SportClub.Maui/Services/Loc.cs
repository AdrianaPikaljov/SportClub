using System.ComponentModel;
using System.Globalization;
using Microsoft.Maui.Controls.Xaml;

namespace SportClub.Maui.Services;

/// <summary>
/// Liidese tekstid eesti ja inglise keeles. XAML-is: Text="{loc:T login.title}".
/// Keele vahetamisel uuenevad kõik sildid kohe (PropertyChanged). API veateated jäävad eesti keelde.
/// </summary>
public class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private const string LangKey = "lang";
    private string _lang = "et";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Language => _lang;
    public bool IsEnglish => _lang == "en";

    /// <summary>Indekser, millega XAML seob: {Binding [login.title], Source={x:Static ...}}.</summary>
    // MAUI jagab binding-tee punktide kohalt, seega XAML-i võtmes on punkti asemel "_" (login_title).
    public string this[string key] => Get(key.Replace('_', '.'));

    public static string T(string key) => Instance.Get(key);

    /// <summary>T("pass.left", 3, 8) → "3 / 8 külastust alles"</summary>
    public static string T(string key, params object?[] args) => string.Format(Instance.Get(key), args);

    public string Get(string key)
    {
        var dict = _lang == "en" ? En : Et;
        return dict.TryGetValue(key, out var text) ? text : Et.TryGetValue(key, out var et) ? et : key;
    }

    /// <summary>Loeb salvestatud keele (vaikimisi eesti).</summary>
    public void Load() => SetLanguage(Preferences.Default.Get(LangKey, "et"), save: false);

    public void SetLanguage(string lang, bool save = true)
    {
        _lang = lang == "en" ? "en" : "et";
        // Nädalapäevad ja kuupäevad vastavalt keelele.
        var culture = new CultureInfo(_lang == "en" ? "en-GB" : "et-EE");
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        if (save) Preferences.Default.Set(LangKey, _lang);
        // null = kõik seotud omadused (ka indekser) uuenevad
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    /// <summary>API annab treeneri treeningu staatuse eesti keeles – tõlgime selle.</summary>
    public static string TrainerStatus(string status) => status switch
    {
        "Tulemas" => T("status.upcoming"),
        "Kohalolu märkimata" => T("status.unmarked"),
        "Kohalolu märgitud" => T("status.marked"),
        _ => status
    };

    private static readonly Dictionary<string, string> Et = new()
    {
        // vahekaardid
        ["tab.schedule"] = "Tunniplaan", ["tab.bookings"] = "Broneeringud", ["tab.pass"] = "Kuukaart",
        ["tab.sessions"] = "Treeningud", ["tab.stats"] = "Statistika", ["tab.settings"] = "Seaded",
        ["settings.title"] = "Seaded", ["settings.account"] = "Konto", ["settings.light"] = "☀ Hele", ["settings.dark"] = "☾ Tume",
        ["settings.hint"] = "Valik jääb seadmesse meelde.", ["settings.roleClient"] = "Klient", ["settings.roleTrainer"] = "Treener",
        // ühised
        ["back"] = "‹ Tagasi", ["logout"] = "Logi välja", ["cancel"] = "Loobu", ["no"] = "Ei", ["ok"] = "OK",
        ["error.unexpected"] = "Ootamatu viga: {0}", ["error.connection"] = "Serveriga ei saa ühendust ({0}). Kas API töötab?",
        ["settings.language"] = "Keel", ["settings.theme"] = "Teema", ["theme.light"] = "Hele", ["theme.dark"] = "Tume",
        ["minutes"] = "{0} min",
        // login
        ["login.tagline"] = "Broneeri trenn paari puudutusega.",
        ["login.text"] = "Vaata tunniplaani, broneeri koht ja saa teade, kui ootejärjekorras koht vabaneb.",
        ["login.email"] = "E-post", ["login.password"] = "Parool", ["login.submit"] = "Logi sisse",
        ["login.register"] = "Pole kontot? Registreeru", ["login.badEmail"] = "Sisesta korrektne e-posti aadress.",
        ["login.noPassword"] = "Sisesta parool.", ["login.adminOnly"] = "Mobiilirakendus on klientidele ja treeneritele. Admin kasutab veebiliidest.",
        // registreerimine
        ["register.title"] = "Loo konto", ["register.text"] = "Pärast registreerimist saad osta kuukaardi ja broneerida treeninguid.",
        ["register.name"] = "Nimi", ["register.password"] = "Parool (vähemalt 8 tähemärki)", ["register.confirm"] = "Parool uuesti",
        ["register.submit"] = "Registreeru", ["register.v.name"] = "Sisesta oma nimi (vähemalt 2 tähemärki).",
        ["register.v.password"] = "Parool peab olema vähemalt 8 tähemärki.", ["register.v.confirm"] = "Paroolid ei kattu.",
        // tunniplaan
        ["schedule.title"] = "Tunniplaan", ["schedule.live"] = "Reaalajas", ["schedule.empty"] = "Sel päeval treeninguid pole.",
        ["schedule.all"] = "Kõik", ["badge.full"] = "Täis", ["badge.free"] = "{0} vaba", ["badge.freeOf"] = "{0}/{1} vaba",
        // treeningu detail
        ["detail.time"] = "Aeg", ["detail.spots"] = "Kohti", ["detail.trainer"] = "Treener", ["detail.duration"] = "Kestus",
        ["detail.capacity"] = "{0} kohta", ["detail.free"] = "  vaba kohta", ["detail.booked"] = "{0} broneeritud",
        ["detail.live"] = "Uueneb reaalajas", ["detail.buyPass"] = "Osta kuukaart",
        ["detail.howTitle"] = "Kuidas ootejärjekord töötab?",
        ["detail.howText"] = "Kui keegi tühistab, viiakse järjekorras esimene automaatselt treeningule ja kuukaardilt arvestatakse külastus alles siis.",
        ["detail.book"] = "Registreeru", ["detail.waitlist"] = "Pane mind ootejärjekorda",
        ["detail.needPass"] = "Registreerumiseks on vaja kehtivat kuukaarti.",
        ["detail.passInfo"] = "Registreerumine kasutab 1 külastuse. Kuukaardil on alles {0} / {1}.",
        ["detail.inQueue"] = "Oled ootejärjekorras, koht #{0}", ["detail.hasSpot"] = "Sul on sellele treeningule koht ✓",
        ["detail.fullTitle"] = "Treening on täis",
        ["detail.fullText"] = "Sind lisati ootejärjekorda, sinu koht: {0}. Kui koht vabaneb, saad automaatselt treeningule.",
        ["detail.bookedTitle"] = "Registreeritud!", ["detail.bookedText"] = "{0} – {1}. Meeldetuletus tuleb tund enne algust.",
        // broneeringud
        ["bookings.title"] = "Broneeringud", ["bookings.upcoming"] = "Tulevased", ["bookings.waitlist"] = "Ootel",
        ["bookings.waitlistCount"] = "Ootel ({0})", ["bookings.history"] = "Ajalugu",
        ["bookings.reminder"] = "Meeldetuletus tuleb tund enne treeningut.", ["bookings.noUpcoming"] = "Tulevasi broneeringuid pole.",
        ["bookings.noWaitlist"] = "Sa ei ole ühegi treeningu ootejärjekorras.", ["bookings.noHistory"] = "Ajalugu on tühi.",
        ["bookings.cancel"] = "Tühista", ["bookings.leave"] = "Lahku järjekorrast",
        ["bookings.cancelTitle"] = "Tühista broneering", ["bookings.cancelText"] = "{0}, {1}?\nKülastus läheb kuukaardile tagasi.",
        ["bookings.leaveTitle"] = "Lahku järjekorrast", ["bookings.leaveText"] = "{0}, {1}?", ["bookings.leaveButton"] = "Lahku",
        ["tag.present"] = "Kohal", ["tag.absent"] = "Puudus", ["tag.confirmed"] = "Kinnitatud", ["tag.unmarked"] = "Märkimata",
        ["tag.queue"] = "Järjekorras #{0}",
        // kuukaart
        ["pass.title"] = "Kuukaart", ["pass.active"] = "Aktiivne", ["pass.left"] = "  / {0} külastust alles",
        ["pass.none"] = "Sul pole veel kuukaarti. Osta kuukaart, et treeninguid broneerida.",
        ["pass.visits"] = "Viimased külastused", ["pass.noVisits"] = "Külastusi veel pole.", ["pass.buy"] = "Osta kuukaart",
        ["pass.valid"] = "Kehtib {0:dd.MM.yyyy} – {1:dd.MM.yyyy}",
        // ost
        ["buy.title"] = "Vali pakett", ["buy.text"] = "Iga broneering kasutab ühe külastuse. Tühistamisel saad külastuse tagasi.",
        ["buy.simulated"] = "Makse on simuleeritud – päris raha ei võeta.", ["buy.choose"] = "Vali pakett", ["buy.button"] = "Osta – {0}",
        ["buy.validDays"] = "Kehtib {0} päeva ostupäevast.", ["buy.visits"] = "{0} külastust · {1} päeva", ["buy.perVisit"] = "{0} € / treening",
        ["buy.startNow"] = "Kuukaart hakkab kehtima kohe pärast ostu.",
        ["buy.hasPass"] = "Sul on juba kehtiv kuukaart ({0} külastust alles, kuni {1:dd.MM.yyyy}). Uue saad osta, kui see on läbi.",
        ["buy.confirmTitle"] = "Kinnita ost",
        ["buy.confirmText"] = "{0}\n{1}\nHind: {2}\n\nMakse on selles demorakenduses simuleeritud – päris raha ei võeta.",
        ["buy.pay"] = "Maksa", ["buy.doneTitle"] = "Kuukaart ostetud",
        ["card.title"] = "Kaardimakse", ["card.number"] = "Kaardi number", ["card.holder"] = "Kaardiomaniku nimi", ["card.expiry"] = "Kehtiv kuni (KK/AA)",
        ["card.test"] = "Testkaart: 4242 4242 4242 4242 (õnnestub) · 4000 0000 0000 0002 (tagasi lükatud). Kaardi andmeid ei salvestata.",
        ["card.errNumber"] = "Kontrolli kaardi numbrit.", ["card.errHolder"] = "Sisesta kaardiomaniku nimi.",
        ["card.errExpiry"] = "Kehtivus peab olema kujul KK/AA, nt 12/28.", ["card.errExpired"] = "Kaart on aegunud.",
        ["card.errCvc"] = "CVC peab olema 3–4 numbrit.",
        ["buy.doneText"] = "{0}: {1} külastust, kehtib kuni {2:dd.MM.yyyy}. Saad kohe treeninguid broneerida!",
        // treener
        ["trainer.title"] = "Minu treeningud", ["trainer.greeting"] = "Tere, {0}", ["trainer.participants"] = " osalejat",
        ["trainer.mark"] = "Märgi kohalolu", ["trainer.all"] = "Kõik treeningud", ["trainer.empty"] = "Treeninguid pole.",
        ["trainer.now"] = "Käimas", ["trainer.inMinutes"] = "Järgmine · {0} min pärast", ["trainer.nextDay"] = "Järgmine · {0}",
        ["trainer.waiting"] = "{0} · ootel {1}",
        ["trainer.unmarkedWarning"] = "⚠ {0} toimunud treeningul on kohalolu märkimata ({1} inimest). Ava treening ja märgi iga osaleja: kohal või puudus.",
        ["trainer.groupUnmarked"] = "Kohalolu märkimata ({0})", ["trainer.groupUpcoming"] = "Tulevased", ["trainer.groupPast"] = "Toimunud",
        ["trainer.unmarkedCount"] = "Märkimata {0} / {1}",
        ["status.upcoming"] = "Tulemas", ["status.unmarked"] = "Kohalolu märkimata", ["status.marked"] = "Kohalolu märgitud",
        // kohalolu
        ["att.title"] = "Kohalolu", ["att.present"] = "Kohal", ["att.absent"] = "Puudus", ["att.unmarked"] = "Märkimata",
        ["att.empty"] = "Sellele treeningule pole veel keegi registreerunud.", ["att.markRest"] = "Märgi ülejäänud kohal", ["att.subtitle"] = "{0} · {1} · {2} osalejat",
        // statistika
        ["stats.title"] = "Statistika", ["stats.week"] = "Nädal", ["stats.month"] = "Kuu", ["stats.all"] = "Kõik",
        ["stats.bookings"] = "broneeringut", ["stats.fill"] = "keskmine täituvus", ["stats.popular"] = "Populaarseimad treeningutüübid",
        ["stats.empty"] = "Andmed puuduvad.", ["stats.row"] = "{0} broneeringut · kohal {1} · täituvus {2}%"
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["tab.schedule"] = "Schedule", ["tab.bookings"] = "Bookings", ["tab.pass"] = "Pass",
        ["tab.sessions"] = "Trainings", ["tab.stats"] = "Statistics", ["tab.settings"] = "Settings",
        ["settings.title"] = "Settings", ["settings.account"] = "Account", ["settings.light"] = "☀ Light", ["settings.dark"] = "☾ Dark",
        ["settings.hint"] = "Your choice is saved on this device.", ["settings.roleClient"] = "Client", ["settings.roleTrainer"] = "Trainer",
        ["back"] = "‹ Back", ["logout"] = "Log out", ["cancel"] = "Cancel", ["no"] = "No", ["ok"] = "OK",
        ["error.unexpected"] = "Unexpected error: {0}", ["error.connection"] = "Cannot reach the server ({0}). Is the API running?",
        ["settings.language"] = "Language", ["settings.theme"] = "Theme", ["theme.light"] = "Light", ["theme.dark"] = "Dark",
        ["minutes"] = "{0} min",
        ["login.tagline"] = "Book a workout in a few taps.",
        ["login.text"] = "See the schedule, book a spot and get notified when a waitlist spot opens up.",
        ["login.email"] = "E-mail", ["login.password"] = "Password", ["login.submit"] = "Log in",
        ["login.register"] = "No account? Sign up", ["login.badEmail"] = "Enter a valid e-mail address.",
        ["login.noPassword"] = "Enter your password.", ["login.adminOnly"] = "The mobile app is for clients and trainers. Admins use the web UI.",
        ["register.title"] = "Create account", ["register.text"] = "After signing up you can buy a monthly pass and book trainings.",
        ["register.name"] = "Name", ["register.password"] = "Password (at least 8 characters)", ["register.confirm"] = "Repeat password",
        ["register.submit"] = "Sign up", ["register.v.name"] = "Enter your name (at least 2 characters).",
        ["register.v.password"] = "Password must be at least 8 characters.", ["register.v.confirm"] = "Passwords do not match.",
        ["schedule.title"] = "Schedule", ["schedule.live"] = "Live", ["schedule.empty"] = "No trainings on this day.",
        ["schedule.all"] = "All", ["badge.full"] = "Full", ["badge.free"] = "{0} free", ["badge.freeOf"] = "{0}/{1} free",
        ["detail.time"] = "Time", ["detail.spots"] = "Spots", ["detail.trainer"] = "Trainer", ["detail.duration"] = "Duration",
        ["detail.capacity"] = "{0} spots", ["detail.free"] = "  spots free", ["detail.booked"] = "{0} booked",
        ["detail.live"] = "Updates live", ["detail.buyPass"] = "Buy a monthly pass",
        ["detail.howTitle"] = "How does the waitlist work?",
        ["detail.howText"] = "When someone cancels, the first person in the queue is moved to the training automatically, and only then is a visit used from the pass.",
        ["detail.book"] = "Book", ["detail.waitlist"] = "Put me on the waitlist",
        ["detail.needPass"] = "You need a valid monthly pass to book.",
        ["detail.passInfo"] = "Booking uses 1 visit. Your pass has {0} / {1} left.",
        ["detail.inQueue"] = "You are on the waitlist, place #{0}", ["detail.hasSpot"] = "You have a spot in this training ✓",
        ["detail.fullTitle"] = "The training is full",
        ["detail.fullText"] = "You were added to the waitlist, your place: {0}. If a spot opens up, you get into the training automatically.",
        ["detail.bookedTitle"] = "Booked!", ["detail.bookedText"] = "{0} – {1}. You get a reminder an hour before the start.",
        ["bookings.title"] = "Bookings", ["bookings.upcoming"] = "Upcoming", ["bookings.waitlist"] = "Waitlist",
        ["bookings.waitlistCount"] = "Waitlist ({0})", ["bookings.history"] = "History",
        ["bookings.reminder"] = "You get a reminder an hour before the training.", ["bookings.noUpcoming"] = "No upcoming bookings.",
        ["bookings.noWaitlist"] = "You are not on any waitlist.", ["bookings.noHistory"] = "History is empty.",
        ["bookings.cancel"] = "Cancel", ["bookings.leave"] = "Leave waitlist",
        ["bookings.cancelTitle"] = "Cancel booking", ["bookings.cancelText"] = "{0}, {1}?\nThe visit goes back to your pass.",
        ["bookings.leaveTitle"] = "Leave waitlist", ["bookings.leaveText"] = "{0}, {1}?", ["bookings.leaveButton"] = "Leave",
        ["tag.present"] = "Present", ["tag.absent"] = "Absent", ["tag.confirmed"] = "Confirmed", ["tag.unmarked"] = "Not marked",
        ["tag.queue"] = "In queue #{0}",
        ["pass.title"] = "Monthly pass", ["pass.active"] = "Active", ["pass.left"] = "  / {0} visits left",
        ["pass.none"] = "You don't have a monthly pass yet. Buy one to book trainings.",
        ["pass.visits"] = "Recent visits", ["pass.noVisits"] = "No visits yet.", ["pass.buy"] = "Buy a monthly pass",
        ["pass.valid"] = "Valid {0:dd.MM.yyyy} – {1:dd.MM.yyyy}",
        ["buy.title"] = "Choose a plan", ["buy.text"] = "Every booking uses one visit. When you cancel, you get the visit back.",
        ["buy.simulated"] = "Payment is simulated – no real money is charged.", ["buy.choose"] = "Choose a plan", ["buy.button"] = "Buy – {0}",
        ["buy.validDays"] = "Valid for {0} days from purchase.", ["buy.visits"] = "{0} visits · {1} days", ["buy.perVisit"] = "{0} € / training",
        ["buy.startNow"] = "The pass is valid right after purchase.",
        ["buy.hasPass"] = "You already have a valid pass ({0} visits left, until {1:dd.MM.yyyy}). You can buy a new one when it runs out.",
        ["buy.confirmTitle"] = "Confirm purchase",
        ["buy.confirmText"] = "{0}\n{1}\nPrice: {2}\n\nPayment is simulated in this demo app – no real money is charged.",
        ["buy.pay"] = "Pay", ["buy.doneTitle"] = "Pass bought",
        ["card.title"] = "Card payment", ["card.number"] = "Card number", ["card.holder"] = "Cardholder name", ["card.expiry"] = "Expires (MM/YY)",
        ["card.test"] = "Test card: 4242 4242 4242 4242 (succeeds) · 4000 0000 0000 0002 (declined). Card details are not stored.",
        ["card.errNumber"] = "Check the card number.", ["card.errHolder"] = "Enter the cardholder name.",
        ["card.errExpiry"] = "Expiry must be MM/YY, e.g. 12/28.", ["card.errExpired"] = "The card has expired.",
        ["card.errCvc"] = "CVC must be 3–4 digits.",
        ["buy.doneText"] = "{0}: {1} visits, valid until {2:dd.MM.yyyy}. You can book trainings right away!",
        ["trainer.title"] = "My trainings", ["trainer.greeting"] = "Hi, {0}", ["trainer.participants"] = " participants",
        ["trainer.mark"] = "Mark attendance", ["trainer.all"] = "All trainings", ["trainer.empty"] = "No trainings.",
        ["trainer.now"] = "In progress", ["trainer.inMinutes"] = "Next · in {0} min", ["trainer.nextDay"] = "Next · {0}",
        ["trainer.waiting"] = "{0} · {1} waiting",
        ["trainer.unmarkedWarning"] = "⚠ {0} past trainings have unmarked attendance ({1} people). Open the training and mark everyone: present or absent.",
        ["trainer.groupUnmarked"] = "Attendance not marked ({0})", ["trainer.groupUpcoming"] = "Upcoming", ["trainer.groupPast"] = "Past",
        ["trainer.unmarkedCount"] = "Not marked {0} / {1}",
        ["status.upcoming"] = "Upcoming", ["status.unmarked"] = "Attendance not marked", ["status.marked"] = "Attendance marked",
        ["att.title"] = "Attendance", ["att.present"] = "Present", ["att.absent"] = "Absent", ["att.unmarked"] = "Not marked",
        ["att.empty"] = "Nobody has booked this training yet.", ["att.markRest"] = "Mark the rest as present", ["att.subtitle"] = "{0} · {1} · {2} participants",
        ["stats.title"] = "Statistics", ["stats.week"] = "Week", ["stats.month"] = "Month", ["stats.all"] = "All",
        ["stats.bookings"] = "bookings", ["stats.fill"] = "average fill rate", ["stats.popular"] = "Most popular training types",
        ["stats.empty"] = "No data.", ["stats.row"] = "{0} bookings · attended {1} · fill {2}%"
    };
}

/// <summary>XAML-i lühike kuju: Text="{loc:T schedule.title}". Tekst uueneb keele vahetamisel.</summary>
[ContentProperty(nameof(Key))]
[AcceptEmptyServiceProvider]
public class TExtension : IMarkupExtension<BindingBase>
{
    public string Key { get; set; } = "";

    public BindingBase ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key.Replace('.', '_')}]", BindingMode.OneWay, source: Loc.Instance);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}

/// <summary>
/// Hele ja tume teema. Värvid on rakenduse ressurssides ja XAML kasutab neid {DynamicResource ...} kaudu,
/// nii et teema vahetub kohe. Tumedad paneelid (päis, sisselogimine) jäävad mõlemas teemas tumedaks.
/// </summary>
public static class ThemeService
{
    // Uus võti: varem salvestatud "light" ei kehti, vaikimisi on tume teema.
    private const string ThemeKey = "app_theme";

    public static bool IsDark { get; private set; }

    public static event EventHandler? Changed;

    private static readonly Dictionary<string, (string Light, string Dark)> Palette = new()
    {
        ["Ink"] = ("#14161A", "#F4F5F2"),       // põhitekst
        ["Ink2"] = ("#3A3F47", "#C9CDD3"),
        ["Muted"] = ("#5B616B", "#9EA4AD"),
        ["Ground"] = ("#F4F5F2", "#0E1013"),    // lehe taust
        ["Surface"] = ("#FFFFFF", "#1A1D22"),   // kaardid
        ["Line"] = ("#E3E5E0", "#2A2E35"),
        ["Border"] = ("#D5D8D2", "#353A42"),
        ["Segment"] = ("#E6E8E3", "#23272D"),   // vahekaartide taust
        ["Panel"] = ("#14161A", "#1F232A"),     // tumedad paneelid
        ["Selected"] = ("#14161A", "#D4FF3A"),  // valitud päev / pakett
        ["OnSelected"] = ("#F4F5F2", "#14161A"),
        ["SubOnSelected"] = ("#B7BCC4", "#3A3F47"),
        ["Bar"] = ("#7A808A", "#6E747D"),
        ["TopBar"] = ("#14161A", "#D4FF3A"),
        ["DangerBg"] = ("#FFF6F1", "#2E1A12"), ["DangerBorder"] = ("#F3C4AC", "#6B3418"),
        ["OkBg"] = ("#E1F4E7", "#12301F"), ["OkText"] = ("#17663A", "#6FD99B"),
        ["FewBg"] = ("#FFF1D6", "#33260D"), ["FewText"] = ("#8A4B00", "#F5C26B"),
        ["FullBg"] = ("#FDE8DC", "#3A1A0C"), ["FullText"] = ("#A33A08", "#FF9A6B")
    };

    /// <summary>Loeb salvestatud teema (vaikimisi tume).</summary>
    public static void Load() => Apply(Preferences.Default.Get(ThemeKey, "dark") == "dark", save: false);

    public static void Apply(bool dark, bool save = true)
    {
        IsDark = dark;
        var app = Application.Current;
        if (app is not null)
        {
            foreach (var (key, (light, darkColor)) in Palette)
                app.Resources[key] = Microsoft.Maui.Graphics.Color.FromArgb(dark ? darkColor : light);
            app.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;
        }
        if (save) Preferences.Default.Set(ThemeKey, dark ? "dark" : "light");
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Värv koodist (ViewModelid), nt ThemeService.Get("Surface").</summary>
    public static Color Get(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color c
            ? c
            : Color.FromArgb(Palette[key].Light);
}
