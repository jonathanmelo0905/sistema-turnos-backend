using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public interface IReminderService
    {
        // Avisa en la campanita a cada colaborador por sus turnos de mañana sin recordar. Devuelve cuántos avisos creó.
        Task<int> GenerarAsync(CancellationToken ct = default);

        // Registra el recordatorio (WhatsApp: ya lo abrió el colaborador; Email: lo envía aquí)
        Task<ServiceResult<AppointmentDto>> RecordarAsync(Guid businessId, Guid id, Guid byUserId, string channel);
    }

    public static class ReminderChannels
    {
        public const string WhatsApp = "WhatsApp";
        public const string Email = "Email";
    }

    public class ReminderService : IReminderService
    {
        private static readonly string[] Activos = { AppointmentStatus.Pending, AppointmentStatus.Confirmed };

        private readonly AgendaProDbContext _context;
        private readonly INotificationService _notifications;
        private readonly IAppointmentService _appointments;
        private readonly ICorreoCitasService _correos;

        public ReminderService(AgendaProDbContext context, INotificationService notifications,
            IAppointmentService appointments, ICorreoCitasService correos)
        {
            _context = context;
            _notifications = notifications;
            _appointments = appointments;
            _correos = correos;
        }

        public async Task<int> GenerarAsync(CancellationToken ct = default)
        {
            var manana = DateTime.Today.AddDays(1);
            var pasado = manana.AddDays(1);

            // Sin aviso previo de tipo Reminder para el turno: correr el job varias veces no duplica
            var turnos = await _context.Appointments.AsNoTracking()
                .Where(a => a.DateTimeStart >= manana && a.DateTimeStart < pasado
                    && Activos.Contains(a.Status) && a.ReminderSentAt == null
                    && !_context.StaffNotifications.Any(n => n.AppointmentId == a.AppointmentId && n.Type == NotificationTypes.Reminder))
                .Select(a => new { a.AppointmentId, a.BusinessId, a.UserId, a.DateTimeStart, Cliente = a.Client.Name, Servicio = a.Service.Name })
                .ToListAsync(ct);

            foreach (var t in turnos)
            {
                await _notifications.NotifyAsync(t.BusinessId, new[] { t.UserId }, NotificationTypes.Reminder,
                    "Recordar turno de mañana",
                    $"{t.Cliente} · {t.Servicio} a las {t.DateTimeStart:HH:mm}. Avísale por WhatsApp o correo.",
                    "inicio", t.AppointmentId);
            }
            if (turnos.Count > 0) await _context.SaveChangesAsync(ct);
            return turnos.Count;
        }

        public async Task<ServiceResult<AppointmentDto>> RecordarAsync(Guid businessId, Guid id, Guid byUserId, string channel)
        {
            var canal = channel?.Trim();
            if (canal != ReminderChannels.WhatsApp && canal != ReminderChannels.Email)
                return ServiceResult<AppointmentDto>.BadRequest("Canal inválido: usa WhatsApp o Email.");

            var a = await _context.Appointments
                .Include(x => x.Client).Include(x => x.Service).Include(x => x.User).Include(x => x.Business)
                .FirstOrDefaultAsync(x => x.AppointmentId == id && x.BusinessId == businessId);
            if (a == null) return ServiceResult<AppointmentDto>.NotFound("Turno no encontrado.");
            if (!Activos.Contains(a.Status))
                return ServiceResult<AppointmentDto>.BadRequest("Solo se recuerdan turnos pendientes o confirmados.");

            if (canal == ReminderChannels.Email)
            {
                if (string.IsNullOrWhiteSpace(a.Client.Email))
                    return ServiceResult<AppointmentDto>.BadRequest("El cliente no tiene correo registrado.");
                if (!await _correos.EnviarAsync(a.AppointmentId, TipoCorreoCita.Recordatorio))
                    return ServiceResult<AppointmentDto>.BadRequest("No se pudo enviar el correo. Inténtalo de nuevo o avísale por WhatsApp.");
            }

            a.ReminderSentAt = DateTime.UtcNow;
            a.ReminderChannel = canal;
            _context.AppointmentHistory.Add(new AppointmentHistory
            {
                HistoryId = Guid.NewGuid(),
                AppointmentId = id,
                Action = "Reminder",
                FromStatus = a.Status,
                ToStatus = a.Status,
                ByUserId = byUserId,
                At = DateTime.UtcNow,
                Note = canal == ReminderChannels.Email ? "Recordatorio enviado por correo." : "Recordatorio enviado por WhatsApp."
            });

            // El aviso de la campanita deja de tener sentido
            var avisos = await _context.StaffNotifications
                .Where(n => n.AppointmentId == id && n.Type == NotificationTypes.Reminder && n.ReadAt == null).ToListAsync();
            foreach (var n in avisos) n.ReadAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return ServiceResult<AppointmentDto>.Ok((await _appointments.GetDetailAsync(businessId, id))!);
        }
    }

    // Revisa cada 30 min si hay turnos de mañana por recordar
    public class ReminderJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<ReminderJob> _log;

        public ReminderJob(IServiceScopeFactory scopes, ILogger<ReminderJob> log)
        {
            _scopes = scopes;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
            do
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var n = await scope.ServiceProvider.GetRequiredService<IReminderService>().GenerarAsync(stoppingToken);
                    if (n > 0) _log.LogInformation("Recordatorios: {N} avisos creados.", n);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _log.LogError(ex, "Falló la generación de recordatorios."); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
