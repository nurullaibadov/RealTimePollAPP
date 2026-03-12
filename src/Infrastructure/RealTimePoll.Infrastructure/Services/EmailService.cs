using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using RealTimePoll.Application.DTOs.Vote;
using RealTimePoll.Application.Interfaces;

namespace RealTimePoll.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _config["Smtp:FromName"] ?? "RealTimePoll",
                _config["Smtp:FromEmail"] ?? "noreply@realtimepoll.com"
            ));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(
                _config["Smtp:Host"],
                Convert.ToInt32(_config["Smtp:Port"] ?? "587"),
                SecureSocketOptions.StartTls
            );
            await client.AuthenticateAsync(
                _config["Smtp:Username"],
                _config["Smtp:Password"]
            );
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {Email}, subject: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            throw;
        }
    }

    public async Task SendEmailConfirmationAsync(string toEmail, string userName, string confirmationLink)
    {
        var html = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; background:#f4f4f4; padding:20px;'>
  <div style='max-width:600px; margin:auto; background:#fff; border-radius:8px; padding:30px;'>
    <h2 style='color:#4F46E5;'>🗳️ RealTimePoll</h2>
    <h3>E-posta Adresinizi Doğrulayın</h3>
    <p>Merhaba <strong>{userName}</strong>,</p>
    <p>RealTimePoll'a hoş geldiniz! Hesabınızı aktif etmek için aşağıdaki butona tıklayın.</p>
    <a href='{confirmationLink}' style='display:inline-block; background:#4F46E5; color:#fff; padding:12px 24px; border-radius:6px; text-decoration:none; margin:20px 0;'>
      E-postayı Doğrula
    </a>
    <p style='color:#999; font-size:12px;'>Bu link 24 saat geçerlidir. Eğer bu işlemi siz yapmadıysanız bu e-postayı görmezden gelin.</p>
  </div>
</body>
</html>";

        await SendAsync(toEmail, userName, "E-posta Doğrulama - RealTimePoll", html);
    }

    public async Task SendPasswordResetAsync(string toEmail, string userName, string resetLink)
    {
        var html = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; background:#f4f4f4; padding:20px;'>
  <div style='max-width:600px; margin:auto; background:#fff; border-radius:8px; padding:30px;'>
    <h2 style='color:#4F46E5;'>🗳️ RealTimePoll</h2>
    <h3>Şifre Sıfırlama</h3>
    <p>Merhaba <strong>{userName}</strong>,</p>
    <p>Şifre sıfırlama talebinde bulundunuz. Aşağıdaki butona tıklayarak şifrenizi yenileyebilirsiniz.</p>
    <a href='{resetLink}' style='display:inline-block; background:#EF4444; color:#fff; padding:12px 24px; border-radius:6px; text-decoration:none; margin:20px 0;'>
      Şifremi Sıfırla
    </a>
    <p style='color:#999; font-size:12px;'>Bu link 15 dakika geçerlidir. Eğer bu isteği siz yapmadıysanız hesabınız güvende, bu e-postayı silebilirsiniz.</p>
  </div>
</body>
</html>";

        await SendAsync(toEmail, userName, "Şifre Sıfırlama - RealTimePoll", html);
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string userName)
    {
        var html = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; background:#f4f4f4; padding:20px;'>
  <div style='max-width:600px; margin:auto; background:#fff; border-radius:8px; padding:30px;'>
    <h2 style='color:#4F46E5;'>🗳️ RealTimePoll'a Hoş Geldiniz!</h2>
    <p>Merhaba <strong>{userName}</strong>,</p>
    <p>Hesabınız başarıyla oluşturuldu. Artık anketler oluşturabilir ve gerçek zamanlı sonuçları takip edebilirsiniz.</p>
    <p style='color:#6B7280;'>İyi anketler! 🎉</p>
  </div>
</body>
</html>";

        await SendAsync(toEmail, userName, "Hoş Geldiniz! - RealTimePoll", html);
    }

    public async Task SendPollResultsAsync(string toEmail, string userName, VoteResultResponse results)
    {
        var optionRows = string.Join("", results.Results.Select(r =>
            $"<tr><td>{r.OptionText}</td><td>{r.VoteCount}</td><td>%{r.Percentage:F1}</td></tr>"));

        var html = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; background:#f4f4f4; padding:20px;'>
  <div style='max-width:600px; margin:auto; background:#fff; border-radius:8px; padding:30px;'>
    <h2 style='color:#4F46E5;'>🗳️ Anket Sonuçları</h2>
    <p>Merhaba <strong>{userName}</strong>,</p>
    <p><strong>{results.PollTitle}</strong> anketinin sonuçları:</p>
    <p>Toplam Oy: <strong>{results.TotalVotes}</strong></p>
    <table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse; width:100%;'>
      <thead><tr style='background:#4F46E5; color:#fff;'><th>Seçenek</th><th>Oy</th><th>Yüzde</th></tr></thead>
      <tbody>{optionRows}</tbody>
    </table>
  </div>
</body>
</html>";

        await SendAsync(toEmail, userName, $"Anket Sonuçları: {results.PollTitle}", html);
    }
}
