using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public class SchedulesService : ISchedulesService
    {
        private static readonly string[] Dias = { "", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado", "domingo" };

        private readonly AgendaProDbContext _context;
        private readonly ILogger<SchedulesService> _logger;

        public SchedulesService(AgendaProDbContext context, ILogger<SchedulesService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<UserScheduleResponseDto>> GetWeeklyScheduleAsync(Guid userId)
        {
            return await _context.UserSchedules
                .Where(s => s.UserId == userId)
                .OrderBy(s => s.DayOfWeek)
                .Select(s => new UserScheduleResponseDto
                {
                    ScheduleId = s.ScheduleId,
                    UserId = s.UserId,
                    DayOfWeek = s.DayOfWeek,
                    IsAvailable = s.IsAvailable,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    BreakStart = s.BreakStart,
                    BreakEnd = s.BreakEnd
                })
                .ToListAsync();
        }

        // Guarda la semana completa validando cada día contra el horario de la empresa.
        public async Task<ServiceResult<List<UserScheduleResponseDto>>> SaveWeekAsync(Guid userId, Guid businessId, WeeklyScheduleDto dto)
        {
            if (dto.Days.Select(d => d.DayOfWeek).Distinct().Count() != 7 || dto.Days.Any(d => d.DayOfWeek is < 1 or > 7))
                return ServiceResult<List<UserScheduleResponseDto>>.BadRequest("Se deben enviar los 7 días de la semana (1 a 7).");

            var empresa = await _context.BusinessSchedules.AsNoTracking()
                .Where(b => b.BusinessId == businessId)
                .ToDictionaryAsync(b => b.DayOfWeek);

            foreach (var d in dto.Days.Where(d => d.IsAvailable))
            {
                var dia = Dias[d.DayOfWeek];
                if (!TryTime(d.StartTime, out var ini) || !TryTime(d.EndTime, out var fin) || ini >= fin)
                    return ServiceResult<List<UserScheduleResponseDto>>.BadRequest($"El {dia}: la salida debe ser después de la entrada.");

                if (!empresa.TryGetValue(d.DayOfWeek, out var b) || !b.IsOpen
                    || !TryTime(b.OpenTime, out var abre) || !TryTime(b.CloseTime, out var cierra))
                    return ServiceResult<List<UserScheduleResponseDto>>.BadRequest($"El {dia} la empresa no atiende; desactiva ese día.");

                if (ini < abre || fin > cierra)
                    return ServiceResult<List<UserScheduleResponseDto>>.BadRequest(
                        $"El {dia} tu jornada debe estar dentro del horario de la empresa ({Fmt(abre)}–{Fmt(cierra)}).");

                var tieneInicioPausa = !string.IsNullOrWhiteSpace(d.BreakStart);
                var tieneFinPausa = !string.IsNullOrWhiteSpace(d.BreakEnd);
                if (tieneInicioPausa != tieneFinPausa)
                    return ServiceResult<List<UserScheduleResponseDto>>.BadRequest($"El {dia}: indica inicio y fin de la pausa, o ninguno.");

                if (tieneInicioPausa)
                {
                    if (!TryTime(d.BreakStart, out var p1) || !TryTime(d.BreakEnd, out var p2) || p1 >= p2)
                        return ServiceResult<List<UserScheduleResponseDto>>.BadRequest($"El {dia}: el fin de la pausa debe ser después del inicio.");
                    if (p1 <= ini || p2 >= fin)
                        return ServiceResult<List<UserScheduleResponseDto>>.BadRequest($"El {dia}: la pausa debe quedar dentro de la jornada.");
                }
            }

            var existentes = await _context.UserSchedules.Where(s => s.UserId == userId).ToListAsync();
            foreach (var d in dto.Days)
            {
                var fila = existentes.FirstOrDefault(s => s.DayOfWeek == d.DayOfWeek);
                if (fila == null)
                {
                    fila = new UserSchedule { ScheduleId = Guid.NewGuid(), UserId = userId, DayOfWeek = d.DayOfWeek };
                    _context.UserSchedules.Add(fila);
                }
                fila.IsAvailable = d.IsAvailable;
                fila.StartTime = d.IsAvailable ? Norm(d.StartTime) : null;
                fila.EndTime = d.IsAvailable ? Norm(d.EndTime) : null;
                fila.BreakStart = d.IsAvailable ? Norm(d.BreakStart) : null;
                fila.BreakEnd = d.IsAvailable ? Norm(d.BreakEnd) : null;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Horario semanal guardado para usuario {UserId}.", userId);
            return ServiceResult<List<UserScheduleResponseDto>>.Ok(await GetWeeklyScheduleAsync(userId));
        }

        // Compatibilidad: guardado de un solo día (sin validación de empresa)
        public async Task UpsertScheduleAsync(Guid userId, UserScheduleDto dto)
        {
            var existing = await _context.UserSchedules
                .FirstOrDefaultAsync(s => s.UserId == userId && s.DayOfWeek == dto.DayOfWeek);

            if (existing == null)
            {
                existing = new UserSchedule { ScheduleId = Guid.NewGuid(), UserId = userId, DayOfWeek = dto.DayOfWeek };
                _context.UserSchedules.Add(existing);
            }

            existing.IsAvailable = dto.IsAvailable;
            existing.StartTime = dto.StartTime;
            existing.EndTime = dto.EndTime;
            existing.BreakStart = dto.BreakStart;
            existing.BreakEnd = dto.BreakEnd;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Horario semanal actualizado para usuario {UserId}, día {DayOfWeek}.", userId, dto.DayOfWeek);
        }

        public async Task<List<UserScheduleExceptionResponseDto>> GetExceptionsAsync(Guid userId, DateTime from)
        {
            return await _context.UserScheduleExceptions.AsNoTracking()
                .Where(e => e.UserId == userId && e.Date >= from.Date)
                .OrderBy(e => e.Date).ThenBy(e => e.StartTime)
                .Select(e => new UserScheduleExceptionResponseDto
                {
                    ExceptionId = e.ExceptionId,
                    UserId = e.UserId,
                    Date = e.Date,
                    IsAvailable = e.IsAvailable,
                    StartTime = e.StartTime,
                    EndTime = e.EndTime,
                    Reason = e.Reason
                })
                .ToListAsync();
        }

        public async Task<ServiceResult<UserScheduleExceptionResponseDto>> AddExceptionAsync(Guid userId, Guid businessId, UserScheduleExceptionCreateDto dto)
        {
            var date = dto.Date.Date;
            if (date < DateTime.Today)
                return ServiceResult<UserScheduleExceptionResponseDto>.BadRequest("La fecha no puede estar en el pasado.");

            if (!TryTime(dto.StartTime, out var ini) || !TryTime(dto.EndTime, out var fin) || ini >= fin)
                return ServiceResult<UserScheduleExceptionResponseDto>.BadRequest("Indica un rango de horas válido.");

            if (dto.IsAvailable)
            {
                // Horas extra: siempre dentro del horario de la empresa
                var dow = (byte)(date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek);
                var b = await _context.BusinessSchedules.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.BusinessId == businessId && x.DayOfWeek == dow);
                if (b == null || !b.IsOpen || !TryTime(b.OpenTime, out var abre) || !TryTime(b.CloseTime, out var cierra))
                    return ServiceResult<UserScheduleExceptionResponseDto>.BadRequest("Ese día la empresa no atiende.");
                if (ini < abre || fin > cierra)
                    return ServiceResult<UserScheduleExceptionResponseDto>.BadRequest($"Las horas extra deben estar dentro del horario de la empresa ({Fmt(abre)}–{Fmt(cierra)}).");
            }

            var exception = new UserScheduleException
            {
                ExceptionId = Guid.NewGuid(),
                UserId = userId,
                Date = date,
                IsAvailable = dto.IsAvailable,
                StartTime = Fmt(ini),
                EndTime = Fmt(fin),
                Reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim()
            };

            _context.UserScheduleExceptions.Add(exception);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Excepción de horario {ExceptionId} agregada para usuario {UserId}.", exception.ExceptionId, userId);

            return ServiceResult<UserScheduleExceptionResponseDto>.Ok(new UserScheduleExceptionResponseDto
            {
                ExceptionId = exception.ExceptionId,
                UserId = userId,
                Date = date,
                IsAvailable = exception.IsAvailable,
                StartTime = exception.StartTime,
                EndTime = exception.EndTime,
                Reason = exception.Reason
            });
        }

        public async Task<ServiceResult<bool>> DeleteExceptionAsync(Guid userId, Guid exceptionId)
        {
            var exception = await _context.UserScheduleExceptions
                .FirstOrDefaultAsync(e => e.ExceptionId == exceptionId && e.UserId == userId);

            if (exception == null)
                return ServiceResult.NotFound("No se encontró la excepción.");

            _context.UserScheduleExceptions.Remove(exception);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Excepción de horario {ExceptionId} eliminada.", exceptionId);
            return ServiceResult.Ok();
        }

        private static bool TryTime(string? value, out TimeSpan time) =>
            TimeSpan.TryParse(value, out time) && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);

        private static string? Norm(string? value) => TryTime(value, out var t) ? Fmt(t) : null;

        private static string Fmt(TimeSpan t) => t.ToString(@"hh\:mm");
    }
}
