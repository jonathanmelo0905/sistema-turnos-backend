using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public class UserService : IUserService
    {
        private readonly AgendaProDbContext _context;
        private readonly IPermissionCache _permissionCache;
        private readonly ILogger<UserService> _logger;

        public UserService(AgendaProDbContext context, IPermissionCache permissionCache, ILogger<UserService> logger)
        {
            _context = context;
            _permissionCache = permissionCache;
            _logger = logger;
        }

        public async Task<List<UserResponseDto>> GetUsersByBusinessAsync(Guid businessId)
        {
            return await _context.Users
                .Where(u => u.BusinessId == businessId)
                .Select(u => new UserResponseDto
                {
                    UserId = u.UserId,
                    BusinessId = u.BusinessId,
                    Name = u.Name,
                    Email = u.Email,
                    Role = u.Role,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<UserResponseDto?> GetUserAsync(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return null;

            return new UserResponseDto
            {
                UserId = user.UserId,
                BusinessId = user.BusinessId,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<ServiceResult<UserResponseDto>> CreateUserAsync(CreateUserDto dto)
        {
            if (!Roles.IsValid(dto.Role))
                return ServiceResult<UserResponseDto>.BadRequest($"Role inválido. Valores permitidos: {string.Join(", ", Roles.All)}.");

            var user = new User
            {
                UserId = Guid.NewGuid(),
                BusinessId = dto.BusinessId,
                Name = dto.Name,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = dto.Role,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Usuario {UserId} creado para el negocio {BusinessId} con rol {Role}.", user.UserId, user.BusinessId, user.Role);

            return ServiceResult<UserResponseDto>.Ok(new UserResponseDto
            {
                UserId = user.UserId,
                BusinessId = user.BusinessId,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            });
        }

        public async Task<ServiceResult<bool>> UpdateUserAsync(Guid id, UpdateUserDto dto)
        {
            if (!Roles.IsValid(dto.Role))
                return ServiceResult.BadRequest($"Role inválido. Valores permitidos: {string.Join(", ", Roles.All)}.");

            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return ServiceResult.NotFound("Usuario no encontrado.");

            var roleChanged = user.Role != dto.Role;

            user.Name = dto.Name;
            user.Role = dto.Role;

            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            if (roleChanged)
            {
                // El cambio de rol afecta si el usuario tiene acceso implícito (Owner) o
                // depende de la tabla de permisos (Encargado) — se invalida la cache para
                // que el nuevo rol surta efecto de inmediato, igual que al otorgar/revocar.
                _permissionCache.Invalidate(id);
            }

            _logger.LogInformation("Usuario {UserId} actualizado. RoleChanged={RoleChanged}.", id, roleChanged);
            return ServiceResult.Ok();
        }

        public async Task<bool> DeleteUserAsync(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return false;

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Usuario {UserId} eliminado.", id);
            return true;
        }
    }
}
