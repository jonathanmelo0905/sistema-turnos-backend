using AgendaApp.Dtos;

namespace AgendaApp.Services
{
    public interface IBusinessScheduleService
    {
        Task<ServiceResult<BusinessScheduleResponseDto>> GetScheduleAsync(Guid businessId);
        Task<ServiceResult<BusinessScheduleResponseDto>> UpdateScheduleAsync(Guid businessId, BusinessScheduleUpdateDto dto);
    }
}
