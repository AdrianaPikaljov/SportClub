using System.Net;
using SportClub.Core.Entities;

namespace SportClub.Core.Services;

/// <summary>Klientidele saadetavate e-kirjade tekstid (HTML, eesti keeles).</summary>
public static class EmailTemplates
{
    public static (string Subject, string Html) Welcome(User user) => (
        "Tere tulemast SportClubi!",
        Layout(user.FullName,
            $"<p>Sinu konto on loodud. Logi rakendusse sisse e-postiga <b>{E(user.Email)}</b>.</p>" +
            "<p>Järgmine samm: osta kuukaart ja registreeru treeningutele.</p>"));

    /// <summary>Admin lõi treeneri – tal on nüüd oma konto. Parooli kirja ei panda, selle annab administraator.</summary>
    public static (string Subject, string Html) TrainerWelcome(User user) => (
        "Sinu SportClubi treeneri konto",
        Layout(user.FullName,
            $"<p>Administraator lõi sulle SportClubis treeneri konto. Logi rakendusse sisse e-postiga <b>{E(user.Email)}</b> " +
            "ja parooliga, mille said administraatorilt.</p>" +
            "<p>Rakenduses näed oma treeninguid, osalejate nimekirja ja saad märkida kohalolu.</p>"));

    /// <summary>Admin muutis treeneri parooli.</summary>
    public static (string Subject, string Html) PasswordChanged(User user) => (
        "Sinu SportClubi parool muudeti",
        Layout(user.FullName,
            $"<p>Administraator muutis sinu konto (<b>{E(user.Email)}</b>) parooli. Uue parooli saad administraatorilt.</p>" +
            "<p>Kui sa seda ei oodanud, võta ühendust klubiga.</p>"));

    public static (string Subject, string Html) Receipt(User user, MonthlyPass pass) => (
        $"Kviitung: {pass.PlanName}",
        Layout(user.FullName,
            "<p>Aitäh ostu eest! Sinu kuukaart on aktiivne.</p>" +
            Table(
                ("Tariif", pass.PlanName),
                ("Külastusi", pass.VisitsTotal.ToString()),
                ("Kehtib", $"{pass.ValidFrom:dd.MM.yyyy} – {pass.ValidUntil:dd.MM.yyyy}"),
                ("Summa", $"{pass.PricePaid:0.00} €"),
                ("Makseviis", $"{pass.CardBrand} •••• {pass.CardLast4}"),
                ("Makse viide", pass.PaymentReference ?? "")) +
            "<p style='color:#5b616b;font-size:13px'>Makse on simuleeritud. Kaardi andmeid ei salvestata.</p>"));

    public static (string Subject, string Html) Booked(User user, Session s) => (
        $"Registreeritud: {Name(s)} {When(s)}",
        Layout(user.FullName,
            $"<p>Oled kirjas treeningule <b>{E(Name(s))}</b>.</p>" + Details(s) +
            "<p>Kuukaardilt võeti 1 külastus. Tühistada saad kuni 2 tundi enne algust.</p>"));

    public static (string Subject, string Html) Waitlisted(User user, Session s, int position) => (
        $"Ootejärjekorras: {Name(s)} {When(s)}",
        Layout(user.FullName,
            $"<p>Treening <b>{E(Name(s))}</b> on täis. Sind lisati ootejärjekorda, sinu koht: <b>{position}</b>.</p>" + Details(s) +
            "<p>Kui koht vabaneb, viime sind automaatselt treeningule ja anname sellest teada.</p>"));

    public static (string Subject, string Html) Promoted(User user, Session s) => (
        $"Said koha: {Name(s)} {When(s)}",
        Layout(user.FullName,
            $"<p>Hea uudis! Ootejärjekorrast vabanes koht ja sa oled nüüd kirjas treeningule <b>{E(Name(s))}</b>.</p>" + Details(s) +
            "<p>Kuukaardilt võeti 1 külastus.</p>"));

    public static (string Subject, string Html) Reminder(User user, Session s) => (
        $"Meeldetuletus: {Name(s)} algab {s.StartsAt:HH:mm}",
        Layout(user.FullName,
            $"<p>Tuletame meelde: sinu treening <b>{E(Name(s))}</b> algab umbes tunni pärast.</p>" + Details(s)));

    public static (string Subject, string Html) SessionCancelled(User user, Session s) => (
        $"Treening tühistati: {Name(s)} {When(s)}",
        Layout(user.FullName,
            $"<p>Kahjuks tühistati treening <b>{E(Name(s))}</b>.</p>" + Details(s) +
            "<p>Kui sul oli koht, läks külastus kuukaardile tagasi.</p>"));

    // ---------- abimeetodid ----------

    private static string Name(Session s) => s.TrainingType?.Name ?? "Treening";
    private static string When(Session s) => s.StartsAt.ToString("dd.MM HH:mm");

    private static string Details(Session s) => Table(
        ("Treening", Name(s)),
        ("Aeg", $"{s.StartsAt:dd.MM.yyyy HH:mm} ({s.DurationMinutes} min)"),
        ("Treener", s.Trainer?.FullName ?? ""));

    private static string Table(params (string Label, string Value)[] rows) =>
        "<table cellpadding='6' style='border-collapse:collapse;margin:12px 0'>" +
        string.Concat(rows.Select(r =>
            $"<tr><td style='color:#5b616b'>{E(r.Label)}</td><td><b>{E(r.Value)}</b></td></tr>")) +
        "</table>";

    private static string Layout(string name, string body) =>
        "<div style='font-family:Arial,sans-serif;font-size:15px;color:#14161a;max-width:560px'>" +
        "<div style='background:#14161a;color:#d4ff3a;padding:14px 18px;border-radius:12px 12px 0 0;font-weight:bold;font-size:18px'>SportClub</div>" +
        $"<div style='border:1px solid #e3e5e0;border-top:0;padding:18px;border-radius:0 0 12px 12px'><p>Tere, {E(name)}!</p>{body}" +
        "<p style='color:#9ea4ad;font-size:12px;margin-top:24px'>See on automaatne kiri, palun ära vasta sellele.</p></div></div>";

    private static string E(string? text) => WebUtility.HtmlEncode(text ?? "");
}
