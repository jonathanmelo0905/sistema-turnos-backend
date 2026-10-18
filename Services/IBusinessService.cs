using AgendaApp.Dtos;

namespace AgendaApp.Services
{
    public interface IBusinessService
    {
        Task<ServiceResult<BusinessResponseDto>> GetBusinessAsync(Guid businessId);
        Task<ServiceResult<BusinessResponseDto>> UpdateBusinessAsync(Guid businessId, BusinessUpdateDto dto);
        Task<ServiceResult<BusinessResponseDto>> SetVisibilityAsync(Guid businessId, bool published);
        Task<ServiceResult<BusinessSetupDto>> GetSetupAsync(Guid businessId);
    }
}
