using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public class BusinessService : IBusinessService
    {
        private readonly AgendaProDbContext _context;
        private readonly ILogger<BusinessService> _logger;

        public BusinessService(AgendaProDbContext context, ILogger<BusinessService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ServiceResult<BusinessResponseDto>> GetBusinessAsync(Guid businessId)
        {
            var business = await _context.Businesses
                .Include(b => b.Users)
                .FirstOrDefaultAsync(b => b.BusinessId == businessId);

            if (business == null)
            {
                _logger.LogWarning("Negocio no encontrado: {BusinessId}", businessId);
                return ServiceResult<BusinessResponseDto>.NotFound("Negocio no encontrado.");
            }

            var response = new BusinessResponseDto
            {
                BusinessId = business.BusinessId,
                Name = business.Name,
                Email = business.Email,
                Phone = business.Phone,
                Address = business.Address,
                PlanId = business.PlanId,
                CreatedAt = business.CreatedAt,
                Users = business.Users.Select(u => new UserResponseDto
                {
                    UserId = u.UserId,
                    Name = u.Name,
                    Email = u.Email,
                    BusinessId = business.BusinessId,
                    Role = u.Role,
                    CreatedAt = u.CreatedAt,
                }).ToList()
            };

            return ServiceResult<BusinessResponseDto>.Ok(response);
        }
    }
}
