using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public class BusinessScheduleService : IBusinessScheduleService
    {
        private readonly AgendaProDbContext _context;
        private readonly ILogger<BusinessScheduleService> _logger;

        public BusinessScheduleService(AgendaProDbContext context, ILogger<BusinessScheduleService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ServiceResult<BusinessScheduleResponseDto>> GetScheduleAsync(Guid businessId)
        {
            var businessExists = await _context.Businesses.AnyAsync(b => b.BusinessId == businessId);
            if (!businessExists)
                return ServiceResult<BusinessScheduleResponseDto>.NotFound("El negocio especificado no existe.");

            var days = await _context.BusinessSchedules
                .Where(bs => bs.BusinessId == businessId)
                .OrderBy(bs => bs.DayOfWeek)
                .Select(bs => new BusinessScheduleDayDto
                {
                    DayOfWeek = bs.DayOfWeek,
                    IsOpen = bs.IsOpen,
                    OpenTime = bs.OpenTime,
                    CloseTime = bs.CloseTime
                })
                .ToListAsync();

            return ServiceResult<BusinessScheduleResponseDto>.Ok(new BusinessScheduleResponseDto
            {
                BusinessId = businessId,
                Days = days
            });
        }

        public async Task<ServiceResult<BusinessScheduleResponseDto>> UpdateScheduleAsync(Guid businessId, BusinessScheduleUpdateDto dto)
        {
            var businessExists = await _context.Businesses.AnyAsync(b => b.BusinessId == businessId);
            if (!businessExists)
                return ServiceResult<BusinessScheduleResponseDto>.NotFound("El negocio especificado no existe.");

            var daysOfWeek = dto.Days.Select(d => d.DayOfWeek).ToList();
            if (daysOfWeek.Distinct().Count() != 7 || !Enumerable.Range(1, 7).All(d => daysOfWeek.Contains((byte)d)))
                return ServiceResult<BusinessScheduleResponseDto>.BadRequest("El horario debe incluir exactamente los 7 días de la semana (1 a 7), sin duplicados.");

            foreach (var day in dto.Days)
            {
                if (day.IsOpen && (string.IsNullOrWhiteSpace(day.OpenTime) || string.IsNullOrWhiteSpace(day.CloseTime)))
                    return ServiceResult<BusinessScheduleResponseDto>.BadRequest($"El día {day.DayOfWeek} está marcado como abierto pero falta OpenTime o CloseTime.");
            }

            var existing = await _context.BusinessSchedules
                .Where(bs => bs.BusinessId == businessId)
                .ToListAsync();

            _context.BusinessSchedules.RemoveRange(existing);

            var newSchedule = dto.Days.Select(d => new BusinessSchedule
            {
                ScheduleId = Guid.NewGuid(),
                BusinessId = businessId,
                DayOfWeek = d.DayOfWeek,
                IsOpen = d.IsOpen,
                OpenTime = d.IsOpen ? d.OpenTime : null,
                CloseTime = d.IsOpen ? d.CloseTime : null
            }).ToList();

            _context.BusinessSchedules.AddRange(newSchedule);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Horario semanal del negocio {BusinessId} actualizado.", businessId);

            return ServiceResult<BusinessScheduleResponseDto>.Ok(new BusinessScheduleResponseDto
            {
                BusinessId = businessId,
                Days = newSchedule
                    .OrderBy(d => d.DayOfWeek)
                    .Select(d => new BusinessScheduleDayDto
                    {
                        DayOfWeek = d.DayOfWeek,
                        IsOpen = d.IsOpen,
                        OpenTime = d.OpenTime,
                        CloseTime = d.CloseTime
                    }).ToList()
            });
        }
    }
}
