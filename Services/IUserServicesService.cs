using AgendaApp.Dtos;

namespace AgendaApp.Services
{
    public interface IUserServicesService
    {
        Task<List<UserServiceResponseDto>> GetUserServicesAsync(Guid userId);
        Task<ServiceResult<bool>> AssignServicesAsync(Guid userId, AssignUserServicesDto dto);
        Task<ServiceResult<bool>> RemoveUserServiceAsync(Guid userId, Guid serviceId);
    }
}
