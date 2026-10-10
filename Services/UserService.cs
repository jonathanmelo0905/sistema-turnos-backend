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
                .OrderBy(u => u.Name)
                .Select(u => new UserResponseDto
                {
                    UserId = u.UserId,
                    BusinessId = u.BusinessId,
                    Name = u.Name,
                    Email = u.Email,
                    Phone = u.Phone,
                    Role = u.Role,
                    Active = u.Active,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<UserResponseDto?> GetUserAsync(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            return user == null ? null : ToDto(user);
        }

        public async Task<ServiceResult<UserResponseDto>> CreateUserAsync(CreateUserDto dto)
        {
            if (!Roles.IsValid(dto.Role))
                return ServiceResult<UserResponseDto>.BadRequest($"Role inválido. Valores permitidos: {string.Join(", ", Roles.All)}.");

            var email = dto.Email.Trim().ToLowerInvariant();
            if (await _context.Users.AnyAsync(u => u.Email == email))
                return ServiceResult<UserResponseDto>.Conflict("Ya existe un usuario con ese correo.");

            var user = new User
            {
                UserId = Guid.NewGuid(),
                BusinessId = dto.BusinessId,
                Name = dto.Name.Trim(),
                Email = email,
                Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = dto.Role,
                Active = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Usuario {UserId} creado para el negocio {BusinessId} con rol {Role}.", user.UserId, user.BusinessId, user.Role);

            return ServiceResult<UserResponseDto>.Ok(ToDto(user));
        }

        public async Task<ServiceResult<bool>> UpdateUserAsync(Guid id, UpdateUserDto dto)
        {
            if (!Roles.IsValid(dto.Role))
                return ServiceResult.BadRequest($"Role inválido. Valores permitidos: {string.Join(", ", Roles.All)}.");

            if (!string.IsNullOrEmpty(dto.NewPassword) && dto.NewPassword.Length < 6)
                return ServiceResult.BadRequest("La contraseña debe tener al menos 6 caracteres.");

            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return ServiceResult.NotFound("Usuario no encontrado.");

            var roleChanged = user.Role != dto.Role;
            var deactivated = user.Active && !dto.Active;

            user.Name = dto.Name.Trim();
            user.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();
            user.Role = dto.Role;
            user.Active = dto.Active;

            var passwordChanged = !string.IsNullOrEmpty(dto.NewPassword);
            if (passwordChanged)
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

            // Al desactivar o cambiar la contraseña se cierran sus sesiones abiertas
            if (deactivated || passwordChanged)
                await RevokeSessionsAsync(id);

            await _context.SaveChangesAsync();

            if (roleChanged)
            {
                // El cambio de rol afecta si el usuario tiene acceso implícito (Owner) o
                // depende de la tabla de permisos (Encargado) — se invalida la cache para
                // que el nuevo rol surta efecto de inmediato, igual que al otorgar/revocar.
                _permissionCache.Invalidate(id);
            }

            _logger.LogInformation("Usuario {UserId} actualizado. RoleChanged={RoleChanged} Active={Active}.", id, roleChanged, dto.Active);
            return ServiceResult.Ok();
        }

        // Borrado lógico: conserva el historial de turnos del colaborador.
        public async Task<bool> DeleteUserAsync(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return false;

            user.Active = false;
            await RevokeSessionsAsync(id);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Usuario {UserId} desactivado.", id);
            return true;
        }

        private async Task RevokeSessionsAsync(Guid userId)
        {
            var tokens = await _context.RefreshTokens.Where(r => r.UserId == userId && !r.Revoked).ToListAsync();
            tokens.ForEach(t => t.Revoked = true);
        }

        private static UserResponseDto ToDto(User user) => new()
        {
            UserId = user.UserId,
            BusinessId = user.BusinessId,
            Name = user.Name,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role,
            Active = user.Active,
            CreatedAt = user.CreatedAt
        };
    }
}
