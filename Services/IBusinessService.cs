using AgendaApp.Dtos;

namespace AgendaApp.Services
{
    public interface IBusinessService
    {
        Task<ServiceResult<BusinessResponseDto>> GetBusinessAsync(Guid businessId);
    }
}
