using System.Net;
using System.Net.Mail;
using SportClub.Core.Interfaces;

namespace SportClub.Api.Infrastructure;

/// <summary>
/// E-posti seaded (appsettings.json → "Email"). Kirjad saadetakse Gmaili kaudu.
/// Gmaili aadress ja rakenduse parool pane faili appsettings.Local.json – see ei lähe GitHubi.
/// Kui User on tühi, kirju ei saadeta, need ainult logitakse.
/// </summary>
public class EmailOptions
{
    public string FromName { get; set; } = "SportClub";
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    /// <summary>Gmaili aadress, millelt kirjad saadetakse.</summary>
    public string User { get; set; } = "";
    /// <summary>Google'i rakenduse parool (16 tähte), mitte tavaline Gmaili parool.</summary>
    public string Password { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(User) && !string.IsNullOrWhiteSpace(Password);
}

/// <summary>
/// Saadab kirjad Gmaili SMTP kaudu. Kiri saadetakse taustal, nii et päring ei jää ootama,
/// ja viga kirja saatmisel ainult logitakse – broneering, ost jne õnnestuvad ikka.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(EmailOptions options, ILogger<SmtpEmailSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task SendAsync(string toEmail, string toName, string subject, string html, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            // Seadistamata: kirja ei saadeta, aga logist on näha, mis oleks läinud.
            _logger.LogInformation("E-kiri (ei saadetud, Gmail pole seadistatud) → {To}: {Subject}", toEmail, subject);
            return Task.CompletedTask;
        }

        // Taustal, et API vastus ei jääks SMTP serverit ootama.
        _ = Task.Run(async () =>
        {
            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(_options.User, _options.FromName), // Gmail lubab saata ainult enda aadressilt
                    Subject = subject,
                    Body = html,
                    IsBodyHtml = true
                };
                message.To.Add(new MailAddress(toEmail, toName));

                using var client = new SmtpClient(_options.Host, _options.Port)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(_options.User, _options.Password)
                };
                await client.SendMailAsync(message);
                _logger.LogInformation("E-kiri saadetud → {To}: {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("E-kirja saatmine ebaõnnestus → {To}: {Error}", toEmail, ex.Message);
            }
        });
        return Task.CompletedTask;
    }
}
