using System.Security.Claims;
using AgendaApp.Context;
using AgendaApp.Services;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Authorization
{
    // Aislamiento multiempresa: todo recurso que llega por id se valida contra el BusinessId del token.
    public interface ITenantGuard
    {
        Guid UserId { get; }
        Guid BusinessId { get; }
        string? Role { get; }
        bool IsOwner { get; }
        bool IsOwnBusiness(Guid businessId);
        Task<bool> UserInBusinessAsync(Guid userId);
        Task<bool> ServiceInBusinessAsync(Guid serviceId);
        Task<bool> CategoryInBusinessAsync(Guid categoryId);
        Task<bool> HasPermissionAsync(string permissionCode);
        // Mismo negocio y (es él mismo, es Owner o tiene collaborators.manage)
        Task<bool> CanManageUserAsync(Guid userId);
    }

    public class TenantGuard : ITenantGuard
    {
        private readonly AgendaProDbContext _context;
        private readonly IPermissionService _permissionService;
        private readonly ClaimsPrincipal? _user;

        public TenantGuard(IHttpContextAccessor accessor, AgendaProDbContext context, IPermissionService permissionService)
        {
            _context = context;
            _permissionService = permissionService;
            _user = accessor.HttpContext?.User;
        }

        public Guid UserId => ParseClaim(ClaimTypes.NameIdentifier) ?? ParseClaim("sub") ?? Guid.Empty;
        public Guid BusinessId => ParseClaim("BusinessId") ?? Guid.Empty;
        public string? Role => _user?.FindFirst(ClaimTypes.Role)?.Value;
        public bool IsOwner => Role == Roles.Owner;

        public bool IsOwnBusiness(Guid businessId) => businessId != Guid.Empty && businessId == BusinessId;

        public Task<bool> UserInBusinessAsync(Guid userId) =>
            _context.Users.AnyAsync(u => u.UserId == userId && u.BusinessId == BusinessId);

        public Task<bool> ServiceInBusinessAsync(Guid serviceId) =>
            _context.Services.AnyAsync(s => s.ServiceId == serviceId && s.BusinessId == BusinessId);

        public Task<bool> CategoryInBusinessAsync(Guid categoryId) =>
            _context.Categories.AnyAsync(c => c.CategoryId == categoryId && c.BusinessId == BusinessId);

        public async Task<bool> HasPermissionAsync(string permissionCode)
        {
            if (IsOwner) return true;
            var codes = await _permissionService.GetGrantedCodesAsync(UserId);
            return codes.Contains(permissionCode);
        }

        public async Task<bool> CanManageUserAsync(Guid userId)
        {
            if (!await UserInBusinessAsync(userId)) return false;
            return userId == UserId || await HasPermissionAsync(PermissionCodes.CollaboratorsManage);
        }

        private Guid? ParseClaim(string type) =>
            Guid.TryParse(_user?.FindFirst(type)?.Value, out var id) ? id : null;
    }
}
