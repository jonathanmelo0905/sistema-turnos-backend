using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public class ClientService : IClientService
    {
        private readonly AgendaProDbContext _context;
        private readonly ILogger<ClientService> _logger;

        public ClientService(AgendaProDbContext context, ILogger<ClientService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<ClientsDto>> GetClientsAsync(Guid businessId)
        {
            return await _context.Clients
                .Where(x => x.BusinessId == businessId)
                .Select(x => new ClientsDto
                {
                    ClientId = x.ClientId,
                    Name = x.Name,
                    Phone = x.Phone,
                    Email = x.Email,
                    Notes = x.Notes,
                }).ToListAsync();
        }

        public async Task<ClientsDto?> GetClientAsync(Guid clientId)
        {
            return await _context.Clients
                .Where(x => x.ClientId == clientId)
                .Select(x => new ClientsDto
                {
                    ClientId = x.ClientId,
                    Name = x.Name,
                    Phone = x.Phone,
                    Email = x.Email,
                    Notes = x.Notes,
                }).FirstOrDefaultAsync();
        }

        public async Task<ServiceResult<ClientsDto>> CreateClientAsync(CreateClientDto client)
        {
            if (client.BusinessId == Guid.Empty)
                return ServiceResult<ClientsDto>.BadRequest("El BusinessId es obligatorio.");

            var exists = await _context.Clients
                .AnyAsync(c => c.BusinessId == client.BusinessId && c.Phone == client.Phone);

            if (exists)
            {
                _logger.LogWarning("Intento de crear cliente duplicado (teléfono {Phone}) en negocio {BusinessId}.", client.Phone, client.BusinessId);
                return ServiceResult<ClientsDto>.Conflict("Ya existe un cliente con ese número de teléfono en este negocio.");
            }

            var cliente = new Client
            {
                ClientId = Guid.NewGuid(),
                BusinessId = client.BusinessId,
                Email = client.Email,
                Name = client.Name,
                Phone = client.Phone,
                Notes = client.Notes
            };

            _context.Clients.Add(cliente);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cliente {ClientId} creado en negocio {BusinessId}.", cliente.ClientId, cliente.BusinessId);

            var response = new ClientsDto
            {
                ClientId = cliente.ClientId,
                Name = cliente.Name,
                Phone = cliente.Phone,
                Email = cliente.Email,
                Notes = cliente.Notes
            };

            return ServiceResult<ClientsDto>.Ok(response);
        }

        public async Task<ServiceResult<bool>> UpdateClientAsync(Guid clientId, UpdateClientDto client)
        {
            var existingClient = await _context.Clients
                .FirstOrDefaultAsync(c => c.ClientId == clientId && c.BusinessId == client.BusinessId);

            if (existingClient == null)
                return ServiceResult.NotFound("No se encontró el cliente o no pertenece a este negocio.");

            var duplicate = await _context.Clients.AnyAsync(c =>
                c.BusinessId == client.BusinessId &&
                c.Phone == client.Phone &&
                c.ClientId != clientId);

            if (duplicate)
                return ServiceResult.Conflict("Ya existe otro cliente con ese número de teléfono en este negocio.");

            existingClient.Name = client.Name;
            existingClient.Phone = client.Phone;
            existingClient.Email = client.Email;
            existingClient.Notes = client.Notes;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Cliente {ClientId} actualizado.", clientId);
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult<bool>> DeleteClientAsync(Guid clientId, Guid businessId)
        {
            if (businessId == Guid.Empty)
                return ServiceResult.BadRequest("El parámetro 'businessId' es obligatorio.");

            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.ClientId == clientId && c.BusinessId == businessId);

            if (client == null)
                return ServiceResult.NotFound("No se encontró el cliente o no pertenece a este negocio.");

            _context.Clients.Remove(client);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cliente {ClientId} eliminado.", clientId);
            return ServiceResult.Ok();
        }
    }
}
