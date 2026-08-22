using AgendaApp.Dtos;

namespace AgendaApp.Services
{
    public interface IServiceService
    {
        Task<List<ServiceResponseDto>> GetServicesAsync(Guid businessId);
        Task<ServiceResult<ServiceResponseDto>> CreateServiceAsync(ServiceCreateDto dto);
        Task<ServiceResult<ServiceResponseDto>> UpdateServiceAsync(Guid id, ServiceUpdateDto dto);
        Task<ServiceResult<bool>> DeactivateServiceAsync(Guid id);
    }
}
