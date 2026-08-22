using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;
using ServiceEntity = AgendaApp.Context.Service;

namespace AgendaApp.Services
{
    public class ServiceService : IServiceService
    {
        private readonly AgendaProDbContext _context;
        private readonly ILogger<ServiceService> _logger;

        public ServiceService(AgendaProDbContext context, ILogger<ServiceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<ServiceResponseDto>> GetServicesAsync(Guid businessId)
        {
            return await _context.Services
                .Where(s => s.BusinessId == businessId)
                .Select(s => new ServiceResponseDto
                {
                    ServiceId = s.ServiceId,
                    BusinessId = s.BusinessId,
                    Name = s.Name,
                    Description = s.Description,
                    DurationMin = s.DurationMin,
                    Price = s.Price,
                    Active = s.Active
                })
                .ToListAsync();
        }

        public async Task<ServiceResult<ServiceResponseDto>> CreateServiceAsync(ServiceCreateDto dto)
        {
            var businessExists = await _context.Businesses.AnyAsync(b => b.BusinessId == dto.BusinessId);
            if (!businessExists)
                return ServiceResult<ServiceResponseDto>.NotFound("El negocio especificado no existe.");

            var service = new ServiceEntity
            {
                ServiceId = Guid.NewGuid(),
                BusinessId = dto.BusinessId,
                Name = dto.Name,
                Description = dto.Description,
                DurationMin = dto.DurationMin,
                Price = dto.Price,
                Active = true
            };

            _context.Services.Add(service);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Servicio {ServiceId} creado para negocio {BusinessId}.", service.ServiceId, service.BusinessId);

            return ServiceResult<ServiceResponseDto>.Ok(new ServiceResponseDto
            {
                ServiceId = service.ServiceId,
                BusinessId = service.BusinessId,
                Name = service.Name,
                Description = service.Description,
                DurationMin = service.DurationMin,
                Price = service.Price,
                Active = service.Active
            });
        }

        public async Task<ServiceResult<ServiceResponseDto>> UpdateServiceAsync(Guid id, ServiceUpdateDto dto)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null)
                return ServiceResult<ServiceResponseDto>.NotFound("Servicio no encontrado.");

            service.Name = dto.Name;
            service.Description = dto.Description;
            service.DurationMin = dto.DurationMin;
            service.Price = dto.Price;
            service.Active = dto.Active;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Servicio {ServiceId} actualizado.", id);

            return ServiceResult<ServiceResponseDto>.Ok(new ServiceResponseDto
            {
                ServiceId = service.ServiceId,
                BusinessId = service.BusinessId,
                Name = service.Name,
                Description = service.Description,
                DurationMin = service.DurationMin,
                Price = service.Price,
                Active = service.Active
            });
        }

        public async Task<ServiceResult<bool>> DeactivateServiceAsync(Guid id)
        {
            var service = await _context.Services.FindAsync(id);
            if (service == null)
                return ServiceResult.NotFound("Servicio no encontrado.");

            service.Active = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Servicio {ServiceId} desactivado.", id);
            return ServiceResult.Ok();
        }
    }
}
