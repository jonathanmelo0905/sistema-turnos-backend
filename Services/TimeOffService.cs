using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public interface ITimeOffService
    {
        Task<List<TimeOffResponseDto>> ListAsync(Guid businessId, Guid? userId, string? status, DateTime? from, DateTime? to);
        Task<TimeOffRequest?> FindAsync(Guid timeOffId);
        Task<List<TimeOffConflictDto>> ConflictsAsync(Guid userId, DateTime startDate, DateTime endDate, string? startTime, string? endTime);
        Task<ServiceResult<TimeOffResponseDto>> CreateAsync(Guid businessId, Guid userId, Guid createdBy, TimeOffCreateDto dto, bool autoApprove);
        Task<ServiceResult<TimeOffResponseDto>> ReviewAsync(Guid timeOffId, Guid reviewer, bool approve, string? note);
        Task<ServiceResult<TimeOffResponseDto>> CancelAsync(Guid timeOffId, Guid by);
    }

    public class TimeOffService : ITimeOffService
    {
        private const int MaxDays = 60;

        private readonly AgendaProDbContext _context;
        private readonly ILogger<TimeOffService> _logger;
        private readonly INotificationService _notify;
        private readonly IAppointmentService _appointments;

        public TimeOffService(AgendaProDbContext context, ILogger<TimeOffService> logger, INotificationService notify, IAppointmentService appointments)
        {
            _context = context;
            _logger = logger;
            _notify = notify;
            _appointments = appointments;
        }

        public async Task<List<TimeOffResponseDto>> ListAsync(Guid businessId, Guid? userId, string? status, DateTime? from, DateTime? to)
        {
            var query = _context.TimeOffRequests.AsNoTracking().Include(t => t.User)
                .Where(t => t.BusinessId == businessId);

            if (userId.HasValue) query = query.Where(t => t.UserId == userId.Value);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);
            if (from.HasValue) query = query.Where(t => t.EndDate >= from.Value.Date);
            if (to.HasValue) query = query.Where(t => t.StartDate <= to.Value.Date);

            var items = await query.OrderByDescending(t => t.StartDate).ThenByDescending(t => t.CreatedAt).Take(300).ToListAsync();

            var reviewerIds = items.Where(i => i.ReviewedByUserId.HasValue).Select(i => i.ReviewedByUserId!.Value).Distinct().ToList();
            var reviewers = await _context.Users.Where(u => reviewerIds.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => u.Name);

            var result = new List<TimeOffResponseDto>();
            foreach (var t in items)
            {
                // Los conflictos solo importan mientras la solicitud está pendiente
                var conflicts = t.Status == TimeOffStatus.Pending
                    ? (await ConflictsAsync(t.UserId, t.StartDate, t.EndDate, t.StartTime, t.EndTime)).Count
                    : 0;
                result.Add(ToDto(t, t.User.Name, t.ReviewedByUserId.HasValue ? reviewers.GetValueOrDefault(t.ReviewedByUserId.Value) : null, conflicts));
            }
            return result;
        }

        public Task<TimeOffRequest?> FindAsync(Guid timeOffId) =>
            _context.TimeOffRequests.FirstOrDefaultAsync(t => t.TimeOffId == timeOffId);

        // Turnos vigentes del colaborador que caen dentro de la ausencia
        public async Task<List<TimeOffConflictDto>> ConflictsAsync(Guid userId, DateTime startDate, DateTime endDate, string? startTime, string? endTime)
        {
            var (from, to) = Window(startDate, endDate, startTime, endTime);

            return await _context.Appointments.AsNoTracking()
                .Where(a => a.UserId == userId
                    && !AppointmentFreeStatus.All.Contains(a.Status)
                    && a.DateTimeStart < to && a.DateTimeEnd > from)
                .OrderBy(a => a.DateTimeStart)
                .Select(a => new TimeOffConflictDto
                {
                    AppointmentId = a.AppointmentId,
                    Start = a.DateTimeStart,
                    End = a.DateTimeEnd,
                    Status = a.Status,
                    ClientName = a.Client.Name,
                    ClientPhone = a.Client.Phone,
                    ServiceName = a.Service.Name
                })
                .ToListAsync();
        }

        public async Task<ServiceResult<TimeOffResponseDto>> CreateAsync(Guid businessId, Guid userId, Guid createdBy, TimeOffCreateDto dto, bool autoApprove)
        {
            var error = Validate(dto);
            if (error != null) return ServiceResult<TimeOffResponseDto>.BadRequest(error);

            var start = dto.StartDate.Date;
            var end = dto.EndDate.Date;
            var partial = !string.IsNullOrWhiteSpace(dto.StartTime);

            var overlaps = await _context.TimeOffRequests.AnyAsync(t =>
                t.UserId == userId
                && (t.Status == TimeOffStatus.Pending || t.Status == TimeOffStatus.Approved)
                && t.StartDate <= end && t.EndDate >= start);
            if (overlaps)
                return ServiceResult<TimeOffResponseDto>.Conflict("Ya hay una ausencia pendiente o aprobada que se cruza con esas fechas.");

            var conflicts = await ConflictsAsync(userId, start, end, dto.StartTime, dto.EndTime);
            var request = new TimeOffRequest
            {
                TimeOffId = Guid.NewGuid(),
                BusinessId = businessId,
                UserId = userId,
                Type = dto.Type,
                StartDate = start,
                EndDate = end,
                StartTime = partial ? Normalize(dto.StartTime) : null,
                EndTime = partial ? Normalize(dto.EndTime) : null,
                Reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = createdBy,
                Status = TimeOffStatus.Pending
            };

            // Quien puede aprobar registra la ausencia ya aprobada, salvo que haya turnos por resolver
            if (autoApprove && conflicts.Count == 0)
            {
                request.Status = TimeOffStatus.Approved;
                request.ReviewedByUserId = createdBy;
                request.ReviewedAt = DateTime.UtcNow;
            }

            _context.TimeOffRequests.Add(request);

            var persona = await _context.Users.Where(u => u.UserId == userId).Select(u => u.Name).FirstAsync();
            var rango = start == end ? $"{start:dddd d 'de' MMMM}" : $"{start:d MMM} al {end:d MMM}";
            if (conflicts.Count > 0)
                await _appointments.FlagForTimeOffAsync(conflicts.Select(c => c.AppointmentId),
                    $"{persona} pidió ausencia ({request.Type}): reprograma, cede o cancela y avísale al cliente.");
            if (request.Status == TimeOffStatus.Pending)
                await _notify.NotifyAsync(businessId, await _notify.WithPermissionAsync(businessId, PermissionCodes.FlexibilityApprove),
                    NotificationTypes.TimeOffRequested, $"{persona} pide {request.Type.ToLower()}",
                    rango + (conflicts.Count > 0 ? $" · {conflicts.Count} turno(s) por resolver" : ""), "ausencias", null, createdBy);

            await _context.SaveChangesAsync();
            _logger.LogInformation("Ausencia {TimeOffId} ({Status}) creada para {UserId} por {By}.", request.TimeOffId, request.Status, userId, createdBy);

            return ServiceResult<TimeOffResponseDto>.Ok(await DtoAsync(request, conflicts.Count));
        }

        public async Task<ServiceResult<TimeOffResponseDto>> ReviewAsync(Guid timeOffId, Guid reviewer, bool approve, string? note)
        {
            var t = await _context.TimeOffRequests.FirstOrDefaultAsync(x => x.TimeOffId == timeOffId);
            if (t == null) return ServiceResult<TimeOffResponseDto>.NotFound("Solicitud no encontrada.");
            if (t.Status != TimeOffStatus.Pending)
                return ServiceResult<TimeOffResponseDto>.BadRequest("La solicitud ya fue revisada o cancelada.");

            if (approve)
            {
                var conflicts = await ConflictsAsync(t.UserId, t.StartDate, t.EndDate, t.StartTime, t.EndTime);
                if (conflicts.Count > 0)
                    return ServiceResult<TimeOffResponseDto>.Conflict(
                        $"No se puede aprobar: hay {conflicts.Count} turno(s) agendado(s) en esas fechas. Reprográmalos, cédelos a otra persona o cancélalos primero.");
            }

            t.Status = approve ? TimeOffStatus.Approved : TimeOffStatus.Rejected;
            t.ReviewedByUserId = reviewer;
            t.ReviewedAt = DateTime.UtcNow;
            t.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            var rangoR = t.StartDate == t.EndDate ? $"{t.StartDate:dddd d 'de' MMMM}" : $"{t.StartDate:d MMM} al {t.EndDate:d MMM}";
            if (!approve) await UnflagAsync(t);
            await _notify.NotifyAsync(t.BusinessId, new[] { t.UserId }, NotificationTypes.TimeOffReviewed,
                approve ? $"Te aprobaron {t.Type.ToLower()}" : $"Rechazaron tu solicitud de {t.Type.ToLower()}",
                rangoR + (string.IsNullOrWhiteSpace(t.ReviewNote) ? "" : $" · «{t.ReviewNote}»"), "agenda-laboral?tab=ausencias", null, reviewer);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Ausencia {TimeOffId} {Status} por {Reviewer}.", timeOffId, t.Status, reviewer);
            return ServiceResult<TimeOffResponseDto>.Ok(await DtoAsync(t, 0));
        }

        public async Task<ServiceResult<TimeOffResponseDto>> CancelAsync(Guid timeOffId, Guid by)
        {
            var t = await _context.TimeOffRequests.FirstOrDefaultAsync(x => x.TimeOffId == timeOffId);
            if (t == null) return ServiceResult<TimeOffResponseDto>.NotFound("Solicitud no encontrada.");
            if (t.Status is TimeOffStatus.Rejected or TimeOffStatus.Cancelled)
                return ServiceResult<TimeOffResponseDto>.BadRequest("La solicitud ya no está vigente.");
            if (t.EndDate < DateTime.Today)
                return ServiceResult<TimeOffResponseDto>.BadRequest("No se puede cancelar una ausencia que ya pasó.");

            t.Status = TimeOffStatus.Cancelled;
            t.ReviewedByUserId ??= by;
            t.ReviewedAt ??= DateTime.UtcNow;
            await UnflagAsync(t);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Ausencia {TimeOffId} cancelada por {By}.", timeOffId, by);
            return ServiceResult<TimeOffResponseDto>.Ok(await DtoAsync(t, 0));
        }

        // Si la ausencia no sigue adelante, sus turnos ya no necesitan aviso
        private async Task UnflagAsync(TimeOffRequest t)
        {
            var (from, to) = Window(t.StartDate, t.EndDate, t.StartTime, t.EndTime);
            var lista = await _context.Appointments
                .Where(a => a.UserId == t.UserId && a.NeedsContact && a.ContactReason != null && a.ContactReason.Contains("pidió ausencia")
                    && a.DateTimeStart < to && a.DateTimeEnd > from)
                .ToListAsync();
            lista.ForEach(a => { a.NeedsContact = false; a.ContactReason = null; });
        }

        private static string? Validate(TimeOffCreateDto dto)
        {
            if (!TimeOffTypes.All.Contains(dto.Type))
                return $"Tipo inválido. Valores permitidos: {string.Join(", ", TimeOffTypes.All)}.";

            var start = dto.StartDate.Date;
            var end = dto.EndDate.Date;
            if (end < start) return "La fecha final no puede ser anterior a la inicial.";
            if (start < DateTime.Today) return "No se pueden registrar ausencias en fechas pasadas.";
            if ((end - start).TotalDays + 1 > MaxDays) return $"Una ausencia no puede superar {MaxDays} días.";

            var hasStart = !string.IsNullOrWhiteSpace(dto.StartTime);
            var hasEnd = !string.IsNullOrWhiteSpace(dto.EndTime);
            if (hasStart != hasEnd) return "Indica la hora de inicio y la de fin, o ninguna para el día completo.";
            if (hasStart)
            {
                if (start != end) return "Las ausencias por horas deben ser de un solo día.";
                if (!TimeSpan.TryParse(dto.StartTime, out var s) || !TimeSpan.TryParse(dto.EndTime, out var e) || s >= e)
                    return "La hora de fin debe ser posterior a la de inicio.";
            }
            return null;
        }

        private static (DateTime From, DateTime To) Window(DateTime startDate, DateTime endDate, string? startTime, string? endTime)
        {
            if (TimeSpan.TryParse(startTime, out var s) && TimeSpan.TryParse(endTime, out var e))
                return (startDate.Date + s, startDate.Date + e);
            return (startDate.Date, endDate.Date.AddDays(1));
        }

        private static string? Normalize(string? hhmm) =>
            TimeSpan.TryParse(hhmm, out var t) ? t.ToString(@"hh\:mm") : null;

        private async Task<TimeOffResponseDto> DtoAsync(TimeOffRequest t, int conflicts)
        {
            var userName = await _context.Users.Where(u => u.UserId == t.UserId).Select(u => u.Name).FirstAsync();
            var reviewer = t.ReviewedByUserId.HasValue
                ? await _context.Users.Where(u => u.UserId == t.ReviewedByUserId).Select(u => u.Name).FirstOrDefaultAsync()
                : null;
            return ToDto(t, userName, reviewer, conflicts);
        }

        private static TimeOffResponseDto ToDto(TimeOffRequest t, string userName, string? reviewer, int conflicts) => new()
        {
            TimeOffId = t.TimeOffId,
            UserId = t.UserId,
            UserName = userName,
            Type = t.Type,
            StartDate = t.StartDate,
            EndDate = t.EndDate,
            StartTime = t.StartTime,
            EndTime = t.EndTime,
            Reason = t.Reason,
            Status = t.Status,
            CreatedAt = t.CreatedAt,
            CreatedByUserId = t.CreatedByUserId,
            ReviewedByName = reviewer,
            ReviewedAt = t.ReviewedAt,
            ReviewNote = t.ReviewNote,
            ConflictCount = conflicts
        };
    }
}
