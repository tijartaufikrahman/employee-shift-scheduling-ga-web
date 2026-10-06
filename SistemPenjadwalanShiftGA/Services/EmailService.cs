using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using SistemPenjadwalanShiftGA.Interfaces;

namespace SistemPenjadwalanShiftGA.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendResetPasswordEmailAsync(string toEmail, string toName, string resetLink)
        {
            try
            {
                var smtpHost = _config["Smtp:Host"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_config["Smtp:Port"] ?? "587");
                var smtpUser = _config["Smtp:Username"] ?? "";
                var smtpPass = _config["Smtp:Password"] ?? "";
                var fromEmail = _config["Smtp:FromEmail"] ?? smtpUser;
                var fromName = _config["Smtp:FromName"] ?? "Sistem Penjadwalan";

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromEmail));
                message.To.Add(new MailboxAddress(toName, toEmail));
                message.Subject = "Reset Password - Sistem Penjadwalan";

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = $@"
                    <!DOCTYPE html>
                    <html lang='id'>
                    <head>
                        <meta charset='UTF-8'>
                        <style>
                            body {{ font-family: 'Segoe UI', sans-serif; background: #eef7f5; margin: 0; padding: 20px; }}
                            .container {{ max-width: 520px; margin: 0 auto; background: #fff; border-radius: 16px; overflow: hidden; box-shadow: 0 8px 24px rgba(0,0,0,.08); }}
                            .header {{ background: linear-gradient(135deg, #2dd4bf, #0d9488); padding: 30px; text-align: center; }}
                            .header h1 {{ color: #fff; margin: 0; font-size: 22px; }}
                            .header p {{ color: rgba(255,255,255,.85); margin: 6px 0 0; font-size: 13px; }}
                            .body {{ padding: 32px; }}
                            .body p {{ color: #475569; line-height: 1.7; font-size: 14px; }}
                            .btn {{ display: inline-block; margin: 20px 0; padding: 14px 32px; background: linear-gradient(135deg, #2dd4bf, #0d9488); color: #fff !important; text-decoration: none; border-radius: 10px; font-weight: 600; font-size: 14px; }}
                            .footer {{ background: #f8fafc; padding: 16px 32px; text-align: center; color: #94a3b8; font-size: 12px; }}
                            .warning {{ background: #fef3c7; border: 1px solid #fde68a; border-radius: 8px; padding: 12px 16px; margin-top: 16px; font-size: 12.5px; color: #92400e; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1><i>&#x1F4C5;</i> Sistem Penjadwalan</h1>
                                <p>Reset Password Akun Anda</p>
                            </div>
                            <div class='body'>
                                <p>Halo <strong>{toName}</strong>,</p>
                                <p>Kami menerima permintaan untuk mereset password akun Anda di <strong>Sistem Penjadwalan</strong>. Klik tombol di bawah untuk membuat password baru:</p>
                                <div style='text-align:center'>
                                    <a href='{resetLink}' class='btn'>&#x1F511; Reset Password Sekarang</a>
                                </div>
                                <div class='warning'>
                                    &#x26A0;&#xFE0F; <strong>Perhatian:</strong> Link ini hanya berlaku selama <strong>1 jam</strong>. Jika Anda tidak meminta reset password, abaikan email ini.
                                </div>
                                <p style='margin-top:16px;'>Atau copy link berikut ke browser Anda:<br>
                                <small style='color:#0d9488;word-break:break-all;'>{resetLink}</small></p>
                            </div>
                            <div class='footer'>
                                &copy; 2026 Sistem Penjadwalan &mdash; Email otomatis, jangan dibalas.
                            </div>
                        </div>
                    </body>
                    </html>"
                };

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(smtpUser, smtpPass);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gagal mengirim email reset password ke {Email}", toEmail);
                throw;
            }
        }
    }
}
