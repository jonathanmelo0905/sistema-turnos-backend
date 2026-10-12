using System.Net;
using System.Net.Mail;

namespace AgendaApp.Services
{
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string html, string? fromName = null);
    }

    // Correo por SMTP (sección "Smtp" de la configuración; la contraseña en user-secrets).
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

        public async Task SendAsync(string to, string subject, string html, string? fromName = null)
        {
            var smtp = _config.GetSection("Smtp");
            var from = smtp["From"] ?? "no-responder@turnos.local";

            using var msg = new MailMessage
            {
                From = new MailAddress(from, fromName ?? "Turnos"),
                Subject = subject,
                Body = html,
                IsBodyHtml = true
            };
            msg.To.Add(to);

            using var client = new SmtpClient();
            if (string.IsNullOrWhiteSpace(smtp["Host"]))
            {
                var carpeta = Path.Combine(_env.ContentRootPath, "correos-dev");
                Directory.CreateDirectory(carpeta);
                client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
                client.PickupDirectoryLocation = carpeta;
            }
            else
            {
                client.Host = smtp["Host"]!;
                client.Port = int.TryParse(smtp["Port"], out var port) ? port : 587;
                client.EnableSsl = !bool.TryParse(smtp["EnableSsl"], out var ssl) || ssl;
                if (!string.IsNullOrWhiteSpace(smtp["User"]))
                    client.Credentials = new NetworkCredential(smtp["User"], smtp["Password"]);
            }
            await client.SendMailAsync(msg);
        }
    }
}
