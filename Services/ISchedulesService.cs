using AgendaApp.Dtos;

namespace AgendaApp.Services
{
    public interface ISchedulesService
    {
        Task<List<UserScheduleResponseDto>> GetWeeklyScheduleAsync(Guid userId);
        Task UpsertScheduleAsync(Guid userId, UserScheduleDto dto);
        Task AddExceptionAsync(Guid userId, UserScheduleExceptionCreateDto dto);
        Task<ServiceResult<bool>> DeleteExceptionAsync(Guid userId, Guid exceptionId);
    }
}
