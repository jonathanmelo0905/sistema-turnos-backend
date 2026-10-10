using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public class AppointmentService : IAppointmentService
    {
        private const int MaxRangeDays = 62;

        private readonly AgendaProDbContext _context;
        private readonly AvailabilityService _availabilityService;
        private readonly ILogger<AppointmentService> _logger;

        public AppointmentService(AgendaProDbContext context, AvailabilityService availabilityService, ILogger<AppointmentService> logger)
        {
            _context = context;
            _availabilityService = availabilityService;
            _logger = logger;
        }

        // ---------------------------------------------------------------- Consultas

        public async Task<List<AppointmentDto>> ListAsync(Guid businessId, DateTime from, DateTime to, Guid? userId, string? status)
        {
            from = from.Date;
            to = to.Date.AddDays(1);
            if ((to - from).TotalDays > MaxRangeDays) to = from.AddDays(MaxRangeDays);

            var query = _context.Appointments.AsNoTracking()
                .Where(a => a.BusinessId == businessId && a.DateTimeStart < to && a.DateTimeEnd > from);

            if (userId.HasValue) query = query.Where(a => a.UserId == userId.Value);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == status);

            return await query.OrderBy(a => a.DateTimeStart).Select(Projection()).ToListAsync();
        }

        public async Task<AppointmentDetailDto?> GetDetailAsync(Guid businessId, Guid id)
        {
            var a = await _context.Appointments.AsNoTracking()
                .Where(x => x.AppointmentId == id && x.BusinessId == businessId)
                .Select(Projection())
                .FirstOrDefaultAsync();
            if (a == null) return null;

            var history = await _context.AppointmentHistory.AsNoTracking()
                .Where(h => h.AppointmentId == id)
                .OrderBy(h => h.At)
                .Select(h => new AppointmentHistoryDto
                {
                    Action = h.Action,
                    FromStatus = h.FromStatus,
                    ToStatus = h.ToStatus,
                    At = h.At,
                    Note = h.Note,
                    ByName = _context.Users.Where(u => u.UserId == h.ByUserId).Select(u => u.Name).FirstOrDefault()
                })
                .ToListAsync();

            var visits = await _context.Appointments.CountAsync(x =>
                x.ClientId == a.ClientId && x.Status == AppointmentStatus.Completed);

            var detail = new AppointmentDetailDto { History = history, ClientVisits = visits };
            foreach (var p in typeof(AppointmentDto).GetProperties()) p.SetValue(detail, p.GetValue(a));
            return detail;
        }

        // ---------------------------------------------------------------- Reservar

        public async Task<ServiceResult<AppointmentDto>> BookAsync(Guid businessId, Guid? byUserId, string source, AppointmentBookDto dto)
        {
            var business = await _context.Businesses.AsNoTracking().FirstOrDefaultAsync(b => b.BusinessId == businessId);
            if (business == null) return ServiceResult<AppointmentDto>.NotFound("Negocio no encontrado.");

            var service = await _context.Services.AsNoTracking()
                .FirstOrDefaultAsync(s => s.ServiceId == dto.ServiceId && s.BusinessId == businessId);
            if (service == null) return ServiceResult<AppointmentDto>.NotFound("El servicio no existe en esta empresa.");

            var staffOk = await _context.Users.AnyAsync(u => u.UserId == dto.UserId && u.BusinessId == businessId && u.Active);
            if (!staffOk) return ServiceResult<AppointmentDto>.NotFound("El colaborador no existe o está inactivo.");

            // Cliente existente o nuevo (si el teléfono ya existe en la empresa, se reutiliza)
            var clientResult = await ResolveClientAsync(businessId, dto);
            if (!clientResult.Success) return ServiceResult<AppointmentDto>.BadRequest(clientResult.Error!);
            var clientId = clientResult.Data;

            var start = dto.Start;
            var end = start.AddMinutes(service.DurationMin);

            var validacion = await _availabilityService.IsUserAvailable(dto.UserId, start, end, dto.ServiceId);
            var outOfHours = false;
            if (!validacion.Status)
            {
                if (validacion.Reason == AvailabilityReason.OutOfSchedule && business.AllowOutOfHoursBooking)
                    outOfHours = true;
                else if (validacion.Reason == AvailabilityReason.OverlapsAppointment)
                    return ServiceResult<AppointmentDto>.Conflict("El colaborador ya tiene un turno en ese horario.");
                else
                    return ServiceResult<AppointmentDto>.BadRequest(validacion.Mensaje);
            }

            // 🔒 Sección crítica: el chequeo de solapamiento y el INSERT van en la misma transacción
            // con SELECT ... FOR UPDATE, para que dos reservas simultáneas no tomen el mismo hueco.
            await using var tx = await _context.Database.BeginTransactionAsync();
            var conflict = await LockConflictsAsync(dto.UserId, clientId, start, end, null);
            if (conflict != null)
            {
                await tx.RollbackAsync();
                return ServiceResult<AppointmentDto>.Conflict(conflict);
            }

            var appointment = new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                BusinessId = businessId,
                ClientId = clientId,
                UserId = dto.UserId,
                serviceId = dto.ServiceId,
                DateTimeStart = start,
                DateTimeEnd = end,
                Price = service.Price,
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                Source = source,
                CreatedByUserId = byUserId,
                Status = outOfHours ? AppointmentStatus.PendingOutOfHours : AppointmentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _context.Appointments.Add(appointment);
            AddHistory(appointment.AppointmentId, "Created", null, appointment.Status, byUserId,
                outOfHours ? "Fuera del horario del colaborador: requiere aprobación." : null);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            _logger.LogInformation("Turno {Id} reservado ({Source}) para {UserId} a las {Start}.", appointment.AppointmentId, source, dto.UserId, start);
            return ServiceResult<AppointmentDto>.Ok((await GetDetailAsync(businessId, appointment.AppointmentId))!);
        }

        // Contrato anterior: se respeta el inicio y se recalcula el fin con el servicio
        public Task<ServiceResult<AppointmentDto>> CreateAppointmentAsync(AppointmentCreateDto dto) =>
            BookAsync(dto.BusinessId, null, "Mostrador", new AppointmentBookDto
            {
                ClientId = dto.ClientId,
                UserId = dto.UserId,
                ServiceId = dto.serviceId,
                Start = dto.DateTimeStart
            });

        // ---------------------------------------------------------------- Reprogramar / ceder

        public async Task<ServiceResult<AppointmentDto>> RescheduleAsync(Guid businessId, Guid id, Guid byUserId, AppointmentRescheduleDto dto)
        {
            var a = await _context.Appointments.FirstOrDefaultAsync(x => x.AppointmentId == id && x.BusinessId == businessId);
            if (a == null) return ServiceResult<AppointmentDto>.NotFound("Turno no encontrado.");
            if (AppointmentStatus.IsClosed(a.Status) || a.Status == AppointmentStatus.InProgress)
                return ServiceResult<AppointmentDto>.BadRequest("Este turno ya no se puede reprogramar.");

            var newUserId = dto.UserId ?? a.UserId;
            if (newUserId != a.UserId)
            {
                var ok = await _context.Users.AnyAsync(u => u.UserId == newUserId && u.BusinessId == businessId && u.Active);
                if (!ok) return ServiceResult<AppointmentDto>.NotFound("El colaborador no existe o está inactivo.");
            }

            var duration = a.DateTimeEnd - a.DateTimeStart;
            var start = dto.Start;
            var end = start + duration;

            var business = await _context.Businesses.AsNoTracking().FirstAsync(b => b.BusinessId == businessId);
            var validacion = await _availabilityService.IsUserAvailable(newUserId, start, end, a.serviceId, id);
            var outOfHours = false;
            if (!validacion.Status)
            {
                if (validacion.Reason == AvailabilityReason.OutOfSchedule && business.AllowOutOfHoursBooking)
                    outOfHours = true;
                else if (validacion.Reason == AvailabilityReason.OverlapsAppointment)
                    return ServiceResult<AppointmentDto>.Conflict("El colaborador ya tiene un turno en ese horario.");
                else
                    return ServiceResult<AppointmentDto>.BadRequest(validacion.Mensaje);
            }

            await using var tx = await _context.Database.BeginTransactionAsync();
            var conflict = await LockConflictsAsync(newUserId, a.ClientId, start, end, id);
            if (conflict != null)
            {
                await tx.RollbackAsync();
                return ServiceResult<AppointmentDto>.Conflict(conflict);
            }

            var oldStart = a.DateTimeStart;
            var oldUser = a.UserId;
            var oldStatus = a.Status;
            a.DateTimeStart = start;
            a.DateTimeEnd = end;
            a.UserId = newUserId;
            a.UpdatedAt = DateTime.UtcNow;
            // Un turno movido vuelve a quedar por confirmar con el cliente
            a.Status = outOfHours ? AppointmentStatus.PendingOutOfHours : AppointmentStatus.Pending;

            var cambio = newUserId != oldUser ? "Reassigned" : "Rescheduled";
            var nota = $"Antes: {oldStart:dd/MM/yyyy HH:mm}" + (string.IsNullOrWhiteSpace(dto.Note) ? "" : $" · {dto.Note.Trim()}");
            AddHistory(id, cambio, oldStatus, a.Status, byUserId, nota);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            _logger.LogInformation("Turno {Id} {Accion} por {By}.", id, cambio, byUserId);
            return ServiceResult<AppointmentDto>.Ok((await GetDetailAsync(businessId, id))!);
        }

        // ---------------------------------------------------------------- Estados

        public async Task<ServiceResult<AppointmentDto>> ChangeStatusAsync(Guid businessId, Guid id, Guid byUserId, AppointmentStatusDto dto)
        {
            var a = await _context.Appointments.FirstOrDefaultAsync(x => x.AppointmentId == id && x.BusinessId == businessId);
            if (a == null) return ServiceResult<AppointmentDto>.NotFound("Turno no encontrado.");

            var to = dto.Status;
            if (!AppointmentStatus.CanMove(a.Status, to))
                return ServiceResult<AppointmentDto>.BadRequest($"No se puede pasar un turno de «{a.Status}» a «{to}».");

            var now = DateTime.Now;
            if (to == AppointmentStatus.NoShow && now < a.DateTimeStart)
                return ServiceResult<AppointmentDto>.BadRequest("Solo puedes marcar «No asistió» cuando ya pasó la hora del turno.");
            if (to == AppointmentStatus.InProgress && a.DateTimeStart.Date != now.Date)
                return ServiceResult<AppointmentDto>.BadRequest("Solo puedes iniciar turnos del día de hoy.");
            if (to == AppointmentStatus.Cancelled && string.IsNullOrWhiteSpace(dto.Note))
                return ServiceResult<AppointmentDto>.BadRequest("Indica el motivo de la cancelación.");

            var from = a.Status;
            a.Status = to;
            a.UpdatedAt = DateTime.UtcNow;
            if (to == AppointmentStatus.InProgress) a.StartedAt = now;
            if (to == AppointmentStatus.Completed) a.CompletedAt = now;
            if (to == AppointmentStatus.Cancelled) a.CancelReason = dto.Note!.Trim();

            AddHistory(id, "Status", from, to, byUserId, dto.Note);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Turno {Id}: {From} → {To} por {By}.", id, from, to, byUserId);
            return ServiceResult<AppointmentDto>.Ok((await GetDetailAsync(businessId, id))!);
        }

        public async Task<ServiceResult<AppointmentDto>> ApproveOutOfHoursAsync(Guid id, Guid callerId, bool callerCanActOnBehalf)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
                return ServiceResult<AppointmentDto>.NotFound("No se encontró la cita especificada.");

            if (appointment.UserId != callerId && !callerCanActOnBehalf)
                return ServiceResult<AppointmentDto>.Unauthorized("No tienes permiso para aprobar esta cita.");

            if (appointment.Status != AppointmentStatus.PendingOutOfHours)
                return ServiceResult<AppointmentDto>.BadRequest("La cita no está pendiente de aprobación por horario.");

            appointment.Status = AppointmentStatus.Confirmed;
            AddHistory(id, "Status", AppointmentStatus.PendingOutOfHours, AppointmentStatus.Confirmed, callerId, "Aprobado fuera de horario.");
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cita {AppointmentId} aprobada (fuera de horario) por {CallerId}.", id, callerId);
            return ServiceResult<AppointmentDto>.Ok((await GetDetailAsync(appointment.BusinessId, id))!);
        }

        public async Task<ServiceResult<AppointmentDto>> RejectOutOfHoursAsync(Guid id, Guid callerId, bool callerCanActOnBehalf)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
                return ServiceResult<AppointmentDto>.NotFound("No se encontró la cita especificada.");

            if (appointment.UserId != callerId && !callerCanActOnBehalf)
                return ServiceResult<AppointmentDto>.Unauthorized("No tienes permiso para rechazar esta cita.");

            if (appointment.Status != AppointmentStatus.PendingOutOfHours)
                return ServiceResult<AppointmentDto>.BadRequest("La cita no está pendiente de aprobación por horario.");

            appointment.Status = AppointmentStatus.RejectedOutOfHours;
            AddHistory(id, "Status", AppointmentStatus.PendingOutOfHours, AppointmentStatus.RejectedOutOfHours, callerId, "Rechazado fuera de horario.");
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cita {AppointmentId} rechazada (fuera de horario) por {CallerId}.", id, callerId);
            return ServiceResult<AppointmentDto>.Ok((await GetDetailAsync(appointment.BusinessId, id))!);
        }

        // ---------------------------------------------------------------- Auxiliares

        private async Task<ServiceResult<Guid>> ResolveClientAsync(Guid businessId, AppointmentBookDto dto)
        {
            if (dto.ClientId.HasValue)
            {
                var ok = await _context.Clients.AnyAsync(c => c.ClientId == dto.ClientId && c.BusinessId == businessId);
                return ok ? ServiceResult<Guid>.Ok(dto.ClientId.Value) : ServiceResult<Guid>.BadRequest("El cliente no pertenece a esta empresa.");
            }

            if (dto.NewClient == null || string.IsNullOrWhiteSpace(dto.NewClient.Name))
                return ServiceResult<Guid>.BadRequest("Indica el cliente: elige uno existente o escribe su nombre.");
            if (string.IsNullOrWhiteSpace(dto.NewClient.Phone) && string.IsNullOrWhiteSpace(dto.NewClient.Email))
                return ServiceResult<Guid>.BadRequest("Para un cliente nuevo indica al menos su teléfono o su correo.");

            var phone = dto.NewClient.Phone?.Trim();
            var email = dto.NewClient.Email?.Trim().ToLowerInvariant();
            var existing = await _context.Clients.FirstOrDefaultAsync(c => c.BusinessId == businessId &&
                ((phone != null && phone != "" && c.Phone == phone) || (email != null && email != "" && c.Email == email)));
            if (existing != null) return ServiceResult<Guid>.Ok(existing.ClientId);

            var client = new Client
            {
                ClientId = Guid.NewGuid(),
                BusinessId = businessId,
                Name = dto.NewClient.Name.Trim(),
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                Email = string.IsNullOrWhiteSpace(email) ? null : email
            };
            _context.Clients.Add(client);
            await _context.SaveChangesAsync();
            return ServiceResult<Guid>.Ok(client.ClientId);
        }

        private async Task<string?> LockConflictsAsync(Guid userId, Guid clientId, DateTime start, DateTime end, Guid? excludeId)
        {
            var exclude = excludeId ?? Guid.Empty;
            var locked = await _context.Appointments
                .FromSqlInterpolated($@"
                    SELECT * FROM Appointments
                    WHERE Status <> 'Cancelled' AND Status <> 'RejectedOutOfHours'
                      AND AppointmentId <> {exclude}
                      AND (UserId = {userId} OR ClientId = {clientId})
                      AND DateTimeStart < {end}
                      AND DateTimeEnd > {start}
                    FOR UPDATE")
                .ToListAsync();

            if (locked.Any(a => a.UserId == userId)) return "El colaborador ya tiene un turno en ese horario.";
            if (locked.Any(a => a.ClientId == clientId)) return "El cliente ya tiene un turno en ese horario.";
            return null;
        }

        private void AddHistory(Guid appointmentId, string action, string? from, string? to, Guid? by, string? note) =>
            _context.AppointmentHistory.Add(new AppointmentHistory
            {
                HistoryId = Guid.NewGuid(),
                AppointmentId = appointmentId,
                Action = action,
                FromStatus = from,
                ToStatus = to,
                ByUserId = by,
                At = DateTime.UtcNow,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
            });

        private static System.Linq.Expressions.Expression<Func<Appointment, AppointmentDto>> Projection() => a => new AppointmentDto
        {
            AppointmentId = a.AppointmentId,
            BusinessId = a.BusinessId,
            BusinessName = a.Business.Name,
            ClientId = a.ClientId,
            ClientName = a.Client.Name,
            ClientPhone = a.Client.Phone,
            ClientEmail = a.Client.Email,
            UserId = a.UserId,
            UserName = a.User.Name,
            ServiceId = a.serviceId,
            ServiceName = a.Service.Name,
            DurationMin = MySqlDbFunctionsExtensions.DateDiffMinute(EF.Functions, a.DateTimeStart, a.DateTimeEnd),
            Price = a.Price ?? a.Service.Price,
            DateTimeStart = a.DateTimeStart,
            DateTimeEnd = a.DateTimeEnd,
            Status = a.Status,
            Notes = a.Notes,
            CancelReason = a.CancelReason,
            Source = a.Source,
            CreatedAt = a.CreatedAt,
            StartedAt = a.StartedAt,
            CompletedAt = a.CompletedAt
        };
    }
}
