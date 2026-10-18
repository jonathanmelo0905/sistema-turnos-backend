using System.Globalization;
using System.Net;
using AgendaApp.Context;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public enum TipoCorreoCita { Confirmacion, Cancelacion, Reprogramacion, Recordatorio }

    public interface ICorreoCitasService
    {
        // Envía al cliente el correo del turno con la plantilla común. Nunca lanza: si falla
        // (o el cliente no tiene correo) devuelve false y queda en el log.
        Task<bool> EnviarAsync(Guid appointmentId, TipoCorreoCita tipo, string? detalle = null);

        // Código de acceso a "Mis turnos" con la misma cabecera. Nunca lanza.
        Task<bool> EnviarCodigoAccesoAsync(Business b, Client client, string codigo, int minutos);
    }

    // Correos al cliente: remitente del sistema con el nombre de la empresa y Reply-To al correo de la empresa
    public class CorreoCitasService : ICorreoCitasService
    {
        private static readonly CultureInfo Es = new("es-CO");

        private readonly AgendaProDbContext _context;
        private readonly IEmailService _email;
        private readonly IConfiguration _config;
        private readonly ILogger<CorreoCitasService> _logger;

        public CorreoCitasService(AgendaProDbContext context, IEmailService email, IConfiguration config, ILogger<CorreoCitasService> logger)
        {
            _context = context;
            _email = email;
            _config = config;
            _logger = logger;
        }

        public async Task<bool> EnviarAsync(Guid appointmentId, TipoCorreoCita tipo, string? detalle = null)
        {
            try
            {
                var a = await _context.Appointments.AsNoTracking()
                    .Include(x => x.Client).Include(x => x.Service).Include(x => x.User).Include(x => x.Business)
                    .FirstOrDefaultAsync(x => x.AppointmentId == appointmentId);
                if (a == null || string.IsNullOrWhiteSpace(a.Client.Email)) return false;

                var (asunto, titulo, texto) = Textos(tipo, a);
                var html = Plantilla(a, titulo, texto, detalle, mostrarBoton: tipo != TipoCorreoCita.Cancelacion);
                await _email.SendAsync(a.Client.Email, asunto, html, a.Business.Name, a.Business.Email);
                _logger.LogInformation("Correo {Tipo} del turno {Id} enviado a {Email}.", tipo, appointmentId, a.Client.Email);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar el correo {Tipo} del turno {Id}.", tipo, appointmentId);
                return false;
            }
        }

        public async Task<bool> EnviarCodigoAccesoAsync(Business b, Client client, string codigo, int minutos)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(client.Email)) return false;
                static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
                var nombre = (client.Name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                var html = $"""
                    <div style="background:#f3f4f6;padding:24px 12px;font-family:Segoe UI,Arial,sans-serif;color:#1f2937">
                      <div style="max-width:520px;margin:auto;background:#fff;border-radius:12px;overflow:hidden;border:1px solid #e5e7eb">
                        {Cabecera(b)}
                        <div style="padding:24px">
                          <h2 style="margin:0 0 12px;color:#4f46e5;font-size:20px">Tu código para entrar</h2>
                          <p style="margin:0 0 8px">Hola {H(nombre)},</p>
                          <p style="margin:0 0 16px">Usa este código para ver y gestionar tus turnos en {H(b.Name)}. Vence en {minutos} minutos.</p>
                          <p style="margin:0 0 16px;text-align:center;font-size:32px;font-weight:700;letter-spacing:8px;color:#312e81">{codigo}</p>
                          <p style="margin:0;color:#6b7280;font-size:13px">Si no pediste este código, ignora este correo: nadie puede entrar sin él.</p>
                        </div>
                      </div>
                    </div>
                    """;
                await _email.SendAsync(client.Email, $"{codigo} es tu código de {b.Name}", html, b.Name, b.Email);
                _logger.LogInformation("Código de acceso enviado al cliente {Id}.", client.ClientId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar el código de acceso al cliente {Id}.", client.ClientId);
                return false;
            }
        }

        // Franja superior con el logo y el nombre de la empresa
        private string Cabecera(Business b)
        {
            static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
            var apiUrl = (_config["App:ApiUrl"] ?? "https://localhost:7122").TrimEnd('/');
            var logo = string.IsNullOrWhiteSpace(b.LogoUrl) ? null
                : b.LogoUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? b.LogoUrl : apiUrl + b.LogoUrl;
            return $"""
                <div style="background:#4f46e5;padding:18px 24px;color:#fff">
                  {(logo == null ? "" : $"""<img src="{H(logo)}" alt="" width="44" height="44" style="border-radius:8px;vertical-align:middle;margin-right:10px;background:#fff">""")}
                  <span style="font-size:18px;font-weight:600;vertical-align:middle">{H(b.Name)}</span>
                </div>
                """;
        }

        private static (string Asunto, string Titulo, string Texto) Textos(TipoCorreoCita tipo, Appointment a)
        {
            var empresa = a.Business.Name;
            return tipo switch
            {
                TipoCorreoCita.Confirmacion => ($"Tu turno en {empresa} quedó reservado", "¡Tu turno quedó reservado!",
                    a.Status == AppointmentStatus.PendingOutOfHours
                        ? "Recibimos tu reserva. Como es fuera del horario habitual, la empresa debe aprobarla; te avisaremos."
                        : "Estos son los datos de tu turno:"),
                TipoCorreoCita.Cancelacion => ($"Tu turno en {empresa} fue cancelado", "Tu turno fue cancelado",
                    "Se canceló este turno. Si quieres, puedes reservar otro cuando gustes."),
                TipoCorreoCita.Reprogramacion => ($"Tu turno en {empresa} cambió", "Tu turno cambió",
                    "Actualizamos tu turno. Estos son los nuevos datos:"),
                _ => ($"Recordatorio: tu turno en {empresa}", "Te recordamos tu turno",
                    "Si no puedes asistir, avísanos con tiempo.")
            };
        }

        private string Plantilla(Appointment a, string titulo, string texto, string? detalle, bool mostrarBoton)
        {
            static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
            var b = a.Business;
            var nombre = (a.Client.Name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            var fecha = a.DateTimeStart.ToString("dddd d 'de' MMMM 'de' yyyy", Es);
            var hora = a.DateTimeStart.ToString("h:mm tt", Es);

            var clienteUrl = (_config["App:ClienteUrl"] ?? "http://localhost:4400").TrimEnd('/');
            var enlace = string.IsNullOrWhiteSpace(b.Slug) ? null : $"{clienteUrl}/{b.Slug}/mis-turnos?turno={a.AppointmentId}";

            string Fila(string etiqueta, string? valor) => string.IsNullOrWhiteSpace(valor) ? "" :
                $"""<tr><td style="padding:6px 0;color:#6b7280;width:110px">{etiqueta}</td><td style="padding:6px 0;font-weight:600">{H(valor)}</td></tr>""";

            var precio = a.Price.HasValue ? a.Price.Value.ToString("C0", Es) : null;
            var contacto = string.Join(" · ", new[] { b.Address, b.Phone, b.Email }.Where(x => !string.IsNullOrWhiteSpace(x)));

            return $"""
                <div style="background:#f3f4f6;padding:24px 12px;font-family:Segoe UI,Arial,sans-serif;color:#1f2937">
                  <div style="max-width:520px;margin:auto;background:#fff;border-radius:12px;overflow:hidden;border:1px solid #e5e7eb">
                    {Cabecera(b)}
                    <div style="padding:24px">
                      <h2 style="margin:0 0 12px;color:#4f46e5;font-size:20px">{H(titulo)}</h2>
                      <p style="margin:0 0 8px">Hola {H(nombre)},</p>
                      <p style="margin:0 0 16px">{H(texto)}</p>
                      <table style="width:100%;border-collapse:collapse;font-size:14px;border-top:1px solid #e5e7eb;border-bottom:1px solid #e5e7eb">
                        {Fila("Servicio", a.Service.Name)}
                        {Fila("Con", a.User.Name)}
                        {Fila("Fecha", fecha)}
                        {Fila("Hora", hora)}
                        {Fila("Precio", precio)}
                        {Fila("Dirección", b.Address)}
                        {Fila("Código", a.AppointmentId.ToString("N")[..8].ToUpperInvariant())}
                      </table>
                      {(string.IsNullOrWhiteSpace(detalle) ? "" : $"""<p style="margin:16px 0 0;color:#374151">{H(detalle)}</p>""")}
                      {(mostrarBoton && enlace != null ? $"""<p style="margin:24px 0 0;text-align:center"><a href="{H(enlace)}" style="display:inline-block;background:#4f46e5;color:#fff;padding:12px 22px;border-radius:8px;text-decoration:none;font-weight:600">Gestionar mi cita</a></p>""" : "")}
                    </div>
                    <div style="padding:16px 24px;background:#f9fafb;color:#6b7280;font-size:12px;line-height:1.5">
                      {(contacto == "" ? "" : $"{H(b.Name)} · {H(contacto)}<br>")}
                      Recibes este correo porque reservaste un turno en {H(b.Name)}. Si respondes, tu mensaje le llega a la empresa.
                      Tus datos se usan solo para gestionar tus citas.
                    </div>
                  </div>
                </div>
                """;
        }
    }
}
