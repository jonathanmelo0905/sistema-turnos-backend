using Microsoft.Extensions.Caching.Memory;

namespace AgendaApp.Services
{
    // Cache corta de permisos por usuario (TTL 45s) para no consultar la BD en cada
    // request autenticado. Se invalida explícitamente al otorgar/revocar un permiso
    // o al cambiar el Role de un usuario, para que el cambio sea inmediato sin
    // tener que reemitir el JWT.
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

        public void Invalidate(Guid userId)
        {
            _cache.Remove(CacheKey(userId));
        }

        private static string CacheKey(Guid userId) => $"user-permissions:{userId}";
    }
}
