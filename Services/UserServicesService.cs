using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;
using UserServiceEntity = AgendaApp.Context.UserService;

namespace AgendaApp.Services
{
    public class UserServicesService : IUserServicesService
    {
        private readonly AgendaProDbContext _context;
        private readonly ILogger<UserServicesService> _logger;

        public UserServicesService(AgendaProDbContext context, ILogger<UserServicesService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<UserServiceResponseDto>> GetUserServicesAsync(Guid userId)
        {
            return await _context.UserServices
                .Where(us => us.UserId == userId)
                .Join(_context.Services,
                      us => us.ServiceId,
                      s => s.ServiceId,
                      (us, s) => new UserServiceResponseDto
                      {
                          UserId = userId,
                          ServiceId = s.ServiceId,
                          Name = s.Name,
                          Description = s.Description,
                          Price = s.Price,
                          DurationMin = s.DurationMin,
                          Active = s.Active
                      })
                .ToListAsync();
        }

        public async Task<ServiceResult<bool>> AssignServicesAsync(Guid userId, AssignUserServicesDto dto)
        {
            if (dto.ServiceIds == null || dto.ServiceIds.Count == 0)
                return ServiceResult.BadRequest("Debe proporcionar al menos un servicio.");

            foreach (var serviceId in dto.ServiceIds)
            {
                bool exists = await _context.UserServices.AnyAsync(us => us.UserId == userId && us.ServiceId == serviceId);
                if (!exists)
                {
                    _context.UserServices.Add(new UserServiceEntity
                    {
                        UserId = userId,
                        ServiceId = serviceId
                    });
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Servicios asignados al usuario {UserId}: {ServiceIds}.", userId, string.Join(",", dto.ServiceIds));
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult<bool>> RemoveUserServiceAsync(Guid userId, Guid serviceId)
        {
            var userService = await _context.UserServices.FindAsync(userId, serviceId);
            if (userService == null)
                return ServiceResult.NotFound("El servicio no está asignado al usuario.");

            _context.UserServices.Remove(userService);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Servicio {ServiceId} removido del usuario {UserId}.", serviceId, userId);
            return ServiceResult.Ok();
        }
    }
}
