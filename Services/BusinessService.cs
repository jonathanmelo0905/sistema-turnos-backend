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

        public async Task<ServiceResult<BusinessSetupDto>> GetSetupAsync(Guid businessId)
        {
            var b = await _context.Businesses.AsNoTracking().FirstOrDefaultAsync(x => x.BusinessId == businessId);
            if (b == null)
                return ServiceResult<BusinessSetupDto>.NotFound("No encontramos la empresa.");

            return ServiceResult<BusinessSetupDto>.Ok(new BusinessSetupDto
            {
                Datos = (!string.IsNullOrWhiteSpace(b.Phone) || !string.IsNullOrWhiteSpace(b.WhatsApp))
                    && !string.IsNullOrWhiteSpace(b.Address) && !string.IsNullOrWhiteSpace(b.Description),
                Imagenes = b.LogoUrl != null && b.CoverUrl != null,
                Horario = await _context.BusinessSchedules.AnyAsync(s => s.BusinessId == businessId && s.IsOpen),
                Categorias = await _context.Categories.AnyAsync(c => c.BusinessId == businessId && c.Active),
                Servicios = await _context.Services.AnyAsync(s => s.BusinessId == businessId && s.Active),
                Equipo = await _context.UserServices.AnyAsync(us => us.User.BusinessId == businessId && us.User.Active && us.Service.Active),
                Publicada = b.IsPublished
            });
        }

        // El dueño decide si la empresa sale en la web pública (directorio y página de reservas)
        public async Task<ServiceResult<BusinessResponseDto>> SetVisibilityAsync(Guid businessId, bool published)
        {
            var business = await _context.Businesses.FirstOrDefaultAsync(b => b.BusinessId == businessId);
            if (business == null)
                return ServiceResult<BusinessResponseDto>.NotFound("No encontramos la empresa.");
            if (published && string.IsNullOrWhiteSpace(business.Slug))
                return ServiceResult<BusinessResponseDto>.BadRequest("Define la dirección de tu página antes de publicarla.");

            business.IsPublished = published;
            await _context.SaveChangesAsync();
            return await GetBusinessAsync(businessId);
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
            business.CancelLimitHours = dto.CancelLimitHours;
            business.MinAdvanceMinutes = dto.MinAdvanceMinutes;
            business.BufferMinutes = dto.BufferMinutes;
            business.MaxDaysAhead = dto.MaxDaysAhead;
            business.ValleyOnReschedule = dto.ValleyOnReschedule;

            if (!string.IsNullOrWhiteSpace(dto.Slug))
            {
                var slug = SlugHelper.Normalize(dto.Slug);
                if (await _context.Businesses.AnyAsync(b => b.Slug == slug && b.BusinessId != businessId))
                    return ServiceResult<BusinessResponseDto>.Conflict("Esa dirección ya la usa otra empresa. Prueba con otra.");
                business.Slug = slug;
            }

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
            LogoUrl = business.LogoUrl,
            IsPublished = business.IsPublished,
            CoverUrl = business.CoverUrl,
            Address = business.Address,
            Description = business.Description,
            WhatsApp = business.WhatsApp,
            Instagram = business.Instagram,
            Facebook = business.Facebook,
            TikTok = business.TikTok,
            AllowOutOfHoursBooking = business.AllowOutOfHoursBooking,
            Slug = business.Slug,
            CancelLimitHours = business.CancelLimitHours,
            MinAdvanceMinutes = business.MinAdvanceMinutes,
            BufferMinutes = business.BufferMinutes,
            MaxDaysAhead = business.MaxDaysAhead,
            ValleyOnReschedule = business.ValleyOnReschedule,
            PlanId = business.PlanId,
            CreatedAt = business.CreatedAt,
            Users = business.Users.Select(u => new UserResponseDto
            {
                UserId = u.UserId,
                Name = u.Name,
                Email = u.Email,
                Phone = u.Phone,
                PhotoUrl = u.PhotoUrl,
                BusinessId = business.BusinessId,
                Role = u.Role,
                Active = u.Active,
                CreatedAt = u.CreatedAt,
            }).ToList()
        };
    }
}
