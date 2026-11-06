using AgendaApp.Authorization;
using Microsoft.Extensions.Caching.Memory;

namespace AgendaApp.Services
{
    // Caché corta (TTL 45 s) para no consultar la BD en cada petición con sesión: permisos por
    // usuario, estado de la sesión (activo, rol, empresa) y estado de cada empresa. Se invalida
    // explícitamente al otorgar/revocar permisos, cambiar el rol, desactivar al usuario o
    // aprobar/rechazar la empresa, para que el cambio sea inmediato sin reemitir el JWT.
    public class PermissionCache : IPermissionCache
    {
        private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(45);
        private readonly IMemoryCache _cache;

        public PermissionCache(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<HashSet<string>> GetPermissionCodesAsync(Guid userId, Func<Guid, Task<HashSet<string>>> loader)
        {
            var key = CacheKey(userId);

            if (_cache.TryGetValue(key, out HashSet<string>? cached) && cached != null)
                return cached;

            var loaded = await loader(userId);
            _cache.Set(key, loaded, Ttl);
            return loaded;
        }

        public async Task<EstadoSesion?> GetSesionAsync(Guid userId, Func<Guid, Task<EstadoSesion?>> loader)
        {
            var key = SesionKey(userId);
            if (_cache.TryGetValue(key, out EstadoSesion? cached) && cached != null)
                return cached;

            var loaded = await loader(userId);
            if (loaded != null) _cache.Set(key, loaded, Ttl);
            return loaded;
        }

        public async Task<string?> GetEstadoEmpresaAsync(Guid businessId, Func<Guid, Task<string?>> loader)
        {
            var key = EmpresaKey(businessId);
            if (_cache.TryGetValue(key, out string? cached) && cached != null)
                return cached;

            var loaded = await loader(businessId);
            if (loaded != null) _cache.Set(key, loaded, Ttl);
            return loaded;
        }

        public void Invalidate(Guid userId)
        {
            _cache.Remove(CacheKey(userId));
            _cache.Remove(SesionKey(userId));
        }

        public void InvalidateBusiness(Guid businessId) => _cache.Remove(EmpresaKey(businessId));

        private static string CacheKey(Guid userId) => $"user-permissions:{userId}";
        private static string SesionKey(Guid userId) => $"user-session:{userId}";
        private static string EmpresaKey(Guid businessId) => $"business-status:{businessId}";
    }
}
