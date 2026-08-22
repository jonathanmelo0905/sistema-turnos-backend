using AgendaApp.Dtos;

namespace AgendaApp.Services
{
    public interface IClientService
    {
        Task<List<ClientsDto>> GetClientsAsync(Guid businessId);
        Task<ClientsDto?> GetClientAsync(Guid clientId);
        Task<ServiceResult<ClientsDto>> CreateClientAsync(CreateClientDto dto);
        Task<ServiceResult<bool>> UpdateClientAsync(Guid clientId, UpdateClientDto dto);
        Task<ServiceResult<bool>> DeleteClientAsync(Guid clientId, Guid businessId);
    }
}
