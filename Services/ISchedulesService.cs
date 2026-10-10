using AgendaApp.Dtos;

namespace AgendaApp.Services
{
    public interface ISchedulesService
    {
        Task<List<UserScheduleResponseDto>> GetWeeklyScheduleAsync(Guid userId);
        Task<ServiceResult<List<UserScheduleResponseDto>>> SaveWeekAsync(Guid userId, Guid businessId, WeeklyScheduleDto dto);
        Task UpsertScheduleAsync(Guid userId, UserScheduleDto dto);
        Task<List<UserScheduleExceptionResponseDto>> GetExceptionsAsync(Guid userId, DateTime from);
        Task<ServiceResult<UserScheduleExceptionResponseDto>> AddExceptionAsync(Guid userId, Guid businessId, UserScheduleExceptionCreateDto dto);
        Task<ServiceResult<bool>> DeleteExceptionAsync(Guid userId, Guid exceptionId);
    }
}
