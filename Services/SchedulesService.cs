using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public class SchedulesService : ISchedulesService
    {
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

        public async Task UpsertScheduleAsync(Guid userId, UserScheduleDto dto)
        {
            var existing = await _context.UserSchedules
                .FirstOrDefaultAsync(s => s.UserId == userId && s.DayOfWeek == dto.DayOfWeek);

            if (existing != null)
            {
                existing.IsAvailable = dto.IsAvailable;
                existing.StartTime = dto.StartTime;
                existing.EndTime = dto.EndTime;
                existing.BreakStart = dto.BreakStart;
                existing.BreakEnd = dto.BreakEnd;
            }
            else
            {
                _context.UserSchedules.Add(new UserSchedule
                {
                    ScheduleId = Guid.NewGuid(),
                    UserId = userId,
                    DayOfWeek = dto.DayOfWeek,
                    IsAvailable = dto.IsAvailable,
                    StartTime = dto.StartTime,
                    EndTime = dto.EndTime,
                    BreakStart = dto.BreakStart,
                    BreakEnd = dto.BreakEnd
                });
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Horario semanal actualizado para usuario {UserId}, día {DayOfWeek}.", userId, dto.DayOfWeek);
        }

        public async Task AddExceptionAsync(Guid userId, UserScheduleExceptionCreateDto dto)
        {
            var exception = new UserScheduleException
            {
                ExceptionId = Guid.NewGuid(),
                UserId = userId,
                Date = dto.Date,
                IsAvailable = dto.IsAvailable,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Reason = dto.Reason
            };

            _context.UserScheduleExceptions.Add(exception);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Excepción de horario {ExceptionId} agregada para usuario {UserId}.", exception.ExceptionId, userId);
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
    }
}
