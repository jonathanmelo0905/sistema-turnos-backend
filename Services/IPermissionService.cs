using AgendaApp.Dtos;

namespace AgendaApp.Services
{
    public interface IPermissionService
    {
        List<PermissionDto> GetCatalog();
        Task<ServiceResult<List<UserPermissionAuditDto>>> GetUserPermissionsAsync(Guid userId);
        Task<HashSet<string>> GetGrantedCodesAsync(Guid userId);
        Task<ServiceResult<bool>> GrantAsync(Guid userId, string permissionCode, Guid grantedByUserId);
        Task<ServiceResult<bool>> RevokeAsync(Guid userId, string permissionCode);
    }
}
