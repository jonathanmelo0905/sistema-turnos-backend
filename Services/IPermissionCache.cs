using AgendaApp.Authorization;

namespace AgendaApp.Services
{
    public interface IPermissionCache
    {
        Task<HashSet<string>> GetPermissionCodesAsync(Guid userId, Func<Guid, Task<HashSet<string>>> loader);
        // Activo, rol y empresa del usuario (SesionVigente lo compara con el JWT en cada petición)
        Task<EstadoSesion?> GetSesionAsync(Guid userId, Func<Guid, Task<EstadoSesion?>> loader);
        // Status de la empresa (Approved, Rejected, Internal…)
        Task<string?> GetEstadoEmpresaAsync(Guid businessId, Func<Guid, Task<string?>> loader);
        // Permisos y estado de sesión del usuario: llamar al cambiar permisos, rol, activo o contraseña
        void Invalidate(Guid userId);
        // Estado de la empresa: llamar al aprobarla o rechazarla
        void InvalidateBusiness(Guid businessId);
    }
}
