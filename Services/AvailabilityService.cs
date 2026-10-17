using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public class AvailabilityService
    {
        private const int MaxRangeDays = 31;
        public const int SlotStepMin = 15;

        private readonly AgendaProDbContext _context;
        private readonly ILogger<AvailabilityService> _logger;
        private readonly IValleyService _valley;

        public AvailabilityService(AgendaProDbContext context, ILogger<AvailabilityService> logger, IValleyService valley)
        {
            _context = context;
            _logger = logger;
            _valley = valley;
        }

        /// <summary>
        /// ✅ Verifica si un colaborador (usuario) está disponible en un rango de tiempo específico.
        /// </summary>
        public async Task<ResultadoService> IsUserAvailable(Guid userId, DateTime start, DateTime end, Guid serviceId, Guid? excludeAppointmentId = null)
        {
            // 🧩 0️⃣ Validar que el servicio exista
            var service = await _context.Services
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ServiceId == serviceId);

            if (service == null || !service.Active)
                return new ResultadoService { Status = false, Mensaje = "El servicio no existe o está desactivado.", Reason = AvailabilityReason.ServiceNotFound };

            if (start < DateTime.Now)
                return new ResultadoService { Status = false, Mensaje = "Ese horario ya pasó.", Reason = AvailabilityReason.InPast };

            // 🧩 0️⃣.2 Validar que el usuario ofrezca este servicio
            var userProvidesService = await _context.UserServices
                .AnyAsync(us => us.UserId == userId && us.ServiceId == serviceId);

            if (!userProvidesService)
                return new ResultadoService { Status = false, Mensaje = "el colaborador no presta este servicio, no puede agendarse", Reason = AvailabilityReason.ServiceNotProvided };

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null || !user.Active)
                return new ResultadoService { Status = false, Mensaje = "El colaborador no existe o está inactivo.", Reason = AvailabilityReason.UserNotFound };

            // Una ausencia aprobada nunca se salta, ni siquiera por la vía de flexibilidad
            if (await TimeOffCoversAsync(userId, start, end))
                return new ResultadoService { Status = false, Mensaje = "El colaborador tiene una ausencia aprobada en ese horario.", Reason = AvailabilityReason.TimeOff };

            // 🧩 1️⃣-3️⃣ Intersección empresa ∩ colaborador, menos almuerzo y excepciones (lógica compartida con GetAvailableSlotsAsync)
            var dayIntervals = await GetDayIntervalsAsync(userId, user.BusinessId, start.Date);

            if (dayIntervals.Count == 0)
                return new ResultadoService { Status = false, Mensaje = "El colaborador no tiene disponibilidad ese día (negocio cerrado, sin horario configurado, o excepción de día completo).", Reason = AvailabilityReason.OutOfSchedule };

            var fits = dayIntervals.Any(interval => start.TimeOfDay >= interval.Start && end.TimeOfDay <= interval.End);
            if (!fits)
                return new ResultadoService { Status = false, Mensaje = "El horario solicitado no está disponible (fuera de horario, cruza el almuerzo, o coincide con una excepción).", Reason = AvailabilityReason.OutOfSchedule };

            // 4️⃣ Verificar solapamiento con otras citas (excluye canceladas y turnos fuera de
            // horario rechazados — ninguno de los dos sigue ocupando el horario).
            // El margen de la empresa se exige antes y después de cada turno.
            var margen = await _context.Businesses.Where(b => b.BusinessId == user.BusinessId)
                .Select(b => b.BufferMinutes).FirstAsync();
            var ocupadoIni = start.AddMinutes(-margen);
            var ocupadoFin = end.AddMinutes(margen);
            var overlapping = await _context.Appointments.AnyAsync(a =>
                a.UserId == userId &&
                a.AppointmentId != excludeAppointmentId &&
                a.Status != "Cancelled" &&
                a.Status != "RejectedOutOfHours" &&
                a.DateTimeStart < ocupadoFin && a.DateTimeEnd > ocupadoIni
            );

            if (overlapping)
            {
                _logger.LogInformation("Disponibilidad rechazada para usuario {UserId}: solapamiento con otra cita.", userId);
                return new ResultadoService { Status = false, Mensaje = "solapamiento con otras citas", Reason = AvailabilityReason.OverlapsAppointment };
            }

            // ✅ Si pasa todas las validaciones, está disponible
            return new ResultadoService { Status = true, Mensaje = "pasa todas las validaciones, está disponible", Reason = AvailabilityReason.Available };
        }

        /// <summary>
        /// 🕓 Calcula los huecos libres de un colaborador en un rango de fechas.
        /// Si se indica serviceId, filtra los huecos por la duración de ese servicio (caso cliente agendando).
        /// Si no se indica, devuelve los huecos crudos sin filtrar por duración (caso colaborador viendo su agenda).
        /// </summary>
        /// <param name="publico">Reserva sin sesión: aplica la antelación mínima y los días hacia adelante de la empresa.</param>
        public async Task<ServiceResult<AvailabilityResponseDto>> GetAvailableSlotsAsync(Guid userId, Guid? serviceId, DateTime startDate, DateTime endDate, bool publico = false)
        {
            startDate = startDate.Date;
            endDate = endDate.Date;

            if (endDate < startDate)
                return ServiceResult<AvailabilityResponseDto>.BadRequest("La fecha de fin no puede ser anterior a la fecha de inicio.");

            if ((endDate - startDate).TotalDays > MaxRangeDays)
                return ServiceResult<AvailabilityResponseDto>.BadRequest($"El rango de fechas no puede superar los {MaxRangeDays} días.");

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null || !user.Active)
                return ServiceResult<AvailabilityResponseDto>.NotFound("El colaborador no existe o está inactivo.");

            int? durationMin = null;

            if (serviceId.HasValue)
            {
                var service = await _context.Services.AsNoTracking().FirstOrDefaultAsync(s => s.ServiceId == serviceId.Value);
                if (service == null || !service.Active)
                    return ServiceResult<AvailabilityResponseDto>.NotFound("El servicio no existe o está desactivado.");

                var userProvidesService = await _context.UserServices
                    .AnyAsync(us => us.UserId == userId && us.ServiceId == serviceId.Value);

                if (!userProvidesService)
                    return ServiceResult<AvailabilityResponseDto>.BadRequest("El colaborador no presta este servicio.");

                durationMin = service.DurationMin;
            }

            var politica = await _context.Businesses.AsNoTracking().Where(b => b.BusinessId == user.BusinessId)
                .Select(b => new { b.MinAdvanceMinutes, b.BufferMinutes, b.MaxDaysAhead }).FirstAsync();
            var margen = TimeSpan.FromMinutes(politica.BufferMinutes);
            // Primer inicio que puede tomar el cliente y último día que puede reservar
            var desde = publico ? DateTime.Now.AddMinutes(politica.MinAdvanceMinutes) : DateTime.Now;
            var ultimoDia = publico ? DateTime.Today.AddDays(politica.MaxDaysAhead - 1) : DateTime.MaxValue;

            // Horas Valle de la empresa (en caché); solo importan cuando se consulta con servicio
            var valle = durationMin.HasValue ? await _valley.GetStateAsync(user.BusinessId) : null;

            var response = new AvailabilityResponseDto
            {
                UserId = userId,
                ServiceId = serviceId,
                DurationMin = durationMin
            };

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var dayIntervals = await GetDayIntervalsAsync(userId, user.BusinessId, date);

                // Restar citas existentes de ese día
                var appointments = await _context.Appointments
                    .Where(a => a.UserId == userId
                        && !AppointmentFreeStatus.All.Contains(a.Status)
                        && a.DateTimeStart < date.AddDays(1) && a.DateTimeEnd > date)
                    .ToListAsync();

                // Cada turno ocupa también el margen antes y después
                foreach (var appt in appointments)
                {
                    var ini = appt.DateTimeStart.TimeOfDay - margen;
                    dayIntervals = SubtractInterval(dayIntervals, ini < TimeSpan.Zero ? TimeSpan.Zero : ini, appt.DateTimeEnd.TimeOfDay + margen);
                }

                // Lo que ya pasó (o no cumple la antelación mínima) no se ofrece; tampoco los días
                // más allá del límite de la empresa
                if (date < desde.Date || date > ultimoDia)
                    dayIntervals.Clear();
                else if (date == desde.Date)
                    dayIntervals = SubtractInterval(dayIntervals, TimeSpan.Zero, RoundUp(desde.TimeOfDay));

                // Filtrar por duración del servicio (si aplica)
                if (durationMin.HasValue)
                    dayIntervals = dayIntervals.Where(i => (i.End - i.Start).TotalMinutes >= durationMin.Value).ToList();

                var day = new AvailabilityDayDto
                {
                    Date = date,
                    Slots = dayIntervals
                        .Select(i => new AvailabilitySlotDto { Start = date + i.Start, End = date + i.End })
                        .ToList(),
                    TimeOff = await TimeOffLabelAsync(userId, date)
                };

                if (durationMin.HasValue)
                {
                    var dur = TimeSpan.FromMinutes(durationMin.Value);
                    foreach (var (ini, fin) in dayIntervals)
                        for (var t = RoundUp(ini); t + dur <= fin; t += TimeSpan.FromMinutes(SlotStepMin))
                            day.Starts.Add(date + t);

                    if (valle != null)
                        foreach (var s in day.Starts)
                            if (_valley.Quote(valle, s, durationMin.Value) is { Pct: > 0 } q) day.Valley.Add(q);
                }

                response.Days.Add(day);
            }

            return ServiceResult<AvailabilityResponseDto>.Ok(response);
        }

        /// <summary>
        /// Para reservar: quién presta el servicio y a qué horas puede empezar ese día.
        /// </summary>
        public async Task<ServiceResult<List<StaffAvailabilityDto>>> GetServiceAvailabilityAsync(Guid businessId, Guid serviceId, DateTime date, bool publico = false)
        {
            var service = await _context.Services.AsNoTracking()
                .FirstOrDefaultAsync(s => s.ServiceId == serviceId && s.BusinessId == businessId && s.Active);
            if (service == null)
                return ServiceResult<List<StaffAvailabilityDto>>.NotFound("El servicio no existe o está desactivado.");

            var staff = await _context.UserServices.AsNoTracking()
                .Where(us => us.ServiceId == serviceId && us.User.Active && us.User.BusinessId == businessId)
                .Select(us => new { us.UserId, us.User.Name })
                .OrderBy(u => u.Name)
                .ToListAsync();

            var result = new List<StaffAvailabilityDto>();
            foreach (var u in staff)
            {
                var day = await GetAvailableSlotsAsync(u.UserId, serviceId, date.Date, date.Date, publico);
                var d = day.Data?.Days.FirstOrDefault();
                result.Add(new StaffAvailabilityDto
                {
                    UserId = u.UserId,
                    Name = u.Name,
                    Starts = d?.Starts ?? new List<DateTime>(),
                    TimeOff = d?.TimeOff,
                    Valley = d?.Valley ?? new List<ValleyQuoteDto>()
                });
            }
            return ServiceResult<List<StaffAvailabilityDto>>.Ok(result);
        }

        /// <summary>
        /// Calcula los intervalos libres de un día: horario de empresa ∩ horario del colaborador,
        /// menos el almuerzo del colaborador, aplicando luego las excepciones puntuales de ese día.
        /// Negocio sin BusinessSchedule configurado para ese día = cerrado (fail-safe).
        /// </summary>
        private async Task<List<(TimeSpan Start, TimeSpan End)>> GetDayIntervalsAsync(Guid userId, Guid businessId, DateTime date)
        {
            var dayOfWeek = (int)date.DayOfWeek;
            if (dayOfWeek == 0) dayOfWeek = 7;

            var businessSchedule = await _context.BusinessSchedules
                .AsNoTracking()
                .FirstOrDefaultAsync(bs => bs.BusinessId == businessId && bs.DayOfWeek == dayOfWeek);

            if (businessSchedule == null || !businessSchedule.IsOpen)
                return new List<(TimeSpan, TimeSpan)>();

            if (!TimeSpan.TryParse(businessSchedule.OpenTime, out var businessOpen) ||
                !TimeSpan.TryParse(businessSchedule.CloseTime, out var businessClose) ||
                businessOpen >= businessClose)
                return new List<(TimeSpan, TimeSpan)>();

            var userSchedule = await _context.UserSchedules
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId && s.DayOfWeek == dayOfWeek && s.IsAvailable);

            if (userSchedule == null)
                return new List<(TimeSpan, TimeSpan)>();

            if (!TimeSpan.TryParse(userSchedule.StartTime, out var userStart) ||
                !TimeSpan.TryParse(userSchedule.EndTime, out var userEnd) ||
                userStart >= userEnd)
                return new List<(TimeSpan, TimeSpan)>();

            // Intersección empresa ∩ colaborador
            var baseStart = businessOpen > userStart ? businessOpen : userStart;
            var baseEnd = businessClose < userEnd ? businessClose : userEnd;

            if (baseStart >= baseEnd)
                return new List<(TimeSpan, TimeSpan)>();

            var intervals = new List<(TimeSpan Start, TimeSpan End)> { (baseStart, baseEnd) };

            // Restar almuerzo del colaborador
            if (TimeSpan.TryParse(userSchedule.BreakStart, out var breakStart) &&
                TimeSpan.TryParse(userSchedule.BreakEnd, out var breakEnd) &&
                breakStart < breakEnd)
            {
                intervals = SubtractInterval(intervals, breakStart, breakEnd);
            }

            // Excepciones puntuales de ese día
            var exceptions = await _context.UserScheduleExceptions
                .AsNoTracking()
                .Where(e => e.UserId == userId && e.Date == date)
                .ToListAsync();

            // Excepción de día completo (IsAvailable=false, sin horario) -> sin huecos ese día
            if (exceptions.Any(e => !e.IsAvailable && string.IsNullOrWhiteSpace(e.StartTime) && string.IsNullOrWhiteSpace(e.EndTime)))
                return new List<(TimeSpan, TimeSpan)>();

            foreach (var ex in exceptions)
            {
                if (!TimeSpan.TryParse(ex.StartTime, out var exStart) ||
                    !TimeSpan.TryParse(ex.EndTime, out var exEnd) ||
                    exStart >= exEnd)
                    continue;

                if (!ex.IsAvailable)
                {
                    // Bloqueo puntual: se resta
                    intervals = SubtractInterval(intervals, exStart, exEnd);
                }
                else
                {
                    // Disponibilidad extra: se suma, pero acotada al horario de la empresa (techo)
                    var addStart = exStart > businessOpen ? exStart : businessOpen;
                    var addEnd = exEnd < businessClose ? exEnd : businessClose;

                    if (addStart < addEnd)
                        intervals.Add((addStart, addEnd));
                }
            }

            // Ausencias aprobadas: día completo, nada; por horas, se resta
            var timeOffs = await _context.TimeOffRequests.AsNoTracking()
                .Where(t => t.UserId == userId && t.Status == TimeOffStatus.Approved && t.StartDate <= date && t.EndDate >= date)
                .ToListAsync();

            foreach (var t in timeOffs)
            {
                if (!t.IsPartial) return new List<(TimeSpan, TimeSpan)>();
                if (TimeSpan.TryParse(t.StartTime, out var tIni) && TimeSpan.TryParse(t.EndTime, out var tFin))
                    intervals = SubtractInterval(intervals, tIni, tFin);
            }

            return MergeIntervals(intervals);
        }

        private async Task<bool> TimeOffCoversAsync(Guid userId, DateTime start, DateTime end)
        {
            var day = start.Date;
            var timeOffs = await _context.TimeOffRequests.AsNoTracking()
                .Where(t => t.UserId == userId && t.Status == TimeOffStatus.Approved && t.StartDate <= end.Date && t.EndDate >= day)
                .ToListAsync();

            return timeOffs.Any(t =>
            {
                if (!t.IsPartial) return true;
                if (!TimeSpan.TryParse(t.StartTime, out var a) || !TimeSpan.TryParse(t.EndTime, out var b)) return true;
                return start < t.StartDate + b && end > t.StartDate + a;
            });
        }

        private async Task<string?> TimeOffLabelAsync(Guid userId, DateTime date)
        {
            var t = await _context.TimeOffRequests.AsNoTracking()
                .Where(x => x.UserId == userId && x.Status == TimeOffStatus.Approved && x.StartDate <= date && x.EndDate >= date)
                .FirstOrDefaultAsync();
            if (t == null) return null;
            return t.IsPartial ? $"{t.Type} {t.StartTime}-{t.EndTime}" : t.Type;
        }

        // Redondea hacia arriba al siguiente múltiplo de SlotStepMin (10:07 pasa a 10:15)
        private static TimeSpan RoundUp(TimeSpan t)
        {
            var min = (int)Math.Ceiling(t.TotalMinutes / SlotStepMin) * SlotStepMin;
            return TimeSpan.FromMinutes(min);
        }

        private static List<(TimeSpan Start, TimeSpan End)> SubtractInterval(
            List<(TimeSpan Start, TimeSpan End)> intervals, TimeSpan cutStart, TimeSpan cutEnd)
        {
            var result = new List<(TimeSpan Start, TimeSpan End)>();

            foreach (var (start, end) in intervals)
            {
                if (cutEnd <= start || cutStart >= end)
                {
                    // Sin cruce con el bloque a restar
                    result.Add((start, end));
                    continue;
                }

                if (cutStart > start)
                    result.Add((start, cutStart));

                if (cutEnd < end)
                    result.Add((cutEnd, end));
            }

            return result.Where(i => i.Start < i.End).ToList();
        }

        private static List<(TimeSpan Start, TimeSpan End)> MergeIntervals(List<(TimeSpan Start, TimeSpan End)> intervals)
        {
            var sorted = intervals.Where(i => i.Start < i.End).OrderBy(i => i.Start).ToList();
            var merged = new List<(TimeSpan Start, TimeSpan End)>();

            foreach (var interval in sorted)
            {
                if (merged.Count > 0 && interval.Start <= merged[^1].End)
                {
                    var last = merged[^1];
                    merged[^1] = (last.Start, interval.End > last.End ? interval.End : last.End);
                }
                else
                {
                    merged.Add(interval);
                }
            }

            return merged;
        }
    }
}
