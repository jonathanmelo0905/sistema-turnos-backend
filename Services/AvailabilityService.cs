using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public class AvailabilityService
    {
        private readonly AgendaProDbContext _context;
        private readonly ILogger<AvailabilityService> _logger;

        public AvailabilityService(AgendaProDbContext context, ILogger<AvailabilityService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// ✅ Verifica si un colaborador (usuario) está disponible en un rango de tiempo específico.
        /// </summary>
        public async Task<ResultadoService> IsUserAvailable(Guid userId, DateTime start, DateTime end, Guid serviceId)
        {

            // 🧩 0️⃣ Validar que el servicio exista
            var service = await _context.Services
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ServiceId == serviceId);

            if (service == null)
            {
                // ❌ Si el servicio no existe, no puede continuar
                return new ResultadoService{ Status = false, Mensaje = "El servicio no existe, no puede continuar" };
            }

            // 🧩 0️⃣.2 Validar que el usuario ofrezca este servicio
            var userProvidesService = await _context.UserServices
                .AnyAsync(us => us.UserId == userId && us.ServiceId == serviceId);

            if (!userProvidesService)
            {
                // ❌ Si el usuario no presta este servicio, no puede agendarse
                return new ResultadoService { Status = false, Mensaje = "el colaborador no presta este servicio, no puede agendarse" };
            }

            // 1️⃣ Obtener el día de la semana (1 = Lunes, 7 = Domingo)
            var dayOfWeek = (int)start.DayOfWeek;
            if (dayOfWeek == 0) dayOfWeek = 7;

            // 2️⃣ Convertir las horas actuales a formato "HH:mm"
            var startStr = start.ToString("HH:mm");
            var endStr = end.ToString("HH:mm");

            // 3️⃣ Validar que el usuario tenga horario ese día
            var schedule = await _context.UserSchedules
                .FirstOrDefaultAsync(s =>
                    s.UserId == userId &&
                    s.DayOfWeek == dayOfWeek &&
                    s.IsAvailable == true
                );

            // Si no hay horario → no disponible
            if (schedule == null)
                return new ResultadoService { Status = false, Mensaje = "el Colaborador no tiene horario disponible" }; ;

            // 🔁 Convertir las horas del horario del usuario a TimeSpan para comparar
            if (!TimeSpan.TryParse(schedule.StartTime, out var startTimeSpan)) return new ResultadoService { Status = false, Mensaje = "Problemas al validar la disponibilidad de la cita" }; ;
            if (!TimeSpan.TryParse(schedule.EndTime, out var endTimeSpan)) return new ResultadoService { Status = false, Mensaje = "Problemas al validar la disponibilidad de la cita" }; ;

            // Verificar si el rango solicitado está dentro del horario del usuario
            if (start.TimeOfDay < startTimeSpan || end.TimeOfDay > endTimeSpan)
                return new ResultadoService { Status = false, Mensaje = "El horario de la cita solicitado no está dentro del horario del Colaborador" };

            // 4️⃣ Revisar si hay excepciones (bloqueos)
            var exceptions = await _context.UserScheduleExceptions
                .Where(e => e.UserId == userId && e.Date == start.Date && e.IsAvailable == false)
                .ToListAsync();

            foreach (var ex in exceptions)
            {
                if (ex.StartTime != null && ex.EndTime != null &&
                    TimeSpan.TryParse(ex.StartTime, out var exStart) &&
                    TimeSpan.TryParse(ex.EndTime, out var exEnd))
                {
                    // Si el horario solicitado se cruza con una excepción → no disponible
                    if ((start.TimeOfDay >= exStart && start.TimeOfDay < exEnd) ||
                        (end.TimeOfDay > exStart && end.TimeOfDay <= exEnd))
                        return new ResultadoService { Status = false, Mensaje = "el horario solicitado se cruza con una excepción" };
                }
            }

            // 5️⃣ Verificar solapamiento con otras citas
            var overlapping = await _context.Appointments.AnyAsync(a =>
                a.UserId == userId &&
                a.Status != "Cancelled" &&
                (
                    (start >= a.DateTimeStart && start < a.DateTimeEnd) ||
                    (end > a.DateTimeStart && end <= a.DateTimeEnd) ||
                    (start <= a.DateTimeStart && end >= a.DateTimeEnd)
                )
            );

            if (overlapping)
            {
                _logger.LogInformation("Disponibilidad rechazada para usuario {UserId}: solapamiento con otra cita.", userId);
                return new ResultadoService { Status = false, Mensaje = "solapamiento con otras citas" };
            }

            // ✅ Si pasa todas las validaciones, está disponible
            return new ResultadoService { Status = true, Mensaje = "pasa todas las validaciones, está disponible" };
        }

        /// <summary>
        /// 🕓 Obtiene los intervalos disponibles de un colaborador en un día.
        /// </summary>
        public async Task<IEnumerable<TimeSlot>> GetAvailableSlots(Guid userId, DateTime date)
        {
            var result = new List<TimeSlot>();

            // 1️⃣ Determinar día (ajustando domingo = 7)
            var dayOfWeek = (int)date.DayOfWeek;
            if (dayOfWeek == 0) dayOfWeek = 7;

            // 2️⃣ Buscar el horario del usuario para ese día
            var schedule = await _context.UserSchedules
                .FirstOrDefaultAsync(s =>
                    s.UserId == userId &&
                    s.DayOfWeek == dayOfWeek &&
                    s.IsAvailable == true);

            if (schedule == null)
                return result;

            // 3️⃣ Convertir las horas de texto a TimeSpan
            if (!TimeSpan.TryParse(schedule.StartTime, out var startSpan)) return result;
            if (!TimeSpan.TryParse(schedule.EndTime, out var endSpan)) return result;

            var start = date.Date.Add(startSpan);
            var end = date.Date.Add(endSpan);

            // 4️⃣ Obtener citas existentes ese día
            var appointments = await _context.Appointments
                .Where(a => a.UserId == userId && a.DateTimeStart.Date == date.Date)
                .OrderBy(a => a.DateTimeStart)
                .ToListAsync();

            // 5️⃣ Calcular huecos libres entre citas
            var cursor = start;
            foreach (var appt in appointments)
            {
                if (cursor < appt.DateTimeStart)
                {
                    result.Add(new TimeSlot
                    {
                        Start = cursor,
                        End = appt.DateTimeStart
                    });
                }

                cursor = appt.DateTimeEnd;
            }

            // 6️⃣ Agregar hueco final del día si existe
            if (cursor < end)
                result.Add(new TimeSlot { Start = cursor, End = end });

            // 7️⃣ Remover huecos afectados por excepciones
            var exceptions = await _context.UserScheduleExceptions
                .Where(e => e.UserId == userId && e.Date == date.Date && e.IsAvailable == false)
                .ToListAsync();

            foreach (var ex in exceptions)
            {
                if (ex.StartTime != null && ex.EndTime != null &&
                    TimeSpan.TryParse(ex.StartTime, out var exStart) &&
                    TimeSpan.TryParse(ex.EndTime, out var exEnd))
                {
                    result.RemoveAll(slot =>
                        (slot.Start.TimeOfDay < exEnd && slot.End.TimeOfDay > exStart)
                    );
                }
            }

            // ✅ Retornar los espacios disponibles finales
            return result;
        }
    }

    /// <summary>
    /// 🧩 Representa un bloque de tiempo disponible.
    /// </summary>
    public class TimeSlot
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }
}
