using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly AgendaProDbContext _context;
        private readonly IPermissionCache _permissionCache;
        private readonly ILogger<PermissionService> _logger;

        public PermissionService(AgendaProDbContext context, IPermissionCache permissionCache, ILogger<PermissionService> logger)
        {
            _context = context;
            _permissionCache = permissionCache;
            _logger = logger;
        }

        public List<PermissionDto> GetCatalog()
        {
            return PermissionCodes.Catalog
                .Select(kv => new PermissionDto { Code = kv.Key, Description = kv.Value })
                .ToList();
        }

        public async Task<ServiceResult<List<UserPermissionAuditDto>>> GetUserPermissionsAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return ServiceResult<List<UserPermissionAuditDto>>.NotFound("Usuario no encontrado.");

            if (user.Role == Roles.Owner)
            {
                // Owner: permisos implícitos por rol, todo el catálogo, sin auditoría
                var implicitList = PermissionCodes.Catalog.Keys
                    .Select(code => new UserPermissionAuditDto { Code = code, Implicit = true })
                    .ToList();
                return ServiceResult<List<UserPermissionAuditDto>>.Ok(implicitList);
            }

            var granted = await _context.UserPermissions
                .Where(up => up.UserId == userId)
                .Include(up => up.Permission)
                .Select(up => new UserPermissionAuditDto
                {
                    Code = up.Permission.Code,
                    GrantedAt = up.GrantedAt,
                    GrantedByUserId = up.GrantedByUserId,
                    Implicit = false
                })
                .ToListAsync();

            return ServiceResult<List<UserPermissionAuditDto>>.Ok(granted);
        }

        public async Task<HashSet<string>> GetGrantedCodesAsync(Guid userId)
        {
            return await _permissionCache.GetPermissionCodesAsync(userId, async id =>
            {
                var codes = await _context.UserPermissions
                    .Where(up => up.UserId == id)
                    .Include(up => up.Permission)
                    .Select(up => up.Permission.Code)
                    .ToListAsync();

                return codes.ToHashSet();
            });
        }

        public async Task<ServiceResult<bool>> GrantAsync(Guid userId, string permissionCode, Guid grantedByUserId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return ServiceResult.NotFound("Usuario no encontrado.");

            var permission = await _context.Permissions.FirstOrDefaultAsync(p => p.Code == permissionCode);
            if (permission == null)
                return ServiceResult.NotFound("El permiso especificado no existe.");

            var exists = await _context.UserPermissions
                .AnyAsync(up => up.UserId == userId && up.PermissionId == permission.PermissionId);

            if (!exists)
            {
                _context.UserPermissions.Add(new UserPermission
                {
                    UserId = userId,
                    PermissionId = permission.PermissionId,
                    GrantedAt = DateTime.UtcNow,
                    GrantedByUserId = grantedByUserId
                });
                await _context.SaveChangesAsync();
            }

            _permissionCache.Invalidate(userId);
            _logger.LogInformation("Permiso {PermissionCode} otorgado al usuario {UserId} por {GrantedByUserId}.", permissionCode, userId, grantedByUserId);

            return ServiceResult.Ok();
        }

        public async Task<ServiceResult<bool>> RevokeAsync(Guid userId, string permissionCode)
        {
            var permission = await _context.Permissions.FirstOrDefaultAsync(p => p.Code == permissionCode);
            if (permission == null)
                return ServiceResult.NotFound("El permiso especificado no existe.");

            var grant = await _context.UserPermissions
                .FirstOrDefaultAsync(up => up.UserId == userId && up.PermissionId == permission.PermissionId);

            if (grant == null)
                return ServiceResult.NotFound("El usuario no tiene ese permiso otorgado.");

            _context.UserPermissions.Remove(grant);
            await _context.SaveChangesAsync();

            _permissionCache.Invalidate(userId);
            _logger.LogInformation("Permiso {PermissionCode} revocado del usuario {UserId}.", permissionCode, userId);

            return ServiceResult.Ok();
        }
    }
}
