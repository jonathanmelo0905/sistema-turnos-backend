using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace AgendaApp.Services
{
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string html, string? fromName = null, string? replyTo = null);
    }

    // Correo por SMTP con MailKit (sección "Smtp" de appsettings.json).
    // Sin Smtp:Host deja cada correo como .eml en "correos-dev/" para probar en local.
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public EmailService(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;
        }

        public async Task SendAsync(string to, string subject, string html, string? fromName = null, string? replyTo = null)
        {
            var smtp = _config.GetSection("Smtp");
            var from = smtp["From"] ?? "no-responder@turnos.local";

            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(fromName ?? "Turnos", from));
            msg.To.Add(MailboxAddress.Parse(to));
            // Las respuestas del cliente le llegan a la empresa, no al remitente del sistema
            if (!string.IsNullOrWhiteSpace(replyTo)) msg.ReplyTo.Add(MailboxAddress.Parse(replyTo));
            msg.Subject = subject;
            msg.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

            if (string.IsNullOrWhiteSpace(smtp["Host"]))
            {
                var carpeta = Path.Combine(_env.ContentRootPath, "correos-dev");
                Directory.CreateDirectory(carpeta);
                await msg.WriteToAsync(Path.Combine(carpeta, $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.eml"));
                return;
            }

            var port = int.TryParse(smtp["Port"], out var p) ? p : 587;
            var ssl = !bool.TryParse(smtp["EnableSsl"], out var s) || s;

            using var client = new SmtpClient();
            await client.ConnectAsync(smtp["Host"], port, ssl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
            if (!string.IsNullOrWhiteSpace(smtp["User"]))
                await client.AuthenticateAsync(smtp["User"], smtp["Password"]);
            await client.SendAsync(msg);
            await client.DisconnectAsync(true);
        }
    }
}
