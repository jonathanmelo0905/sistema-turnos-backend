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

            return ServiceResult<BusinessResponseDto>.Ok(ToDto(business));
        }

        public async Task<ServiceResult<BusinessResponseDto>> UpdateBusinessAsync(Guid businessId, BusinessUpdateDto dto)
        {
            var business = await _context.Businesses
                .Include(b => b.Users)
                .FirstOrDefaultAsync(b => b.BusinessId == businessId);

            if (business == null)
                return ServiceResult<BusinessResponseDto>.NotFound("Negocio no encontrado.");

            if (await _context.Businesses.AnyAsync(b => b.Email == dto.Email && b.BusinessId != businessId))
                return ServiceResult<BusinessResponseDto>.Conflict("Ya existe otra empresa con ese correo.");

            business.Name = dto.Name.Trim();
            business.Email = dto.Email.Trim();
            business.Phone = Clean(dto.Phone);
            business.Address = Clean(dto.Address);
            business.Description = Clean(dto.Description);
            business.WhatsApp = Clean(dto.WhatsApp);
            business.Instagram = Clean(dto.Instagram);
            business.Facebook = Clean(dto.Facebook);
            business.TikTok = Clean(dto.TikTok);
            business.AllowOutOfHoursBooking = dto.AllowOutOfHoursBooking;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Perfil del negocio {BusinessId} actualizado.", businessId);

            return ServiceResult<BusinessResponseDto>.Ok(ToDto(business));
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static BusinessResponseDto ToDto(Business business) => new()
        {
            BusinessId = business.BusinessId,
            Name = business.Name,
            Email = business.Email,
            Phone = business.Phone,
            Address = business.Address,
            Description = business.Description,
            WhatsApp = business.WhatsApp,
            Instagram = business.Instagram,
            Facebook = business.Facebook,
            TikTok = business.TikTok,
            AllowOutOfHoursBooking = business.AllowOutOfHoursBooking,
            PlanId = business.PlanId,
            CreatedAt = business.CreatedAt,
            Users = business.Users.Select(u => new UserResponseDto
            {
                UserId = u.UserId,
                Name = u.Name,
                Email = u.Email,
                Phone = u.Phone,
                BusinessId = business.BusinessId,
                Role = u.Role,
                Active = u.Active,
                CreatedAt = u.CreatedAt,
            }).ToList()
        };
    }
}
